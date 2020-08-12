using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApplicationCore.BackgroundJobs
{
    public class BackgroundJobsServiceCore<TJobItem>
    {
        private readonly ILogger<BackgroundJobsServiceCore<TJobItem>> logger;
        private readonly IOptions<JobsOptions> options;
        private readonly IBackgroundJobQueue<TJobItem> queue;
        private readonly IServiceProvider services;
        private Task backgroundSave;
        private bool inprogress;

        public BackgroundJobsServiceCore(IServiceProvider services,
            IBackgroundJobQueue<TJobItem> queue,
            ILogger<BackgroundJobsServiceCore<TJobItem>> logger, IOptions<JobsOptions> options)
        {
            this.services = services;
            this.queue = queue;
            this.logger = logger;
            this.options = options;
        }

        void EnsurePeriodicBackgroundSave(CancellationToken stoppingToken)
        {
            if (!queue.NeedsSaving) return;

            if (backgroundSave == null || backgroundSave.IsCompleted)
            {
                logger.LogInformation("Starting periodic queue saving");

                backgroundSave = Task.Run(async () =>
                {
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        try
                        {
                            var delay = TimeSpan.FromMilliseconds(options.Value
                                .PeriodicBackgroundSaveIntervalMilliseconds);
                            await Task.Delay(
                                delay,
                                stoppingToken);

                            await PersistQueue();
                            var length = await queue.GetLength();

                            if (length == 0 && !inprogress)
                            {
                                logger.LogInformation("Stopping periodic queue saving as queue is empty");
                                break;
                            }

                            logger.LogInformation("Periodic save of queue to storage complete. Next save in {time:#}s",
                                delay.TotalSeconds);
                        }
                        catch (OperationCanceledException)
                        {
                        }
                    }
                }, stoppingToken);
            }
        }

        public async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation(
                "BackgroundJobService started. Checking for previous execution queue that may need to be resumed...");

            await queue.Resume();

            //TODO: may wish to periodically handle items that have been dequeued but not completed
            //      they are available from queue.GetDeadLetterItems()

            while (!stoppingToken.IsCancellationRequested)
            {
                var length = await queue.GetLength();
                logger.LogInformation("Current queue length is {length}", length);

                inprogress = false;
                var workItem = await queue.DequeueAsync(stoppingToken);
                inprogress = true;

                EnsurePeriodicBackgroundSave(stoppingToken);

                try
                {
                    using (var scope = services.CreateScope())
                    {
                        var scopedProcessingService =
                            scope.ServiceProvider
                                .GetRequiredService<IBackgroundJob<TJobItem>>();

                        await scopedProcessingService.ProcessItem(stoppingToken, workItem.Value);

                        //item done so remove it from queue
                        await workItem.Complete();
                    }
                }
                catch (OperationCanceledException ex)
                {
                    logger.LogWarning(ex,
                        "Cancelled work item {WorkItem} before completion. It will re-queued automatically",
                        workItem.Value);

                    //save the queue before shutting down
                    var lengthAsCancelled = await queue.GetLength();
                    logger.LogInformation("Queue length is {length} - Saving before shutdown", lengthAsCancelled);
                    await PersistQueue();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Error occurred executing work item id {WorkItem}.", workItem);
                }
            }
        }


        public async Task OnStop(CancellationToken stoppingToken)
        {
            logger.LogInformation("BackgroundJobService stopped");
            await PersistQueue();
        }

        private async Task PersistQueue()
        {
            if (queue.NeedsSaving)
                await queue.Save();
        }
    }
}
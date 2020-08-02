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
        private bool inprogress = false;

        public BackgroundJobsServiceCore(IServiceProvider services,
            IBackgroundJobQueue<TJobItem> queue,
            ILogger<BackgroundJobsServiceCore<TJobItem>> logger, IOptions<JobsOptions> options)
        {
            this.services = services;
            this.queue = queue;
            this.logger = logger;
            this.options = options;
        }

        public async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("BackgroundJobService started. Checking for previous execution queue that may need to be resumed...");

            await queue.Resume();

            while (!stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Current queue length is {length}", queue.Length);

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

                        await scopedProcessingService.ProcessItem(stoppingToken, workItem);
                    }
                }
                catch (OperationCanceledException ex)
                {
                    // put the cancelled item back on the queue
                    queue.QueueBackgroundWorkItem(workItem);
                    logger.LogWarning(ex,
                        "Cancelled work item id {WorkItem} before completion. It will re re-queued", workItem);

                    //save the queue before shutting down
                    logger.LogInformation("Queue length is {length} - Saving before shutdown", queue.Length);
                    await queue.Save();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Error occurred executing work item id {WorkItem}.", workItem);
                }
            }
        }

        void EnsurePeriodicBackgroundSave(CancellationToken stoppingToken)
        {
            if (backgroundSave == null || backgroundSave.IsCompleted)
            {
                logger.LogInformation("Starting periodic queue saving");

                backgroundSave = Task.Run(async () =>
                {
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        try
                        {
                            var delay = TimeSpan.FromMilliseconds(options.Value.PeriodicBackgroundSaveIntervalMilliseconds);
                            await Task.Delay(
                                delay,
                                stoppingToken);

                            await queue.Save();
                            if (queue.Length == 0 && !inprogress)
                            {
                                logger.LogInformation("Stopping periodic queue saving as queue is empty");
                                break;
                            }
                            else
                            {
                                logger.LogInformation("Periodic save of queue to storage complete. Next save in {time:#}s",delay.TotalSeconds);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                        }
                    }
                }, stoppingToken);
            }
        }


        public async Task OnStop(CancellationToken stoppingToken)
        {
            logger.LogInformation("BackgroundJobService stopped");
            await queue.Save();
        }
    }
}
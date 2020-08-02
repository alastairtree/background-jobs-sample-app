using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            services.AddJobs<DoSomethingComplexJob, ItemToBeProcessed>(Configuration);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }

    public static class ServiceCollectionExtensions 
    {
        /// <summary>
        /// Register a queue of TJobItem and a hosted service to process each item with an instance of TJob
        /// </summary>
        /// <typeparam name="TJob"></typeparam>
        /// <typeparam name="TJobItem"></typeparam>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddJobs<TJob, TJobItem>(this IServiceCollection services, IConfiguration config) where TJob : class, IBackgroundJob<TJobItem>
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            services.AddOptions<JobsOptions>()
                .Bind(config.GetSection(JobsOptions.Jobs));

            // ensure at least a json serialiser is available
            services.TryAddSingleton(typeof(ISerializer<>), typeof(JsonSerializer<>));

            // Would rather call services.AddHostedService but using generics so must re-implement the call as no overload available
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, QueueReaderJobsService<TJobItem>>());

            // single instance of the queue per queue item type
            services.AddSingleton(typeof(IBackgroundJobQueue<>), typeof(BackgroundJobQueue<>));

            // new instance of each job generated in a scope by QueueReaderJobsService
            services.AddScoped<IBackgroundJob<TJobItem>, TJob>();

            services.AddScoped<IBackgroundJob<TJobItem>, TJob>();

            services.AddSingleton(typeof(IStorage<>), typeof(FileStorage<>));

            return services;
        }
    }


    public interface IBackgroundJobQueue<TJobItem>
    {
        int Length { get; }
        void QueueBackgroundWorkItem(TJobItem id);
        Task<TJobItem> DequeueAsync(CancellationToken cancellationToken);
        Task Save();
        Task Resume();
    }

    public interface ISerializer<TJobItem>
    {
        string Serialize(TJobItem item);
        TJobItem Deserialize(string line);
    }

    public class JsonSerializer<TJobItem> : ISerializer<TJobItem>
    {
        public string Serialize(TJobItem item) => JsonSerializer.Serialize(item);
        public TJobItem Deserialize(string line) => JsonSerializer.Deserialize<TJobItem>(line);
    }

    public interface IStorage<TItem>
    {
        Task Save(TItem[] currentQueue);
        Task<TItem[]> Get();
    }

    public class FileStorage<TItem> : IStorage<TItem>
    {
        private readonly ISerializer<TItem[]> serializer;
        private readonly ILogger<FileStorage<TItem>> logger;
        private readonly string filePath;

        public FileStorage(ISerializer<TItem[]> serializer, IOptions<JobsOptions> options, ILogger<FileStorage<TItem>> logger)
        {
            this.serializer = serializer;
            this.logger = logger;
            var jobName = typeof(TItem).Name;

            filePath = Path.GetFullPath(string.Format(options.Value.StoragePath, jobName));
            logger.LogInformation("Using job resume file to {filePath}", filePath);
        }
        public async Task Save(TItem[] items)
        {
            var dirtyData = items.Any() || File.Exists(filePath);
            if (dirtyData)
            {
                var fileData = serializer.Serialize(items);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.Delete(filePath);
                if (items.Any())
                {
                    await File.WriteAllTextAsync(filePath, fileData);
                }
            }
        }

        public async Task<TItem[]> Get()
        {
            if (!Directory.Exists(Path.GetDirectoryName(filePath)) || !File.Exists(filePath))
                return Array.Empty<TItem>();

            var json = await File.ReadAllTextAsync(filePath);

            var jobItems = json.Trim().Length > 0 ? serializer.Deserialize(json) : Array.Empty<TItem>();

            return jobItems;
        }
    }

    public class BackgroundJobQueue<TJobItem> : IBackgroundJobQueue<TJobItem>
    {
        private readonly IStorage<TJobItem> storage;

        public BackgroundJobQueue(IStorage<TJobItem> storage)
        {
            this.storage = storage;
        }
        private readonly SemaphoreSlim signal = new SemaphoreSlim(0);

        private readonly ConcurrentQueue<TJobItem> workItems =
            new ConcurrentQueue<TJobItem>();

        public void QueueBackgroundWorkItem(
            TJobItem workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            workItems.Enqueue(workItem);
            signal.Release();
        }

        public async Task<TJobItem> DequeueAsync(CancellationToken cancellationToken)
        {
            await signal.WaitAsync(cancellationToken);

            workItems.TryDequeue(out var workItem);

            return workItem;
        }

        public int Length => workItems.Count;

        public async Task Save()
        {
            var currentQueue = workItems.ToArray();
            await storage.Save(currentQueue);
        }

        public async Task Resume()
        {
            foreach (var item in await storage.Get())
            {
                QueueBackgroundWorkItem(item);
            }
        }
    }

    public class JobsOptions
    {
        public const string Jobs = "Jobs";
        public int PeriodicBackgroundSaveIntervalMilliseconds { get; set; } = 5000;

        public string StoragePath { get; set; } = "\\temp\\{0}-resume.json";
    }

    public interface IBackgroundJob<in TJobItem>
    {
        Task ProcessItem(CancellationToken stoppingToken, TJobItem jobItem);
    }

    public class ItemToBeProcessed
    {
        public int Id { get; set; }
        public string Message { get; set; }
    }

    internal class DoSomethingComplexJob : IBackgroundJob<ItemToBeProcessed>
    {
        private readonly ILogger logger;

        public DoSomethingComplexJob(ILogger<DoSomethingComplexJob> logger)
        {
            this.logger = logger;
        }

        public async Task ProcessItem(CancellationToken stoppingToken, ItemToBeProcessed item)
        {
            var executionCount = 0;
            while (!stoppingToken.IsCancellationRequested && executionCount < 3)
            {
                executionCount++;

                logger.LogInformation(
                    "Job is working - task id {id}. Count: {Count}", item.Id, executionCount);

                await Task.Delay(2000, stoppingToken);
            }

            logger.LogInformation("Job with message '{msg}' DONE - task id {id}", item.Message, item.Id);
        }
    }

    public class QueueReaderJobsService<TJobItem> : BackgroundService
    {
        private readonly ILogger<QueueReaderJobsService<TJobItem>> logger;
        private readonly IOptions<JobsOptions> options;
        private readonly IBackgroundJobQueue<TJobItem> queue;
        private readonly IServiceProvider services;
        private Task backgroundSave;

        public QueueReaderJobsService(IServiceProvider services,
            IBackgroundJobQueue<TJobItem> queue,
            ILogger<QueueReaderJobsService<TJobItem>> logger, IOptions<JobsOptions> options)
        {
            this.services = services;
            this.queue = queue;
            this.logger = logger;
            this.options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("QueueReaderJobsService running.");

            await queue.Resume();

            while (!stoppingToken.IsCancellationRequested)
            {
                var workItem =
                    await queue.DequeueAsync(stoppingToken);

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
                    await queue.Save();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Error occurred executing work item id {WorkItem}.", workItem);
                }
                finally
                {
                    await queue.Save();
                }
            }
        }

        public void EnsurePeriodicBackgroundSave(CancellationToken stoppingToken)
        {
            if (backgroundSave == null || backgroundSave.IsCompleted)
            {
                logger.LogInformation("Starting background save");

                backgroundSave = Task.Run(async () =>
                {
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(options.Value.PeriodicBackgroundSaveIntervalMilliseconds), stoppingToken);

                            logger.LogInformation("Background queue saving");

                            await queue.Save();
                            if (queue.Length == 0)
                            {
                                logger.LogInformation("Stopping background save as queue empty");
                                break;
                            }
                        }
                        catch (OperationCanceledException)
                        {

                        }
                    }
                }, stoppingToken);
            }
        }


        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("QueueReaderJobsService is stopping.");
            await queue.Save();
        }
    }
}
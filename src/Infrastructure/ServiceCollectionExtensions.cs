using System;
using ApplicationCore.BackgroundJobs;
using ApplicationCore.Storage;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        ///     Register a queue of TJobItem and a hosted service to process each item with an instance of TJob
        /// </summary>
        /// <typeparam name="TJob"></typeparam>
        /// <typeparam name="TJobItem"></typeparam>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddJob<TJob, TJobItem>(this IServiceCollection services,
            IConfiguration config) where TJob : class, IBackgroundJob<TJobItem>
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            services.AddOptions<JobsOptions>()
                .Bind(config.GetSection(JobsOptions.Jobs));

            // ensure at least a json serialiser is available
            services.TryAddSingleton(typeof(ISerializer<>), typeof(JsonSerializer<>));

            // Would rather call services.AddHostedService but using generics so must re-implement the call as no overload available
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, BackgroundJobService<TJobItem>>());

            services.TryAddSingleton(typeof(BackgroundJobsServiceCore<>));

            // single instance of the queue per queue item type
            services.AddSingleton(typeof(IBackgroundJobQueue<>), typeof(InMemoryBackgroundJobQueue<>));

            // new instance of each job generated in a scope by BackgroundJobService
            services.AddScoped<IBackgroundJob<TJobItem>, TJob>();

            services.AddScoped<IBackgroundJob<TJobItem>, TJob>();

            services.AddSingleton(typeof(IStorage<>), typeof(FileStorage<>));

            return services;
        }
    }
}
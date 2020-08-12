using System;
using ApplicationCore.BackgroundJobs;
using Foundatio.Queues;
using FoundatioFxAdapter;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        ///     replace the IBackgroundJobQueue with an implementation from Foundatio Queues
        /// </summary>
        /// <typeparam name="TJob"></typeparam>
        /// <typeparam name="TJobItem"></typeparam>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection
            AddFoundatioQueueAdapter<TJobItem, TFoundatioQueue>(this IServiceCollection services)
            where TFoundatioQueue : class, IQueue<TJobItem> where TJobItem : class
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            // Remove the original in-memory queue registration
            services.Remove(new ServiceDescriptor(typeof(IBackgroundJobQueue<>), typeof(InMemoryBackgroundJobQueue<>)));

            // replace with the new foundatio adapter
            services.AddSingleton(typeof(IBackgroundJobQueue<>), typeof(QueueAdapter<>));

            // register single instance of the foundatio queue
            services.AddSingleton(typeof(IQueue<TJobItem>), typeof(TFoundatioQueue));

            return services;
        }

        /// <summary>
        ///     replace the IBackgroundJobQueue with an implementation from Foundatio Queues
        /// </summary>
        /// <typeparam name="TJob"></typeparam>
        /// <typeparam name="TJobItem"></typeparam>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddFoundatioQueueAdapter<TJobItem, TFoundatioQueue>(
            this IServiceCollection services, Func<IServiceProvider, TFoundatioQueue> builder)
            where TFoundatioQueue : class, IQueue<TJobItem> where TJobItem : class
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            // Remove the original in-memory queue registration
            services.Remove(new ServiceDescriptor(typeof(IBackgroundJobQueue<>), typeof(InMemoryBackgroundJobQueue<>)));

            // replace with the new foundatio adapter
            services.AddSingleton(typeof(IBackgroundJobQueue<>), typeof(QueueAdapter<>));

            // register single instance of the foundatio queue
            services.AddSingleton(typeof(IQueue<TJobItem>), builder);

            return services;
        }
    }
}
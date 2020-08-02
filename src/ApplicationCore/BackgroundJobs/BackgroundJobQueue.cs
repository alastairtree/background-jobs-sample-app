using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ApplicationCore.Storage;

namespace ApplicationCore.BackgroundJobs
{
    public class BackgroundJobQueue<TJobItem> : IBackgroundJobQueue<TJobItem>
    {
        private readonly SemaphoreSlim signal = new SemaphoreSlim(0);
        private readonly IStorage<TJobItem> storage;

        private readonly ConcurrentQueue<TJobItem> workItems =
            new ConcurrentQueue<TJobItem>();

        public BackgroundJobQueue(IStorage<TJobItem> storage)
        {
            this.storage = storage;
        }

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
}
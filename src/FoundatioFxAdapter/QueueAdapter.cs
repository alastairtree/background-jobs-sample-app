using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationCore.BackgroundJobs;
using ApplicationCore.Storage;
using Foundatio.Queues;

namespace FoundatioFxAdapter
{
    public class QueueAdapter<TJobItem> : IBackgroundJobQueue<TJobItem> where TJobItem : class
    {
        private readonly IQueue<TJobItem> foundatioQueue;
        private readonly IStorage<TJobItem> storage;

        public QueueAdapter(IQueue<TJobItem> foundatioQueue, IStorage<TJobItem> storage)
        {
            this.foundatioQueue = foundatioQueue;
            this.storage = storage;
        }

        public async Task<ApplicationCore.BackgroundJobs.IQueueEntry<TJobItem>> DequeueAsync(
            CancellationToken cancellationToken)
        {
            var result = await foundatioQueue.DequeueAsync(cancellationToken);

            if (result == null)
            {
                return new ApplicationCore.BackgroundJobs.QueueEntry<TJobItem>(null, null, () => Task.CompletedTask);
            }

            // NOTE: Foundatio queues can do lots more - AbandonAsync(), RenewLockAsync(), dead letter with retries etc
            return new ApplicationCore.BackgroundJobs.QueueEntry<TJobItem>(result.Id, result.Value,
                async () => { await result.CompleteAsync(); });
        }

        public async Task<ICollection<TJobItem>> GetDeadLetterItems()
        {
            var items = await foundatioQueue.GetDeadletterItemsAsync();
            return items.ToArray();
        }

        public async Task<long> GetLength()
        {
            var stats = await foundatioQueue.GetQueueStatsAsync();
            return stats.Queued;
        }

        public bool NeedsSaving => foundatioQueue is InMemoryQueue<TJobItem>;

        public async Task QueueBackgroundWorkItem(TJobItem workItem)
        {
            await foundatioQueue.EnqueueAsync(workItem);
        }

        public async Task Resume()
        {
            if (foundatioQueue is InMemoryQueue<TJobItem>)
            {
                foreach (var item in await storage.Get())
                {
                    await QueueBackgroundWorkItem(item);
                }
            }
        }

        public async Task Save()
        {
            // assuming that all other foundatio queues like AWS SQS and ServiceBus are already
            // backed by proper storage and so would not need saving to a local disk!
            if (foundatioQueue is InMemoryQueue<TJobItem> localQueueNeedingPersistence)
            {
                var items = localQueueNeedingPersistence.GetEntries();
                var queueEntries = items.Select(x => x.Value).ToArray();
                await storage.Save(queueEntries);
            }
        }
    }
}
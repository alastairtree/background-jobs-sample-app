using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationCore.Storage;
using Microsoft.Extensions.Options;

namespace ApplicationCore.BackgroundJobs
{
    public class InMemoryBackgroundJobQueue<TJobItem> : IBackgroundJobQueue<TJobItem>
    {
        private readonly ConcurrentDictionary<string, QueueEntry<TJobItem>> dequeuedIncompleteWorkItems =
            new ConcurrentDictionary<string, QueueEntry<TJobItem>>();

        private readonly SemaphoreSlim signal = new SemaphoreSlim(0);
        private readonly IStorage<TJobItem> storage;
        private readonly TimeSpan timeBeforeDeadLetter;

        private readonly ConcurrentQueue<TJobItem> workItems =
            new ConcurrentQueue<TJobItem>();

        public InMemoryBackgroundJobQueue(IStorage<TJobItem> storage, IOptions<JobsOptions> options)
        {
            this.storage = storage;
            timeBeforeDeadLetter = options.Value.TimeBeforeDeadLetter;
        }

        public async Task<IQueueEntry<TJobItem>> DequeueAsync(CancellationToken cancellationToken)
        {
            await signal.WaitAsync(cancellationToken);

            workItems.TryDequeue(out var workItem);

            var id = Guid.NewGuid().ToString("N");
            var item = new QueueEntry<TJobItem>(id, workItem, () =>
            {
                dequeuedIncompleteWorkItems.TryRemove(id, out _);
                return Task.CompletedTask;
            });

            dequeuedIncompleteWorkItems.TryAdd(item.Id, item);
            return item;
        }

        public Task<ICollection<TJobItem>> GetDeadLetterItems()
        {
            var items = dequeuedIncompleteWorkItems.Values.Cast<IQueueEntry<TJobItem>>();
            ICollection<TJobItem> expiredItems = items
                .Where(x => x.DequeuedDate < DateTime.UtcNow.Add(-timeBeforeDeadLetter))
                .Select(x => x.Value)
                .ToArray();
            return Task.FromResult(expiredItems);
        }

        public Task<long> GetLength() => Task.FromResult((long) workItems.Count + dequeuedIncompleteWorkItems.Count);

        public bool NeedsSaving { get; } = true;

        public Task QueueBackgroundWorkItem(
            TJobItem workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            workItems.Enqueue(workItem);
            signal.Release();
            return Task.CompletedTask;
        }

        public async Task Resume()
        {
            foreach (var item in await storage.Get())
            {
                await QueueBackgroundWorkItem(item);
            }
        }

        public async Task Save()
        {
            var currentQueue =
                workItems.Union(
                    dequeuedIncompleteWorkItems.Values.Select(x => x.Value)
                ).ToArray();
            await storage.Save(currentQueue);
        }
    }
}
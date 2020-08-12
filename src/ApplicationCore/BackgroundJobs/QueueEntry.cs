using System;
using System.Threading.Tasks;

namespace ApplicationCore.BackgroundJobs
{
    public class QueueEntry<T> : IQueueEntry<T>
    {
        private readonly Func<Task> onComplete;

        public QueueEntry(string id, T value, Func<Task> onComplete)
        {
            Value = value;
            this.onComplete = onComplete;
            Id = id;
            DequeuedDate = DateTime.UtcNow;
        }

        public Task Complete()
        {
            return onComplete();
        }

        public DateTime DequeuedDate { get; }

        public string Id { get; }

        public T Value { get; }
    }
}
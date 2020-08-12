using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ApplicationCore.BackgroundJobs
{
    public interface IBackgroundJobQueue<TJobItem>
    {
        bool NeedsSaving { get; }
        Task<IQueueEntry<TJobItem>> DequeueAsync(CancellationToken cancellationToken);
        Task<ICollection<TJobItem>> GetDeadLetterItems();
        Task<long> GetLength();
        Task QueueBackgroundWorkItem(TJobItem id);
        Task Resume();
        Task Save();
    }
}
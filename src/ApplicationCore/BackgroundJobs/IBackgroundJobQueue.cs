using System.Threading;
using System.Threading.Tasks;

namespace ApplicationCore.BackgroundJobs
{
    public interface IBackgroundJobQueue<TJobItem>
    {
        int Length { get; }
        void QueueBackgroundWorkItem(TJobItem id);
        Task<TJobItem> DequeueAsync(CancellationToken cancellationToken);
        Task Save();
        Task Resume();
    }
}
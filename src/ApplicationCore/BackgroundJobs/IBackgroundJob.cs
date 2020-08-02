using System.Threading;
using System.Threading.Tasks;

namespace ApplicationCore.BackgroundJobs
{
    public interface IBackgroundJob<in TJobItem>
    {
        Task ProcessItem(CancellationToken stoppingToken, TJobItem jobItem);
    }
}
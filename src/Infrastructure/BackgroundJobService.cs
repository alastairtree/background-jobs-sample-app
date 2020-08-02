using System.Threading;
using System.Threading.Tasks;
using ApplicationCore.BackgroundJobs;
using Microsoft.Extensions.Hosting;

namespace Infrastructure
{
    public class BackgroundJobService<TJobItem> : BackgroundService
    {
        private readonly BackgroundJobsServiceCore<TJobItem> service;

        public BackgroundJobService(BackgroundJobsServiceCore<TJobItem> service)
        {
            this.service = service;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return service.ExecuteAsync(stoppingToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            base.StopAsync(cancellationToken);
            return service.OnStop(cancellationToken);
        }
    }
}
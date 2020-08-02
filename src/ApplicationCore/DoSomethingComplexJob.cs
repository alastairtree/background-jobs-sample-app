using System.Threading;
using System.Threading.Tasks;
using ApplicationCore.BackgroundJobs;
using Microsoft.Extensions.Logging;

namespace ApplicationCore
{
    public class DoSomethingComplexJob : IBackgroundJob<ItemToBeProcessed>
    {
        private readonly ILogger logger;

        public DoSomethingComplexJob(ILogger<DoSomethingComplexJob> logger)
        {
            this.logger = logger;
        }

        public async Task ProcessItem(CancellationToken stoppingToken, ItemToBeProcessed item)
        {
            var executionCount = 0;
            var maxTimes = 3;

            logger.LogInformation(
                "Received ItemToBeProcessed from the queue with id '{id}' and msg 'msg'", item.Id, item.Message);

            while (!stoppingToken.IsCancellationRequested && executionCount < maxTimes)
            {
                executionCount++;

                logger.LogInformation(
                    "Long running job DoSomethingComplexJob is running {iteration}/{max}", executionCount, maxTimes);

                await Task.Delay(2000, stoppingToken);
            }

            logger.LogInformation("DoSomethingComplexJob with id '{id}' finished", item.Id);
        }
    }
}
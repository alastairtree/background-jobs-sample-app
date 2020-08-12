using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using ApplicationCore;
using ApplicationCore.BackgroundJobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Web.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class QueueController : ControllerBase
    {
        private readonly ILogger<QueueController> logger;
        private readonly IBackgroundJobQueue<ItemToBeProcessed> queue;

        public QueueController(ILogger<QueueController> logger, IBackgroundJobQueue<ItemToBeProcessed> queue)
        {
            this.logger = logger;
            this.queue = queue;
        }

        [HttpPost]
        public IActionResult Add([Required] ItemToBeProcessed item)
        {
            logger.LogInformation("Adding item with id '{id}' to the queue", item.Id);

            queue.QueueBackgroundWorkItem(item);

            return Accepted();
        }

        [HttpGet]
        public async Task<QueueInfo> Get()
        {
            var queueLength = await queue.GetLength();
            var deadLetterItems = await queue.GetDeadLetterItems();
            return new QueueInfo
            {
                Date = DateTime.UtcNow,
                QueueLength = queueLength,
                DeadLetterLength = deadLetterItems.Count
            };
        }
    }
}
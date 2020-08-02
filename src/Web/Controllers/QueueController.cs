﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
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

        [HttpGet]
        public QueueInfo Get()
        {
            return new QueueInfo
            {
                Date = DateTime.UtcNow,
                QueueLength = this.queue.Length
            };
        }

        [HttpPost]
        public IActionResult Add([Required] ItemToBeProcessed item)
        {
            logger.LogInformation("Adding {id} to the queue", item.Id);

            queue.QueueBackgroundWorkItem(item);

            return Accepted();
        }
    }
}
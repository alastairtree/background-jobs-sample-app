using System;

namespace Web
{
    public class QueueInfo
    {
        public DateTime Date { get; set; }
        public long DeadLetterLength { get; set; }
        public long QueueLength { get; set; }
    }
}
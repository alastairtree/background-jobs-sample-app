using System;

namespace ApplicationCore.BackgroundJobs
{
    public class JobsOptions
    {
        public const string Jobs = "Jobs";
        public int PeriodicBackgroundSaveIntervalMilliseconds { get; set; } = 5000;
        public string StoragePath { get; set; } = "\\temp\\{0}-resume.json";
        public TimeSpan TimeBeforeDeadLetter { get; set; } = TimeSpan.FromMinutes(3);
    }
}
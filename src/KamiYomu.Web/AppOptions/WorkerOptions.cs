namespace KamiYomu.Web.AppOptions
{
    public class WorkerOptions
    {
        /// <summary>
        /// List of Hangfire server identifiers available to process jobs. 
        /// Each name corresponds to a distinct background worker instance; 
        /// add more entries here if you want multiple servers to share or divide queues.
        /// </summary>
        public IEnumerable<string> ServerAvailableNames { get; init; } 

        /// <summary>
        /// Controls how many background processing threads Hangfire will spawn.
        /// A higher value allows more jobs to run concurrently, but increases CPU and memory usage.
        /// </summary>
        public int WorkerCount { get; init; } = 1;


        /// <summary>
        /// Minimum delay (in milliseconds) between job executions.
        /// Helps throttle requests to external services and avoid hitting rate limits (e.g., HTTP 423 "Too Many Requests").
        /// </summary>
        public int MinWaitPeriodInMilliseconds { get; init; } = 3000;

        /// <summary>
        /// Maximum delay (in milliseconds) between job executions.
        /// Provides variability in scheduling to reduce the chance of IP blocking or service throttling.
        /// </summary>
        public int MaxWaitPeriodInMilliseconds { get; init; } = 7001;
        /// <summary>
        /// Maximum number of retry attempts for failed jobs before giving up.
        /// </summary>
        public int MaxRetryAttempts { get; init; } = 10;
        /// <summary>
        /// Queue dedicated to downloading individual chapters.
        /// </summary>
        public string[] DownloadChapterQueues { get; init; } 
        /// <summary>
        /// Queue dedicated to scheduling manga downloads (manages chapter download jobs).
        /// </summary>
        public IEnumerable<string> MangaDownloadSchedulerQueues { get; init; }
        /// <summary>
        /// Queue dedicated to discovering new chapters (polling or scraping for updates).
        /// </summary>
        public IEnumerable<string> DiscoveryNewChapterQueues { get; init; }
        /// <summary>
        /// Defines the maximum number of crawler instances allowed to run concurrently for the same source.
        /// Typically set to 1 to ensure only a single crawler operates at a time, preventing duplicate work,
        /// resource conflicts, and potential rate‑limiting or blocking by the target system.
        /// However, this can be adjusted to increase throughput if the source can handle multiple concurrent requests.
        /// </summary>
        public int MaxConcurrentCrawlerInstances { get; set; } = 1;
        public IEnumerable<string> GetAllQueues() =>
        [   "default",
            .. DownloadChapterQueues,
            .. MangaDownloadSchedulerQueues,
            .. DiscoveryNewChapterQueues,
        ];

        private static readonly Random _random = new();
        public TimeSpan GetWaitPeriod()
        {
            int milliseconds = _random.Next(MinWaitPeriodInMilliseconds, MaxWaitPeriodInMilliseconds);
            return TimeSpan.FromMilliseconds(milliseconds);
        }
    }
}

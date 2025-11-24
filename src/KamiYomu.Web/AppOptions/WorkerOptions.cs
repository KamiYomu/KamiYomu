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
        public IEnumerable<string> GetAllQueues() =>
        [   
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

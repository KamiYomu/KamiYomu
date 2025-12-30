namespace KamiYomu.Web.AppOptions;

public class WorkerOptions
{
    /// <summary>
    /// List of Hangfire server identifiers available to process jobs. 
    /// Each name corresponds to a distinct background worker instance; 
    /// add more entries here if you want multiple servers to share or divide queues.
    /// </summary>
    public required IEnumerable<string> ServerAvailableNames { get; init; }

    /// <summary>
    /// Specifies the number of background processing threads Hangfire will spawn.
    /// Increasing this value allows more jobs to run concurrently, but also raises CPU load 
    /// and memory usage.
    /// Each worker consumes ~80 MB of memory on average while active 
    /// (actual usage may vary depending on the crawler agent implementation and system configuration).
    /// </summary>
    public int WorkerCount { get; init; } = 1;

    /// <summary>
    /// Specifies the maximum number of crawler instances that can run concurrently for the same source.
    /// Typically set to 1 to ensure only a single crawler operates at a time, preventing duplicate work,
    /// resource conflicts, and potential rate‑limiting or blocking by the target system.
    /// This value can be increased to improve throughput if the source supports multiple concurrent requests.
    /// </summary>
    /// <remarks>
    /// 
    /// Relationship to other settings:
    /// <para>
    /// - <c>Worker__WorkerCount</c> defines the total number of background threads available.
    /// </para>
    /// <para>
    /// - <c>Worker__MaxConcurrentCrawlerInstances</c> restricts how many of those threads can be used by a single crawler.
    /// </para>
    /// 
    /// Examples:
    /// <para>
    /// - If <c>Worker__MaxConcurrentCrawlerInstances = 1</c> and <c>Worker__WorkerCount = 4</c>,
    ///   up to 4 different crawler agents can run independently.
    /// </para>
    /// <para>
    /// - If <c>Worker__MaxConcurrentCrawlerInstances = 2</c> and <c>Worker__WorkerCount = 6</c>,
    ///   each crawler agent can run up to 2 instances concurrently,
    ///   while up to 3 different crawler agents may be active at the same time.
    /// </para>
    /// </remarks>
    public int MaxConcurrentCrawlerInstances { get; init; } = 1;

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
    public required string[] DownloadChapterQueues { get; init; }
    /// <summary>
    /// Queue dedicated to scheduling manga downloads (manages chapter download jobs).
    /// </summary>
    public required IEnumerable<string> MangaDownloadSchedulerQueues { get; init; }
    /// <summary>
    /// Queue dedicated to discovering new chapters (polling or scraping for updates).
    /// </summary>
    public required IEnumerable<string> DiscoveryNewChapterQueues { get; init; }

    public IEnumerable<string> GetAllQueues()
    {
        return [
        Defaults.Worker.DefaultQueue,
        Defaults.Worker.DeferredExecutionQueue,
        .. DownloadChapterQueues,
        .. MangaDownloadSchedulerQueues,
        .. DiscoveryNewChapterQueues,
        ];
    }

    private static readonly Random _random = new();
    public TimeSpan GetWaitPeriod()
    {
        int milliseconds = _random.Next(MinWaitPeriodInMilliseconds, MaxWaitPeriodInMilliseconds);
        return TimeSpan.FromMilliseconds(milliseconds);
    }
}

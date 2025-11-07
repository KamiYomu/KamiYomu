namespace KamiYomu.Web
{
    public class Settings
    {
        public class SpecialFolders
        {
            public const string LogDir = "/logs";
            public const string AgentsDir = "/agents";
            public const string DbDir = "/db";
            public const string MangaDir = "/manga";
        }
        public class UI
        {
            public string DefaultLanguage { get; init; } = "en-US";
        }
        public class Worker
        {
            public static readonly string[] CrawlerQueues =
            [
                "crawler-agent-download-queue-1",
                "crawler-agent-download-queue-2",
                "crawler-agent-download-queue-3",
            ];

            public static readonly string[] FetchMangaQueues =
            [
                "crawler-agent-fetch-manga-queue-1",
                "crawler-agent-fetch-manga-queue-2",
                "crawler-agent-fetch-manga-queue-3",
            ];

            public static readonly string[] SearchQueues =
            [
                "crawler-agent-search-queue-1",
                "crawler-agent-search-queue-2",
                "crawler-agent-search-queue-3",
            ];

            public static readonly string[] AllQueues = [.. CrawlerQueues, .. SearchQueues];

            public const string HttpClientBackground = nameof(HttpClientBackground);

            private static readonly Random _random = new();
            public TimeSpan GetWaitPeriod()
            {
                int milliseconds = _random.Next(MinWaitPeriodInMilliseconds, MaxWaitPeriodInMilliseconds);
                return TimeSpan.FromMilliseconds(milliseconds);
            }

            public int MinWaitPeriodInMilliseconds { get; init; } = 3000;
            public int MaxWaitPeriodInMilliseconds { get; init; } = 7001;
            public int WorkerCount { get; init; } = 1;
        }
    }
}

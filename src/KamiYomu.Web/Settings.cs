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
            public static readonly string[] DownloadChapterQueues =
            [
                "download-chapter-queue-1",
                "download-chapter-queue-2",
                "download-chapter-queue-3",
            ];

            public static readonly string[] MangaDownloadSchedulerQueues =
            [
                "manga-download-scheduler-queue-1",
                "manga-download-scheduler-queue-2",
                "manga-download-scheduler-queue-3",
            ];

            public static readonly string DiscoveryNewChapterQueues = "discovery-new-chapter-queue";

            public static readonly string[] AllQueues = 
            [
                DiscoveryNewChapterQueues,
                .. MangaDownloadSchedulerQueues, 
                .. DownloadChapterQueues,
            ];

            public const string HttpClientBackground = nameof(HttpClientBackground);

            private static readonly Random _random = new();
            public TimeSpan GetWaitPeriod()
            {
                int milliseconds = _random.Next(MinWaitPeriodInMilliseconds, MaxWaitPeriodInMilliseconds);
                return TimeSpan.FromMilliseconds(milliseconds);
            }

            public int ChapterDiscoveryIntervalInHours { get; init; } = 6;
            public int MinWaitPeriodInMilliseconds { get; init; } = 3000;
            public int MaxWaitPeriodInMilliseconds { get; init; } = 7001;
            public int WorkerCount { get; init; } = 1;
        }
    }
}

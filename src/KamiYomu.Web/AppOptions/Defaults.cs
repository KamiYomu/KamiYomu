namespace KamiYomu.Web.AppOptions;

public partial class Defaults
{
    public static class ServiceLocator
    {
        private static readonly Lazy<IServiceProvider?> _lazyProvider = new(() => _providerFactory(), true);

        private static Func<IServiceProvider?> _providerFactory = () => null;

        public static void Configure(Func<IServiceProvider?> factory)
        {
            _providerFactory = factory;
        }

        public static IServiceProvider? Instance => _lazyProvider.Value;
    }

    public class NugetFeeds
    {
        public const string NugetFeedUrl = "https://api.nuget.org/v3/index.json";
        public const string KamiYomuFeedUrl = "https://nuget.pkg.github.com/KamiYomu/index.json";
        public const string MyGetFeedUrl = "https://www.myget.org/F/example/api/v3/index.json";
    }

    public class UI
    {
        public const string EnqueueNotification = nameof(EnqueueNotification);
        public const string PushNotification = nameof(PushNotification);
    }

    public static class Package
    {
        public const string KamiYomuCrawlerAgentTag = "kamiyomu-crawler-agents";
    }

    public static class SpecialFolders
    {
        public const string LogDir = "/logs";
        public const string AgentsDir = "/agents";
        public const string DbDir = "/db";
        public const string MangaDir = "/manga";
    }

    public static class Worker
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

    }
}

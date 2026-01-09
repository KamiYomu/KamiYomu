using KamiYomu.CrawlerAgents.Core.Catalog.Definitions;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Entities.Notifications.Definitions;

using LiteDB;


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

        public static IServiceProvider Instance => _lazyProvider.Value;
    }

    public class NugetFeeds
    {
        public const string NugetFeedUrl = "https://api.nuget.org/v3/index.json";
        public const string GiteaFeedUrl = "https://gitea.kamiyomu.com/api/packages/kamiyomu/nuget/index.json";
        public const string KamiYomuFeedUrl = "https://nuget.pkg.github.com/KamiYomu/index.json";
    }

    public static class Integrations
    {
        public const string HttpClientApp = $"{nameof(Integrations)}.{nameof(HttpClientApp)}";
    }

    public static class UI
    {
        public const string EnqueueNotification = nameof(EnqueueNotification);
        public const string PushNotification = nameof(PushNotification);
    }

    public static class Package
    {
        public const string KamiYomuCrawlerAgentTag = "kamiyomu-crawler-agents";
    }

    public static class Worker
    {
        public const string HttpClientApp = $"{nameof(Worker)}.{nameof(HttpClientApp)}";
        public const int HttpTimeOutInSeconds = 60;
        public const int StaleLockTimeout = 20;
        public const int DeferredExecutionInMinutes = 5;
        public const string DeferredExecutionQueue = "deferred-execution-queue";
        public const string DefaultQueue = "default";
        public const string TempDirName = "kamiyomu-worker.tmp";
    }

    public static class LiteDbConfig
    {
        public static void Configure()
        {
            BsonMapper mapper = BsonMapper.Global;

            mapper.RegisterType<Uri>(
                uri => uri != null ? new BsonValue(uri.ToString()) : BsonValue.Null,
                bson =>
                {
                    string str = bson.AsString;
                    return string.IsNullOrWhiteSpace(str) ? null : Uri.TryCreate(str, UriKind.RelativeOrAbsolute, out Uri? uri) ? uri : null;
                }
            );

            mapper.RegisterType(
                serialize: status => new BsonValue((int)status),
                deserialize: bson => (DownloadStatus)bson.AsInt32
            );

            mapper.RegisterType(
                serialize: status => new BsonValue((int)status),
                deserialize: bson => (NotificationType)bson.AsInt32
            );

            mapper.RegisterType(
                serialize: status => new BsonValue((int)status),
                deserialize: bson => (ReleaseStatus)bson.AsInt32
            );
        }
    }
}

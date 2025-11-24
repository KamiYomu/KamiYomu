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
        public const string HttpClientBackground = nameof(HttpClientBackground);
    }

    public static class LiteDbConfig
    {
        public static void Configure()
        {
            var mapper = BsonMapper.Global;

            mapper.RegisterType<Uri>(
                uri => uri != null ? new BsonValue(uri.ToString()) : BsonValue.Null,
                bson =>
                {
                    var str = bson.AsString;
                    if (string.IsNullOrWhiteSpace(str)) return null;

                    return Uri.TryCreate(str, UriKind.RelativeOrAbsolute, out Uri? uri) ? uri : null;
                }
            );

            mapper.RegisterType<DownloadStatus>(
                serialize: status => new BsonValue((int)status),
                deserialize: bson => (DownloadStatus)bson.AsInt32
            );

            mapper.RegisterType<NotificationType>(
                serialize: status => new BsonValue((int)status),
                deserialize: bson => (NotificationType)bson.AsInt32
            );

            mapper.RegisterType<ReleaseStatus>(
                serialize: status => new BsonValue((int)status),
                deserialize: bson => (ReleaseStatus)bson.AsInt32
            );
        }
    }

}

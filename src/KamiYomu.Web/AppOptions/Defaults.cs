using KamiYomu.CrawlerAgents.Core.Catalog.Definitions;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Models.Definitions;


namespace KamiYomu.Web.AppOptions;
/// <summary>
/// Contains default configuration constants and static utility classes for the KamiYomu application.
/// Provides centralized access to service locators, integration settings, UI constants, worker configuration,
/// and LiteDB serialization mappings.
/// </summary>
public partial class Defaults
{
    /// <summary>
    /// ServiceLocator is a static class that provides a centralized way to access
    /// services and configurations throughout the application. 
    /// It allows for the configuration of an IServiceProvider factory, 
    /// which can be used to retrieve service instances as needed.   
    /// </summary>
    public static class ServiceLocator
    {
        /// <summary>
        /// The name of the read-only database context service registration.
        /// </summary>
        public const string ReadOnlyDbContext = nameof(ReadOnlyDbContext);

        /// <summary>
        /// The name of the read-only image database context service registration.
        /// </summary>
        public const string ReadOnlyImageDbContext = nameof(ReadOnlyImageDbContext);

        /// <summary>
        /// The name of the read-only reading database context service registration.
        /// </summary>
        public const string ReadOnlyReadingDbContext = nameof(ReadOnlyReadingDbContext);

        private static readonly Lazy<IServiceProvider?> _lazyProvider = new(() => _providerFactory(), true);

        private static Func<IServiceProvider?> _providerFactory = () => null;

        /// <summary>
        /// Configures the service provider factory used to resolve service instances.
        /// </summary>
        /// <param name="factory">A function that returns an IServiceProvider instance or null.</param>
        public static void Configure(Func<IServiceProvider?> factory)
        {
            _providerFactory = factory;
        }

        /// <summary>
        /// Gets the configured IServiceProvider instance.
        /// Lazily initializes and caches the provider on first access.
        /// </summary>
        public static IServiceProvider Instance => _lazyProvider.Value;
    }

    /// <summary>
    /// Contains NuGet feed URLs used for package source configuration.
    /// </summary>
    public class NugetFeeds
    {
        /// <summary>
        /// The official NuGet.org package feed URL.
        /// </summary>
        public const string NugetFeedUrl = "https://api.nuget.org/v3/index.json";

        /// <summary>
        /// The KamiYomu packages NuGet feed URL for custom packages.
        /// </summary>
        public const string PackagesFeedUrl = "https://packages.kamiyomu.com/api/packages/kamiyomu/nuget/index.json";

        /// <summary>
        /// The GitHub NuGet feed URL for KamiYomu packages.
        /// </summary>
        public const string KamiYomuFeedUrl = "https://nuget.pkg.github.com/KamiYomu/index.json";
    }

    /// <summary>
    /// Contains configuration constants for external integrations.
    /// </summary>
    public static class Integrations
    {
        /// <summary>
        /// The service registration name for the HTTP client used by integrations.
        /// </summary>
        public const string HttpClientApp = $"{nameof(Integrations)}.{nameof(HttpClientApp)}";
    }

    /// <summary>
    /// Contains constants for UI-related events and notifications.
    /// </summary>
    public static class UI
    {
        /// <summary>
        /// The event name for enqueueing notifications to be displayed.
        /// </summary>
        public const string EnqueueNotification = nameof(EnqueueNotification);

        /// <summary>
        /// The event name for pushing notifications to the user interface.
        /// </summary>
        public const string PushNotification = nameof(PushNotification);
    }

    /// <summary>
    /// Contains constants for package tagging and classification.
    /// </summary>
    public static class Package
    {
        /// <summary>
        /// The tag identifier for content marked as not safe for work.
        /// </summary>
        public const string NotSafeForWorkTag = "nsfw";

        /// <summary>
        /// The tag identifier for KamiYomu crawler agent packages.
        /// </summary>
        public const string KamiYomuCrawlerAgentTag = "kamiyomu-crawler-agents";
    }

    /// <summary>
    /// Contains configuration constants for background worker services and job queues.
    /// </summary>
    public static class Worker
    {
        /// <summary>
        /// The service registration name for the HTTP client used by worker services.
        /// </summary>
        public const string HttpClientApp = $"{nameof(Worker)}.{nameof(HttpClientApp)}";

        /// <summary>
        /// The timeout duration in seconds for HTTP requests made by worker services.
        /// </summary>
        public const int HttpTimeOutInSeconds = 60;

        /// <summary>
        /// The timeout duration in seconds for stale job locks before they are released.
        /// </summary>
        public const int StaleLockTimeout = 20;

        /// <summary>
        /// The delay duration in minutes before executing deferred jobs.
        /// </summary>
        public const int DeferredExecutionInMinutes = 5;

        /// <summary>
        /// The queue name for notification jobs.
        /// </summary>
        public const string NotificationQueue = "notification-queue";

        /// <summary>
        /// The queue name for deferred execution jobs.
        /// </summary>
        public const string DeferredExecutionQueue = "deferred-execution-queue";

        /// <summary>
        /// The default queue name for general worker jobs.
        /// </summary>
        public const string DefaultQueue = "default";

        /// <summary>
        /// The job type name for Kavita notification jobs.
        /// </summary>
        public const string NotifyKavitaJob = nameof(NotifyKavitaJob);

        /// <summary>
        /// The directory name for worker temporary files.
        /// </summary>
        public const string TempDirName = "kamiyomu-worker.tmp";

        /// <summary>
        /// Recurring Job Id label used in Hangfire
        /// </summary>
        public const string RecurringJobId = nameof(RecurringJobId);

        /// <summary>
        /// Crawler Agent id used for identify what crawler agent is being executed
        /// </summary>
        public const string CrawlerAgentId = nameof(CrawlerAgentId);
        /// <summary>
        /// Library id used for identify what Library is being executed
        /// </summary>
        public const string LibraryId = nameof(LibraryId);
    }
    /// <summary>
    /// Contains string constants representing metadata field names for crawler agent configuration.
    /// These field names are used as keys when injecting dependencies or configuring crawler agent behavior.
    /// </summary>
    public static class CrawlerAgentMetadata
    {
        /// <summary>
        /// 
        /// </summary>
        public static class Fields
        {
            /// <summary>
            /// 
            /// </summary>
            public const string BrowserUserAgent = nameof(BrowserUserAgent);
            /// <summary>
            /// 
            /// </summary>
            public const string HttpClientTimeout = nameof(HttpClientTimeout);
            /// <summary>
            /// 
            /// </summary>
            public const string KamiYomuILogger = nameof(KamiYomuILogger);
            /// <summary>
            /// 
            /// </summary>
            public const string FlareSolverrUrl = nameof(FlareSolverrUrl);
            /// <summary>
            /// 
            /// </summary>
            public const string FlareSolverrHttpHandler = nameof(FlareSolverrHttpHandler);
            /// <summary>
            /// 
            /// </summary>
            public const string ChromiumHttpHandler = nameof(ChromiumHttpHandler);
            /// <summary>
            /// 
            /// </summary>
            public const string SmartCrawlerHttpHandler = nameof(SmartCrawlerHttpHandler);
        }
        /// <summary>
        /// 
        /// </summary>
        public static class Values
        {
            /// <summary>
            /// The user agent string for KamiYomu HTTP requests.
            /// </summary>
            public static string KamiYomuHttpUserAgent = $"KamiYomu-Agent/1.0 ({Environment.OSVersion.Platform}; {(Environment.Is64BitOperatingSystem ? "x64" : "x86")})";
            /// <summary>
            /// The user agent string to mimic a common browser for HTTP requests.
            /// </summary>
            public const string MimicUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36";
            /// <summary>
            /// The default timeout in milliseconds for HTTP requests.
            /// </summary>
            public static int TimeoutMilliseconds = 60_000;
        }
    }

    /// <summary>
    /// Contains LiteDB serialization configuration for custom types.
    /// Registers type mappers for Uri, DownloadStatus, NotificationType, and ReleaseStatus.
    /// </summary>
    public static class LiteDbConfig
    {
        /// <summary>
        /// Configures LiteDB's BsonMapper with custom type serialization and deserialization handlers.
        /// Enables proper handling of Uri objects and enum types for database persistence.
        /// </summary>
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

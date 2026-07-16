using Hangfire;
using Hangfire.Storage.SQLite;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Filters;
using KamiYomu.Web.Infrastructure.Storage;
using KamiYomu.Web.Worker;
using KamiYomu.Web.Worker.Interfaces;

using SQLite;

namespace KamiYomu.Web.Infrastructure.Hostings;

/// <summary>
/// WorkerJobsHostings provides extension methods for registering worker job implementations with the dependency injection container. It allows for the addition of various background jobs related to manga and chapter processing, as well as deferred execution coordination and notification handling.
/// </summary>
public static class WorkerJobsHostings
{
    /// <summary>
    /// Adds worker job implementations to the dependency injection container. This method registers various background jobs, including chapter discovery, chapter downloading, manga downloading, deferred execution coordination, and notification handling for Kavita. These jobs can be used for processing manga and chapters in the background.
    /// </summary>
    /// <param name="builder"></param>
    public static void AddWorkerJobsHostings(this WebApplicationBuilder builder)
    {
        AddHangfireConfig(builder);

        _ = builder.Services.AddTransient<IChapterDiscoveryJob, ChapterDiscoveryJob>();
        _ = builder.Services.AddTransient<IChapterDownloaderJob, ChapterDownloaderJob>();
        _ = builder.Services.AddTransient<IMangaDownloaderJob, MangaDownloaderJob>();
        _ = builder.Services.AddTransient<IDeferredExecutionCoordinator, DeferredExecutionCoordinator>();
        _ = builder.Services.AddTransient<INotifyKavitaJob, NotifyKavitaJob>();

    }


    private static void AddHangfireConfig(WebApplicationBuilder builder)
    {
        WorkerOptions? workerOptions = builder.Configuration.GetSection("Worker").Get<WorkerOptions>();
        IEnumerable<string> serverNames = workerOptions.ServerAvailableNames;


        _ = builder.Services.AddHangfire(configuration => configuration.UseSimpleAssemblyNameTypeSerializer()
                                                               .UseRecommendedSerializerSettings()
                                                               .UseSQLiteStorage(new SQLiteDbConnectionFactory(() =>
                                                               {
                                                                   SQLiteConnectionString connectionString = new(FileNameHelper.NormalizeSystemPath(builder.Configuration.GetConnectionString("WorkerDb")),
                                                                       SQLiteOpenFlags.Create
                                                                       | SQLiteOpenFlags.ReadWrite
                                                                       | SQLiteOpenFlags.PrivateCache
                                                                       | SQLiteOpenFlags.FullMutex, true);
                                                                   SQLiteConnection connection = new(connectionString);

                                                                   string journalMode = connection.ExecuteScalar<string>("PRAGMA journal_mode=WAL;");


                                                                   string busyTimeout = connection.ExecuteScalar<string>("PRAGMA busy_timeout=5000;");

                                                                   return connection;
                                                               }),
                                                               new SQLiteStorageOptions
                                                               {
                                                                   QueuePollInterval = TimeSpan.FromSeconds(15),
                                                                   DistributedLockLifetime = TimeSpan.FromMinutes(Defaults.Worker.StaleLockTimeout),
                                                                   JobExpirationCheckInterval = TimeSpan.FromHours(1),
                                                                   CountersAggregateInterval = TimeSpan.FromMinutes(5)
                                                               }));

        List<string> allQueues = [.. workerOptions.GetAllQueues()];
        List<List<string>> queuesPerServer = [.. allQueues
        .Select((queue, index) => new { queue, index })
        .GroupBy(x => x.index % serverNames.Count())
        .Select(g => g.Select(x => x.queue).ToList())];

        // Register each server separately
        foreach ((string serverName, List<string> queues) in serverNames.Zip(queuesPerServer))
        {
            _ = builder.Services.AddHangfireServer((services, options) =>
            {
                options.ServerName = serverName;
                options.WorkerCount = workerOptions.WorkerCount;
                options.Queues = [.. queues];
                options.HeartbeatInterval = TimeSpan.FromSeconds(15);
            });
        }

        GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute
        {
            Attempts = workerOptions.MaxRetryAttempts,
            OnAttemptsExceeded = AttemptsExceededAction.Delete,
            LogEvents = true
        });
    }

    /// <summary>
    /// Uses the Hangfire dashboard for worker jobs hosting. This method configures the Hangfire dashboard to be accessible at the "/worker" endpoint, with specific options such as hiding the storage connection string, setting the dashboard title, and allowing all users to access the dashboard without authentication. It also sets up a recurring job for deferred execution coordination.
    /// </summary>
    /// <param name="app"></param>
    public static void UseWorkerJobsHostings(this WebApplication app)
    {
        _ = app.UseHangfireDashboard("/worker", new DashboardOptions
        {
            DisplayStorageConnectionString = false,
            DashboardTitle = nameof(KamiYomu),
            FaviconPath = "/images/favicon.ico",
            IgnoreAntiforgeryToken = true,
            Authorization = [new AllowAllDashboardAuthorizationFilter()]
        });


        RecurringJob.AddOrUpdate<IDeferredExecutionCoordinator>(Defaults.Worker.DeferredExecutionQueue,
                                                                (job) => job.DispatchAsync(Defaults.Worker.DeferredExecutionQueue, null!, CancellationToken.None),
                                                                Cron.MinuteInterval(Defaults.Worker.DeferredExecutionInMinutes));

    }
}

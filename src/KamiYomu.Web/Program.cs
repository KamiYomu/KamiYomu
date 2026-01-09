using System.Globalization;
using System.Text.Json.Serialization;

using Hangfire;
using Hangfire.Storage.SQLite;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Endpoints;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Filters;
using KamiYomu.Web.HealthCheckers;
using KamiYomu.Web.Hubs;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Middlewares;
using KamiYomu.Web.Worker;
using KamiYomu.Web.Worker.Interfaces;

using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Options;

using MonkeyCache;
using MonkeyCache.LiteDB;

using Polly;
using Polly.Extensions.Http;

using QuestPDF.Fluent;

using Serilog;

using SQLite;

using static KamiYomu.Web.AppOptions.Defaults;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

if (!IsRunningInDocker())
{
    if (OperatingSystem.IsWindows())
    {
        _ = builder.Host.UseWindowsService();
    }
    else if (OperatingSystem.IsLinux())
    {
        _ = builder.Host.UseSystemd();
    }
}

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
QuestPDF.Infrastructure.TextStyle.Default.FontFamily("Lato");

LiteDbConfig.Configure();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog((context, services, configuration) =>
       configuration
           .ReadFrom.Configuration(context.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext()
   );

builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.Configure<BasicAuthOptions>(builder.Configuration.GetSection("BasicAuth"));
builder.Services.Configure<SpecialFolderOptions>(builder.Configuration.GetSection("SpecialFolders"));
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});


builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.AddSingleton<IUserClockManager, UserClockManager>();
builder.Services.AddSingleton<ILockManager, LockManager>();

builder.Services.AddScoped(_ => new DbContext(builder.Configuration.GetConnectionString("AgentDb")));
builder.Services.AddScoped<CacheContext>();
builder.Services.AddScoped(_ => new ImageDbContext(builder.Configuration.GetConnectionString("ImageDb")));

builder.Services.AddTransient<ICrawlerAgentRepository, CrawlerAgentRepository>();
builder.Services.AddTransient<IHangfireRepository, HangfireRepository>();
builder.Services.AddTransient<IChapterDiscoveryJob, ChapterDiscoveryJob>();
builder.Services.AddTransient<IChapterDownloaderJob, ChapterDownloaderJob>();
builder.Services.AddTransient<IMangaDownloaderJob, MangaDownloaderJob>();
builder.Services.AddTransient<IDeferredExecutionCoordinator, DeferredExecutionCoordinator>();
builder.Services.AddTransient<INugetService, NugetService>();
builder.Services.AddTransient<INotificationService, NotificationService>();
builder.Services.AddTransient<IWorkerService, WorkerService>();
builder.Services.AddTransient<IGitHubService, GitHubService>();
builder.Services.AddTransient<IStatsService, StatsService>();
builder.Services.AddTransient<IKavitaService, KavitaService>();

builder.Services.AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>(nameof(DatabaseHealthCheck), tags: ["storage"])
                .AddCheck<WorkerHealthCheck>(nameof(WorkerHealthCheck), tags: ["worker"])
                .AddCheck<CachingHealthCheck>(nameof(CachingHealthCheck), tags: ["storage"]);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    CultureInfo[] supportedCultures =
    [
            new CultureInfo("en-US"),
            new CultureInfo("pt-BR"),
            new CultureInfo("fr"),
            new CultureInfo("es"),
            new CultureInfo("nl")
    ];

    options.DefaultRequestCulture = new RequestCulture("en-US");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.FallBackToParentCultures = true;
    options.FallBackToParentUICultures = true;
});



builder.Services.AddRazorPages()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                })
                .AddViewLocalization()
                .AddDataAnnotationsLocalization();

AddHttpClients(builder);

AddHangfireConfig(builder);

WebApplication app = builder.Build();
ServiceLocator.Configure(() => app.Services);

if (!app.Environment.IsDevelopment())
{
    QuestPDF.Settings.EnableDebugging = true;
    _ = app.UseExceptionHandler("/Error");
}
using (IServiceScope appScoped = app.Services.CreateScope())
{
    SpecialFolderOptions specialFolderOptions = appScoped.ServiceProvider.GetRequiredService<IOptions<SpecialFolderOptions>>().Value;
    StartupOptions startupOptions = appScoped.ServiceProvider.GetRequiredService<IOptions<StartupOptions>>().Value;
    IOptions<RequestLocalizationOptions> localizationOptions = appScoped.ServiceProvider.GetRequiredService<IOptions<RequestLocalizationOptions>>();


    _ = Directory.CreateDirectory(specialFolderOptions.LogDir);
    _ = Directory.CreateDirectory(specialFolderOptions.DbDir);
    _ = Directory.CreateDirectory(specialFolderOptions.AgentsDir);
    _ = Directory.CreateDirectory(specialFolderOptions.MangaDir);

    Barrel.ApplicationId = nameof(KamiYomu);
    BarrelUtils.SetBaseCachePath(specialFolderOptions.DbDir);

    using DbContext dbcontext = appScoped.ServiceProvider.GetRequiredService<DbContext>();
    UserPreference userPreference = dbcontext.UserPreferences.FindOne(p => true);
    if (userPreference == null)
    {
        userPreference = new UserPreference(new CultureInfo(startupOptions.DefaultLanguage));
        _ = appScoped.ServiceProvider.GetService<DbContext>()!.UserPreferences.Insert(userPreference);
    }

    localizationOptions.Value.DefaultRequestCulture = new RequestCulture(userPreference!.GetCulture());

    _ = app.UseRequestLocalization(localizationOptions.Value);
}

app.UseResponseCompression();
app.UseStaticFiles();
app.UseRouting();
app.UseMiddleware<BasicAuthMiddleware>();
app.MapStatsEndpoints()
   .MapLibraryEndpoints();
app.UseHangfireDashboard("/worker", new DashboardOptions
{
    DisplayStorageConnectionString = false,
    DashboardTitle = nameof(KamiYomu),
    FaviconPath = "/images/favicon.ico",
    IgnoreAntiforgeryToken = true,
    Authorization = [new AllowAllDashboardAuthorizationFilter()]
});


RecurringJob.AddOrUpdate<IDeferredExecutionCoordinator>(Worker.DeferredExecutionQueue,
                                                        (job) => job.DispatchAsync(Worker.DeferredExecutionQueue, null!, CancellationToken.None),
                                                        Cron.MinuteInterval(Worker.DeferredExecutionInMinutes));

app.MapRazorPages();
app.UseMiddleware<ExceptionNotificationMiddleware>();
app.MapHub<NotificationHub>("/notificationHub");
app.MapHealthChecks("/healthz");
app.Run();

static void AddHangfireConfig(WebApplicationBuilder builder)
{
    WorkerOptions? workerOptions = builder.Configuration.GetSection("Worker").Get<WorkerOptions>();
    IEnumerable<string> serverNames = workerOptions.ServerAvailableNames;


    _ = builder.Services.AddHangfire(configuration => configuration.UseSimpleAssemblyNameTypeSerializer()
                                                           .UseRecommendedSerializerSettings()
                                                           .UseSQLiteStorage(new SQLiteDbConnectionFactory(() =>
                                                           {
                                                               SQLiteConnectionString connectionString = new(builder.Configuration.GetConnectionString("WorkerDb"),
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
                                                               DistributedLockLifetime = TimeSpan.FromMinutes(Worker.StaleLockTimeout),
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

static bool IsRunningInDocker()
{
    return File.Exists("/.dockerenv");
}

static void AddHttpClients(WebApplicationBuilder builder)
{
    Polly.Retry.AsyncRetryPolicy<HttpResponseMessage> retryPolicy = HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

    Polly.Timeout.AsyncTimeoutPolicy<HttpResponseMessage> timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(Worker.HttpTimeOutInSeconds);

    _ = builder.Services.AddHttpClient(Worker.HttpClientApp, client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(CrawlerAgentSettings.HttpUserAgent);
        })
        .AddPolicyHandler(retryPolicy)
        .AddPolicyHandler(timeoutPolicy);

    _ = builder.Services.AddTransient<KavitaAuthHandler>();

    _ = builder.Services.AddHttpClient(Integrations.HttpClientApp, client =>
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd(CrawlerAgentSettings.HttpUserAgent);
    })
        .AddHttpMessageHandler<KavitaAuthHandler>()
        .AddPolicyHandler(retryPolicy)
        .AddPolicyHandler(timeoutPolicy)
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            return new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
        });
}

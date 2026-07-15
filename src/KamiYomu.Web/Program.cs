using System.Globalization;
using System.Text.Json.Serialization;

using Hangfire;
using Hangfire.Storage.SQLite;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Areas.Public;
using KamiYomu.Web.Areas.Reader;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Filters;
using KamiYomu.Web.HealthCheckers;
using KamiYomu.Web.Hubs;
using KamiYomu.Web.Infrastructure.AppServices;
using KamiYomu.Web.Infrastructure.AppServices.Interfaces;
using KamiYomu.Web.Infrastructure.Browser;
using KamiYomu.Web.Infrastructure.Browser.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Infrastructure.Storage;
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

if (OperatingSystem.IsWindows())
{
    _ = builder.Configuration.AddJsonFile("appsettings.windows.json", optional: true, reloadOnChange: true);

    _ = builder.Services.AddSingleton<IChromiumBootstrapper, WindowsChromiumBootstrapper>();
}
else
{
    _ = builder.Services.AddSingleton<IChromiumBootstrapper, LinuxChromiumBootstrapper>();
}

builder.Services.Configure<StartupOptions>(builder.Configuration.GetSection("StartupOptions"));
builder.Services.Configure<BasicAuthOptions>(builder.Configuration.GetSection("BasicAuth"));
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.Configure<SpecialFolderOptions>(builder.Configuration.GetSection("SpecialFolders"));
builder.Services.Configure<ChromiumOptions>(builder.Configuration.GetSection("Chromium"));
builder.Services.PostConfigure<SpecialFolderOptions>(opts =>
{
    opts.MangaDir = FileNameHelper.NormalizeSystemPath(opts.MangaDir);
    opts.AgentsDir = FileNameHelper.NormalizeSystemPath(opts.AgentsDir);
    opts.DbDir = FileNameHelper.NormalizeSystemPath(opts.DbDir);
    opts.LogDir = FileNameHelper.NormalizeSystemPath(opts.LogDir);

    _ = Directory.CreateDirectory(opts.LogDir);
    _ = Directory.CreateDirectory(opts.DbDir);
    _ = Directory.CreateDirectory(opts.MangaDir);
    _ = Directory.CreateDirectory(opts.AgentsDir);
});


if (!FileNameHelper.IsRunningInDocker())
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

if (OperatingSystem.IsWindows())
{
    SpecialFolderOptions special = builder.Services.BuildServiceProvider().GetRequiredService<IOptions<SpecialFolderOptions>>().Value;
    builder.Configuration["Serilog:WriteTo:0:Args:path"] =
    Path.Combine(special.LogDir, "log-.txt");
}

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


builder.Services.AddSingleton<CacheContext>();
builder.Services.AddScoped(_ => new DbContext(FileNameHelper.NormalizeSystemPath(builder.Configuration.GetConnectionString("AgentDb")), false));
builder.Services.AddScoped(_ => new ImageDbContext(FileNameHelper.NormalizeSystemPath(builder.Configuration.GetConnectionString("ImageDb")), false));
builder.Services.AddKeyedScoped(ServiceLocator.ReadOnlyDbContext, (sp, _) => new DbContext(FileNameHelper.NormalizeSystemPath(builder.Configuration.GetConnectionString("AgentDb")), true));
builder.Services.AddKeyedScoped(ServiceLocator.ReadOnlyImageDbContext, (sp, _) => new ImageDbContext(FileNameHelper.NormalizeSystemPath(builder.Configuration.GetConnectionString("ImageDb")), true));


// Repositories
builder.Services.AddTransient<ICrawlerAgentRepository, CrawlerAgentRepository>();
builder.Services.AddTransient<IHangfireRepository, HangfireRepository>();

// Worker jobs
builder.Services.AddTransient<IChapterDiscoveryJob, ChapterDiscoveryJob>();
builder.Services.AddTransient<IChapterDownloaderJob, ChapterDownloaderJob>();
builder.Services.AddTransient<IMangaDownloaderJob, MangaDownloaderJob>();
builder.Services.AddTransient<IDeferredExecutionCoordinator, DeferredExecutionCoordinator>();
builder.Services.AddTransient<INotifyKavitaJob, NotifyKavitaJob>();

// Services
builder.Services.AddTransient<INugetService, NugetService>();
builder.Services.AddTransient<INotificationService, NotificationService>();
builder.Services.AddTransient<IWorkerService, WorkerService>();
builder.Services.AddTransient<IGitHubService, GitHubService>();
builder.Services.AddTransient<IStatsService, StatsService>();
builder.Services.AddTransient<IKavitaService, KavitaService>();
builder.Services.AddTransient<IGotifyService, GotifyService>();
builder.Services.AddTransient<IEpubService, EpubService>();
builder.Services.AddTransient<IPdfService, PdfService>();
builder.Services.AddTransient<IZipService, ZipService>();

// App Services
builder.Services.AddTransient<IDownloadAppService, DownloadAppService>();

// HeathCheckers
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

builder.Services.AddReaderArea(builder.Configuration)
                .AddPublicArea();

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

app.UsePublicArea();
app.MapControllers();
app.MapRazorPages();
app.UseMiddleware<ExceptionNotificationMiddleware>();
app.MapHub<NotificationHub>("/notificationHub");
app.MapHealthChecks("/healthz");
IChromiumBootstrapper chromium = app.Services.GetRequiredService<IChromiumBootstrapper>();
await chromium.InitializeAsync(CancellationToken.None);
app.Run();




static void AddHangfireConfig(WebApplicationBuilder builder)
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

    _ = builder.Services.AddHttpClient(Integrations.HttpClientApp, client =>
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd(CrawlerAgentSettings.HttpUserAgent);
    })
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

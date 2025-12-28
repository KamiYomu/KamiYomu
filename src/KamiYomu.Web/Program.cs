using Hangfire;
using Hangfire.Storage.SQLite;
using KamiYomu.Web.AppOptions;
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
using System.Globalization;
using System.Text.Json.Serialization;
using static KamiYomu.Web.AppOptions.Defaults;

var builder = WebApplication.CreateBuilder(args);

if (!IsRunningInDocker())
{
    if (OperatingSystem.IsWindows())
    {
        builder.Host.UseWindowsService();
    }
    else if (OperatingSystem.IsLinux())
    {
        builder.Host.UseSystemd();
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
builder.Services.Configure<Defaults.NugetFeeds>(builder.Configuration.GetSection("UI"));
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});


builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
});


builder.Services.AddSingleton<CacheContext>();
builder.Services.AddSingleton<ImageDbContext>(_ => new ImageDbContext(builder.Configuration.GetConnectionString("ImageDb")));
builder.Services.AddSingleton<IUserClockManager, UserClockManager>();
builder.Services.AddSingleton<ILockManager, LockManager>();
builder.Services.AddScoped<DbContext>(_ => new DbContext(builder.Configuration.GetConnectionString("AgentDb")));

builder.Services.AddTransient<ICrawlerAgentRepository, CrawlerAgentRepository>();
builder.Services.AddTransient<IHangfireRepository, HangfireRepository>();
builder.Services.AddTransient<IChapterDiscoveryJob, ChapterDiscoveryJob>();
builder.Services.AddTransient<IChapterDownloaderJob, ChapterDownloaderJob>();
builder.Services.AddTransient<IMangaDownloaderJob, MangaDownloaderJob>();
builder.Services.AddTransient<IDeferredExecutionCoordinator, DeferredExecutionCoordinator>();
builder.Services.AddTransient<INugetService, NugetService>();
builder.Services.AddTransient<INotificationService, NotificationService>();
builder.Services.AddTransient<IWorkerService, WorkerService>();

builder.Services.AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>(nameof(DatabaseHealthCheck), tags: ["storage"])
                .AddCheck<WorkerHealthCheck>(nameof(WorkerHealthCheck), tags: ["worker"])
                .AddCheck<CachingHealthCheck>(nameof(CachingHealthCheck), tags: ["storage"]);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
            new CultureInfo("en-US"),
            new CultureInfo("pt-BR"),
            new CultureInfo("fr"),
            new CultureInfo("es"),
            new CultureInfo("nl")
    };

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


var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(Worker.HttpTimeOutInSeconds);

builder.Services.AddHttpClient(Worker.HttpClientBackground, client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd(CrawlerAgentSettings.HttpUserAgent);
})
    .AddPolicyHandler(retryPolicy)
    .AddPolicyHandler(timeoutPolicy);

AddHangfireConfig(builder);


    var app = builder.Build();
Defaults.ServiceLocator.Configure(() => app.Services);

if (!app.Environment.IsDevelopment())
{
    QuestPDF.Settings.EnableDebugging = true;
    app.UseExceptionHandler("/Error");
}
using (var appScoped = app.Services.CreateScope())
{
    var specialFolderOptions = appScoped.ServiceProvider.GetRequiredService<IOptions<SpecialFolderOptions>>().Value;
    var startupOptions = appScoped.ServiceProvider.GetRequiredService<IOptions<StartupOptions>>().Value;
    var localizationOptions = appScoped.ServiceProvider.GetRequiredService<IOptions<RequestLocalizationOptions>>();


    Directory.CreateDirectory(specialFolderOptions.LogDir);
    Directory.CreateDirectory(specialFolderOptions.DbDir);
    Directory.CreateDirectory(specialFolderOptions.AgentsDir);
    Directory.CreateDirectory(specialFolderOptions.MangaDir);

    Barrel.ApplicationId = nameof(KamiYomu);
    BarrelUtils.SetBaseCachePath(specialFolderOptions.DbDir);

    using var dbcontext = appScoped.ServiceProvider.GetRequiredService<DbContext>();
    var userPreference = dbcontext.UserPreferences.FindOne(p => true);
    if (userPreference == null)
    {
        userPreference = new UserPreference(new CultureInfo(startupOptions.DefaultLanguage));
        appScoped.ServiceProvider.GetService<DbContext>()!.UserPreferences.Insert(userPreference);
    }

    localizationOptions.Value.DefaultRequestCulture = new RequestCulture(userPreference!.GetCulture());

    app.UseRequestLocalization(localizationOptions.Value);
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
                                                        Cron.MinuteInterval(Defaults.Worker.DeferredExecutionInMinutes));

app.MapRazorPages();
app.UseMiddleware<ExceptionNotificationMiddleware>();
app.MapHub<NotificationHub>("/notificationHub");
app.MapHealthChecks("/healthz");
app.Run();

static void AddHangfireConfig(WebApplicationBuilder builder)
{
    var workerOptions = builder.Configuration.GetSection("Worker").Get<WorkerOptions>();
    var serverNames = workerOptions.ServerAvailableNames;
 

    builder.Services.AddHangfire(configuration => configuration.UseSimpleAssemblyNameTypeSerializer()
                                                           .UseRecommendedSerializerSettings()
                                                           .UseSQLiteStorage(new SQLiteDbConnectionFactory(() =>
                                                           {
                                                               var connectionString = new SQLiteConnectionString(builder.Configuration.GetConnectionString("WorkerDb"),
                                                                   SQLiteOpenFlags.Create
                                                                   | SQLiteOpenFlags.ReadWrite
                                                                   | SQLiteOpenFlags.PrivateCache
                                                                   | SQLiteOpenFlags.FullMutex, true);
                                                               var connection = new SQLiteConnection(connectionString);

                                                               var journalMode = connection.ExecuteScalar<string>("PRAGMA journal_mode=WAL;");


                                                               var busyTimeout = connection.ExecuteScalar<string>("PRAGMA busy_timeout=5000;");

                                                               return connection;
                                                           }),
                                                           new SQLiteStorageOptions
                                                           {
                                                               QueuePollInterval = TimeSpan.FromSeconds(15),
                                                               DistributedLockLifetime = TimeSpan.FromMinutes(Defaults.Worker.StaleLockTimeout),
                                                               JobExpirationCheckInterval = TimeSpan.FromHours(1),
                                                               CountersAggregateInterval = TimeSpan.FromMinutes(5)
                                                           }));

    var allQueues = workerOptions.GetAllQueues().ToList();
    var queuesPerServer = allQueues
        .Select((queue, index) => new { queue, index })
        .GroupBy(x => x.index % serverNames.Count())
        .Select(g => g.Select(x => x.queue).ToList())
        .ToList();

    // Register each server separately
    foreach (var (serverName, queues) in serverNames.Zip(queuesPerServer))
    {
        builder.Services.AddHangfireServer((services, options) =>
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
    return System.IO.File.Exists("/.dockerenv");
}

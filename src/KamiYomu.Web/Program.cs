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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MonkeyCache;
using MonkeyCache.LiteDB;
using Polly;
using Polly.Extensions.Http;
using QuestPDF.Infrastructure;
using Serilog;
using SQLite;
using System.Globalization;
using System.Text.Json.Serialization;
using static KamiYomu.Web.AppOptions.Defaults;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;
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

Barrel.ApplicationId = nameof(KamiYomu);
BarrelUtils.SetBaseCachePath(Defaults.SpecialFolders.DbDir);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.Configure<Defaults.NugetFeeds>(builder.Configuration.GetSection("UI"));

builder.Services.AddSingleton<CacheContext>();
builder.Services.AddSingleton<ImageDbContext>(_ => new ImageDbContext(builder.Configuration.GetConnectionString("ImageDb")));
builder.Services.AddScoped<DbContext>(_ => new DbContext(builder.Configuration.GetConnectionString("AgentDb")));
builder.Services.AddHangfire(configuration => configuration.UseSimpleAssemblyNameTypeSerializer()
                                                           .UseRecommendedSerializerSettings()
                                                           .UseSQLiteStorage(new SQLiteDbConnectionFactory(() =>
                                                           {
                                                               var connectionString = new SQLiteConnectionString(builder.Configuration.GetConnectionString("WorkerDb"), SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.SharedCache, true);
                                                               return new SQLiteConnection(connectionString);
                                                           })));



builder.Services.AddTransient<ICrawlerAgentRepository, CrawlerAgentRepository>();
builder.Services.AddTransient<IHangfireRepository, HangfireRepository>();
builder.Services.AddTransient<IChapterDiscoveryJob, ChapterDiscoveryJob>();
builder.Services.AddTransient<IChapterDownloaderJob, ChapterDownloaderJob>();
builder.Services.AddTransient<IMangaDownloaderJob, MangaDownloaderJob>();
builder.Services.AddTransient<INugetService, NugetService>();
builder.Services.AddTransient<INotificationService, NotificationService>();

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
            new CultureInfo("fr")
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

var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(10); // 10 seconds

builder.Services.AddHttpClient(Defaults.Worker.HttpClientBackground, client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd(CrawlerAgentSettings.HttpUserAgent);
})
    .AddPolicyHandler(retryPolicy)
    .AddPolicyHandler(timeoutPolicy);


var workerOptions = builder.Configuration.GetSection("Worker").Get<WorkerOptions>();
var serverNames = workerOptions.ServerAvailableNames;
var allQueues = workerOptions.GetAllQueues().ToList();

// Divide queues evenly among servers
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
        options.HeartbeatInterval = TimeSpan.FromSeconds(30);
    });
}

var app = builder.Build();
Defaults.ServiceLocator.Configure(() => app.Services);

if (!app.Environment.IsDevelopment())
{
    QuestPDF.Settings.EnableDebugging = true;
    app.UseExceptionHandler("/Error");
}
using (var appScoped = app.Services.CreateScope())
{
    var startupOptions = appScoped.ServiceProvider.GetRequiredService<IOptions<StartupOptions>>().Value;
    var localizationOptions = appScoped.ServiceProvider.GetRequiredService<IOptions<RequestLocalizationOptions>>();
    var dbcontext = appScoped.ServiceProvider.GetRequiredService<DbContext>();

    var userPreference = dbcontext.UserPreferences.FindOne(p => true);
    if (userPreference == null)
    {
        userPreference = new UserPreference(new CultureInfo(startupOptions.DefaultLanguage));
        appScoped.ServiceProvider.GetService<DbContext>()!.UserPreferences.Insert(userPreference);
    }

    localizationOptions.Value.DefaultRequestCulture = new RequestCulture(userPreference!.GetCulture());

    app.UseRequestLocalization(localizationOptions.Value);
}

app.UseStaticFiles();
app.UseRouting();
app.UseHangfireDashboard("/worker", new DashboardOptions
{
    DisplayStorageConnectionString = false,
    DashboardTitle = "KamiYomu",
    FaviconPath = "/images/favicon.ico",
    IgnoreAntiforgeryToken = true,
    Authorization = [new AllowAllDashboardAuthorizationFilter()]
});

app.MapRazorPages();
app.UseMiddleware<ExceptionNotificationMiddleware>();
app.MapHub<NotificationHub>("/notificationHub");
app.MapHealthChecks("/healthz");
app.Run();

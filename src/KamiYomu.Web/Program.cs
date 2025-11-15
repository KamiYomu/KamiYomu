using Hangfire;
using Hangfire.Storage.SQLite;
using KamiYomu.Web;
using KamiYomu.Web.AppOptions;
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
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using MonkeyCache;
using MonkeyCache.LiteDB;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using SQLite;
using System.Globalization;
using System.Text.Json.Serialization;
using KamiYomu.Web.AppOptions;
using static KamiYomu.Web.AppOptions.Defaults;
using KamiYomu.Web.Entities;

var builder = WebApplication.CreateBuilder(args);


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
builder.Services.AddSignalR();
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Settings:Worker"));
builder.Services.Configure<Defaults.NugetFeeds>(builder.Configuration.GetSection("Settings:UI"));

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

builder.Services.AddHangfireServer((services, optionActions) =>
{
    var workerOptions = services.GetService<IOptions<WorkerOptions>>();
    optionActions.ServerName = nameof(Defaults.Worker.DownloadChapterQueues);
    optionActions.WorkerCount = Environment.ProcessorCount * workerOptions.Value.WorkerCount;
    optionActions.Queues = Defaults.Worker.DownloadChapterQueues;
});

builder.Services.AddHangfireServer((services, optionActions) =>
{
    var workerOptions = services.GetService<IOptions<WorkerOptions>>();
    optionActions.ServerName = nameof(Defaults.Worker.DiscoveryNewChapterQueues);
    optionActions.WorkerCount = Environment.ProcessorCount * workerOptions.Value.WorkerCount;
    optionActions.Queues = [Defaults.Worker.DiscoveryNewChapterQueues];
});

builder.Services.AddHangfireServer((services, optionActions) =>
{
    var workerOptions = services.GetService<IOptions<WorkerOptions>>();
    optionActions.ServerName = nameof(Defaults.Worker.MangaDownloadSchedulerQueues);
    optionActions.WorkerCount = Environment.ProcessorCount * workerOptions.Value.WorkerCount;
    optionActions.Queues = Defaults.Worker.MangaDownloadSchedulerQueues;
});

builder.Services.AddTransient<IAgentCrawlerRepository, AgentCrawlerRepository>();
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

builder.Services.AddRazorPages().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
}).AddViewLocalization();

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


var app = builder.Build();


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();

using(var appScoped = app.Services.CreateScope())
{
    var supportedCultures = new[] { "en-US", "pt-BR", "fr" };
    var dbcontext = appScoped.ServiceProvider.GetRequiredService<DbContext>();
    var userPreference = dbcontext.UserPreferences.FindOne(p => true);
    var startupOptions = appScoped.ServiceProvider.GetRequiredService<IOptions<StartupOptions>>().Value;

    if (userPreference == null)
    {
        userPreference = new UserPreference(new CultureInfo(startupOptions.DefaultLanguage));
        appScoped.ServiceProvider.GetService<DbContext>()!.UserPreferences.Insert(userPreference);
    }

    var localizationOptions = new RequestLocalizationOptions
    {
        DefaultRequestCulture = new RequestCulture(userPreference!.GetCulture()),
        SupportedCultures = [.. supportedCultures.Select(c => new CultureInfo(c))],
        SupportedUICultures = [.. supportedCultures.Select(c => new CultureInfo(c))],
        FallBackToParentCultures = true,
        FallBackToParentUICultures = true
    };
    app.UseRequestLocalization(localizationOptions);
}



app.UseHangfireDashboard("/worker", new DashboardOptions
{
    DisplayStorageConnectionString = false,
    DashboardTitle = I18n.BackgroundJobs,
    FaviconPath = "/images/favicon.ico",
    IgnoreAntiforgeryToken = true,
    Authorization = [new AllowAllDashboardAuthorizationFilter()]
});

var hangfireRepository = app.Services.GetService<IHangfireRepository>();

ServiceLocator.Configure(() => app.Services);
app.MapRazorPages();
app.UseMiddleware<ExceptionNotificationMiddleware>();
app.MapHub<NotificationHub>("/notificationHub");
app.MapHealthChecks("/healthz");
app.Run();

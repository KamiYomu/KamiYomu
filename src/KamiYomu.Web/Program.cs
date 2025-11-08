using Hangfire;
using Hangfire.Storage.SQLite;
using KamiYomu.Web;
using KamiYomu.Web.Filters;
using KamiYomu.Web.HealthCheckers;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
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
BarrelUtils.SetBaseCachePath(Settings.SpecialFolders.DbDir);

builder.Services.Configure<Settings.Worker>(builder.Configuration.GetSection("Settings:Worker"));
builder.Services.Configure<Settings.UI>(builder.Configuration.GetSection("Settings:UI"));

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
    var workerOptions = services.GetService<IOptions<Settings.Worker>>();
    optionActions.ServerName = nameof(Settings.Worker.CrawlerQueues);
    optionActions.WorkerCount = Environment.ProcessorCount * workerOptions.Value.WorkerCount;
    optionActions.Queues = Settings.Worker.CrawlerQueues;
});

builder.Services.AddHangfireServer((services, optionActions) =>
{
    var workerOptions = services.GetService<IOptions<Settings.Worker>>();
    optionActions.ServerName = nameof(Settings.Worker.SearchQueues);
    optionActions.WorkerCount = Environment.ProcessorCount * workerOptions.Value.WorkerCount;
    optionActions.Queues = Settings.Worker.SearchQueues;
});

builder.Services.AddHangfireServer((services, optionActions) =>
{
    var workerOptions = services.GetService<IOptions<Settings.Worker>>();
    optionActions.ServerName = nameof(Settings.Worker.FetchNewChaptersQueues);
    optionActions.WorkerCount = Environment.ProcessorCount * workerOptions.Value.WorkerCount;
    optionActions.Queues = Settings.Worker.FetchNewChaptersQueues;
});

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "en-US", "pt-BR", "fr" }.Select(c => new CultureInfo(c)).ToList();

    options.DefaultRequestCulture = new RequestCulture("en-US");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.FallBackToParentCultures = true;
    options.FallBackToParentUICultures = true;
    options.ApplyCurrentCultureToResponseHeaders = true;
});


builder.Services.AddTransient<IAgentCrawlerRepository, AgentCrawlerRepository>();
builder.Services.AddTransient<IHangfireRepository, HangfireRepository>();
builder.Services.AddTransient<IChapterDownloaderJob, ChapterDownloaderJob>();
builder.Services.AddTransient<IMangaDownloaderJob, MangaDownloaderJob>();

builder.Services.AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>(nameof(DatabaseHealthCheck), tags: ["storage"])
                .AddCheck<WorkerHealthCheck>(nameof(WorkerHealthCheck), tags: ["worker"])
                .AddCheck<CachingHealthCheck>(nameof(CachingHealthCheck), tags: ["storage"]);

builder.Services.AddRazorPages()
                .AddViewLocalization();

var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(10); // 10 seconds

builder.Services.AddHttpClient(Settings.Worker.HttpClientBackground, client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd(CrawlerAgentSettings.HttpUserAgent);
})
    .AddPolicyHandler(retryPolicy)
    .AddPolicyHandler(timeoutPolicy);


var app = builder.Build();

var uiSettings = app.Services.GetService<IOptions<Settings.UI>>();
var localizer = app.Services.GetRequiredService<IStringLocalizer<I18n>>();

app.UseRequestLocalization(options =>
{
    options.DefaultRequestCulture = new RequestCulture(uiSettings.Value.DefaultLanguage);
});


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

app.UseRouting();

app.UseHangfireDashboard("/worker", new DashboardOptions
{
    DisplayStorageConnectionString = false,
    DashboardTitle = I18n.BackgroundJobs,
    FaviconPath = "/images/favicon.ico",
    IgnoreAntiforgeryToken = true,
    Authorization = [new AllowAllDashboardAuthorizationFilter()]
});

RecurringJob.AddOrUpdate<IChapterDiscoveryJob>(
    nameof(ChapterDiscoveryJob),
    job => job.DispatchAsync(null, CancellationToken.None),
    app.Environment.IsDevelopment() ? Cron.Minutely() : Cron.Hourly());

app.MapRazorPages();
app.UseMiddleware<ExceptionNotificationMiddleware>();
app.MapHealthChecks("/healthz");
app.Run();

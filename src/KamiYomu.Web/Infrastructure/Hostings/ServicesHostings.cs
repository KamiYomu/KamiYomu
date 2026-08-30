using KamiYomu.Web.Entities.CrawlerAgentRuntime;
using KamiYomu.Web.Entities.CrawlerAgentRuntime.Interfaces;
using KamiYomu.Web.Infrastructure.AppServices;
using KamiYomu.Web.Infrastructure.AppServices.Interfaces;
using KamiYomu.Web.Infrastructure.Services;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

namespace KamiYomu.Web.Infrastructure.Hostings;

/// <summary>
/// Provides extension methods for registering service implementations with the dependency injection container.
/// </summary>
public static class ServicesHostings
{
    /// <summary>
    /// Registers application service implementations with the dependency injection container.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder to add the service registrations to.</param>
    public static void AddServiceHostings(this WebApplicationBuilder builder)
    {
        _ = builder.Services.AddTransient<INugetService, NugetService>();
        _ = builder.Services.AddTransient<INotificationService, NotificationService>();
        _ = builder.Services.AddTransient<IWorkerService, WorkerService>();
        _ = builder.Services.AddTransient<IGitHubService, GitHubService>();
        _ = builder.Services.AddTransient<IStatsService, StatsService>();
        _ = builder.Services.AddTransient<IKavitaService, KavitaService>();
        _ = builder.Services.AddTransient<IGotifyService, GotifyService>();
        _ = builder.Services.AddTransient<IEpubService, EpubService>();
        _ = builder.Services.AddTransient<IPdfService, PdfService>();
        _ = builder.Services.AddTransient<IZipService, ZipService>();

        AddAppServices(builder);
        AddCrawlerAgentRuntimeServices(builder);
    }
    private static void AddCrawlerAgentRuntimeServices(WebApplicationBuilder builder)
    {
        _ = builder.Services.AddTransient<ICrawlerAgentAssemblyLoader, CrawlerAgentAssemblyLoader>();
        _ = builder.Services.AddTransient<ICrawlerAgentFactory, CrawlerAgentFactory>();


    }

    private static void AddAppServices(WebApplicationBuilder builder)
    {
        _ = builder.Services.AddTransient<IDownloadAppService, DownloadAppService>();
        _ = builder.Services.AddTransient<ICrawlerAgentAppService, CrawlerAgentAppService>();
    }


}

using KamiYomu.Web.Infrastructure.AppServices;
using KamiYomu.Web.Infrastructure.AppServices.Interfaces;
using KamiYomu.Web.Infrastructure.Browser;
using KamiYomu.Web.Infrastructure.Browser.Interfaces;
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

        if (OperatingSystem.IsLinux())
        {
            AddLinuxServices(builder);
        }
        else if (OperatingSystem.IsWindows())
        {
            AddWindowsServices(builder);
        }
    }

    private static void AddAppServices(WebApplicationBuilder builder)
    {
        _ = builder.Services.AddTransient<IDownloadAppService, DownloadAppService>();
    }

    private static void AddWindowsServices(WebApplicationBuilder builder)
    {
        _ = builder.Services.AddTransient<IChromiumBootstrapper, WindowsChromiumBootstrapper>();
    }

    private static void AddLinuxServices(WebApplicationBuilder builder)
    {
        _ = builder.Services.AddTransient<IChromiumBootstrapper, LinuxChromiumBootstrapper>();
    }

}

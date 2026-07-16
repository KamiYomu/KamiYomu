using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Browser;
using KamiYomu.Web.Infrastructure.Browser.Interfaces;
using KamiYomu.Web.Infrastructure.Storage;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Infrastructure.Hostings;

/// <summary>
/// WindowsHostings provides extension methods for configuring Windows-specific hosting settings in the WebApplicationBuilder. It checks if the application is running on a Windows operating system and applies configurations such as using Windows Service hosting and registering the WindowsChromiumBootstrapper service for dependency injection. Additionally, it loads Windows-specific configuration files and sets up logging paths based on special folder options.
/// </summary>
public static class WindowsHostings
{
    /// <summary>
    /// Add Windows-specific hosting configurations to the WebApplicationBuilder. This method checks if the application is running in a Docker container or on a non-Windows operating system, and if so, it skips the Windows-specific configurations. Otherwise, it loads the "appsettings.windows.json" configuration file, sets up logging paths based on special folder options, enables Windows Service hosting, and registers the WindowsChromiumBootstrapper service for dependency injection.
    /// </summary>
    /// <param name="builder"></param>
    public static void AddWindowsHostings(this WebApplicationBuilder builder)
    {
        if (FileNameHelper.IsRunningInDocker())
        {
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        _ = builder.Configuration.AddJsonFile("appsettings.windows.json", optional: true, reloadOnChange: true);

        SpecialFolderOptions special = builder.Services.BuildServiceProvider().GetRequiredService<IOptions<SpecialFolderOptions>>().Value;
        builder.Configuration["Serilog:WriteTo:0:Args:path"] = Path.Combine(special.LogDir, "log-.txt");

        _ = builder.Host.UseWindowsService();

        _ = builder.Services.AddTransient<IChromiumBootstrapper, WindowsChromiumBootstrapper>();
    }

    public static async Task UseWindowsHostingsAsync(this WebApplication app)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        if (FileNameHelper.IsRunningInDocker())
        {
            return;
        }
        IChromiumBootstrapper chromium = app.Services.GetRequiredService<IChromiumBootstrapper>();
        await chromium.InitializeAsync(CancellationToken.None);
    }
}

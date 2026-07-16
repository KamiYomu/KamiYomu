using System.Runtime.CompilerServices;

using KamiYomu.Web.Infrastructure.Browser;
using KamiYomu.Web.Infrastructure.Browser.Interfaces;
using KamiYomu.Web.Infrastructure.Storage;

namespace KamiYomu.Web.Infrastructure.Hostings;

/// <summary>
/// LinuxHostings provides extension methods for configuring Linux-specific hosting settings in the WebApplicationBuilder. It checks if the application is running on a Linux operating system and applies configurations such as using systemd (if not running in Docker) and registering the LinuxChromiumBootstrapper service for dependency injection.
/// </summary>
public static class LinuxHostings
{
    /// <summary>
    /// Adds Linux-specific hosting configurations to the WebApplicationBuilder. This method checks if the application is running on a Linux operating system and, if so, configures the host to use systemd (unless running in Docker) and registers the LinuxChromiumBootstrapper service for dependency injection.
    /// </summary>
    /// <param name="builder"></param>
    public static void AddLinuxHostings(this WebApplicationBuilder builder)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        if (!FileNameHelper.IsRunningInDocker())
        {
            _ = builder.Host.UseSystemd();
        }

        _ = builder.Services.AddTransient<IChromiumBootstrapper, LinuxChromiumBootstrapper>();
    }

    public static async Task UseLinuxHostingsAsync(this WebApplication app)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        if (!FileNameHelper.IsRunningInDocker())
        {
            IChromiumBootstrapper chromium = app.Services.GetRequiredService<IChromiumBootstrapper>();
            await chromium.InitializeAsync(CancellationToken.None);
        }
    }
}

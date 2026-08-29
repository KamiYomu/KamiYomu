using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Browser;
using KamiYomu.Web.Infrastructure.Browser.Interfaces;
using KamiYomu.Web.Infrastructure.Storage;

using Microsoft.Extensions.Options;

using Serilog;

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
        if (FileNameHelper.IsRunningInDocker() || !OperatingSystem.IsLinux())
        {
            return;
        }

        _ = builder.Host.UseSystemd();

        _ = builder.Configuration.AddJsonFile("appsettings.Linux.json", optional: true, reloadOnChange: true);

        SpecialFolderOptions special = builder.Services.BuildServiceProvider().GetRequiredService<IOptions<SpecialFolderOptions>>().Value;
        builder.Configuration["Serilog:WriteTo:0:Args:path"] = Path.Combine(special.LogDir, "log-.txt");

        Log.Logger = new LoggerConfiguration()
                      .ReadFrom.Configuration(builder.Configuration)
                      .CreateLogger();

        _ = builder.Host.UseSerilog((context, services, configuration) =>
               configuration
                   .ReadFrom.Configuration(context.Configuration)
                   .ReadFrom.Services(services)
                   .Enrich.FromLogContext()
           );

        _ = builder.Services.AddTransient<IChromiumBootstrapper, LinuxChromiumBootstrapper>();

        Log.Logger.Information("Linux hostings configured successfully.");
        Log.Logger.Information("LogDir: {LogDir}", special.LogDir);
        Log.Logger.Information("MangaDir: {MangaDir}", special.MangaDir);
        Log.Logger.Information("AgentsDir: {AgentsDir}", special.AgentsDir);
        Log.Logger.Information("DbDir: {DbDir}", special.DbDir);
    }

    /// <summary>
    /// uses Linux-specific hosting configurations in the WebApplication. This method checks if the application is running on a Linux operating system and, if so, initializes the LinuxChromiumBootstrapper service (unless running in Docker).
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static async Task UseLinuxHostingsAsync(this WebApplication app)
    {
        if (FileNameHelper.IsRunningInDocker() || !OperatingSystem.IsLinux())
        {
            return;
        }

        IChromiumBootstrapper chromium = app.Services.GetRequiredService<IChromiumBootstrapper>();
        await chromium.InitializeAsync(CancellationToken.None);

    }
}

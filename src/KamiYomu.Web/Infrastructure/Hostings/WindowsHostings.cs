using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Browser;
using KamiYomu.Web.Infrastructure.Browser.Interfaces;
using KamiYomu.Web.Infrastructure.Storage;

using Microsoft.Extensions.Options;

using Serilog;

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
        if (FileNameHelper.IsRunningInDocker() || !OperatingSystem.IsWindows())
        {
            return;
        }
        _ = builder.Host.UseWindowsService();

        _ = builder.Configuration.AddJsonFile("appsettings.Windows.json", optional: true, reloadOnChange: true);

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

        _ = builder.Services.AddTransient<IChromiumBootstrapper, WindowsChromiumBootstrapper>();

        Log.Logger.Information("Windows hostings configured successfully.");
        Log.Logger.Information("LogDir: {LogDir}", special.LogDir);
        Log.Logger.Information("MangaDir: {MangaDir}", special.MangaDir);
        Log.Logger.Information("AgentsDir: {AgentsDir}", special.AgentsDir);
        Log.Logger.Information("DbDir: {DbDir}", special.DbDir);

    }

    /// <summary>
    /// use Windows-specific hosting configurations in the WebApplication. This method checks if the application is running on a Windows operating system and not in a Docker container. If both conditions are met, it retrieves the IChromiumBootstrapper service from the dependency injection container and initializes it asynchronously.
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static async Task UseWindowsHostingsAsync(this WebApplication app)
    {
        if (!OperatingSystem.IsWindows() || FileNameHelper.IsRunningInDocker())
        {
            return;
        }

        IChromiumBootstrapper chromium = app.Services.GetRequiredService<IChromiumBootstrapper>();
        await chromium.InitializeAsync(CancellationToken.None);
    }
}

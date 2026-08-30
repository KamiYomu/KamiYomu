using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Storage;

using Microsoft.Extensions.Options;

using PuppeteerSharp;
using PuppeteerSharp.BrowserData;

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

        DownloadChromium();

        Log.Logger.Information("Windows hostings configured successfully.");
        Log.Logger.Information("LogDir: {LogDir}", special.LogDir);
        Log.Logger.Information("MangaDir: {MangaDir}", special.MangaDir);
        Log.Logger.Information("AgentsDir: {AgentsDir}", special.AgentsDir);
        Log.Logger.Information("DbDir: {DbDir}", special.DbDir);


    }

    private static void DownloadChromium()
    {
        Log.Logger.Debug("Chromium: Ensure Chromium is downloaded.");
        BrowserFetcher fetcher = new(
            browser: SupportedBrowser.Chromium
            );

        if (fetcher.GetInstalledBrowsers().Any())
        {
            Log.Logger.Debug("Chromium: executable already exists. Skipping download.");
        }
        else
        {
            Log.Logger.Debug("Chromium: executable not found. Downloading...");
            _ = fetcher.DownloadAsync(BrowserTag.Stable).GetAwaiter().GetResult();
        }

        InstalledBrowser chromium = fetcher.GetInstalledBrowsers().First();
        string executable = chromium.GetExecutablePath();

        Environment.SetEnvironmentVariable("PUPPETEER_SKIP_CHROMIUM_DOWNLOAD", "true");
        Environment.SetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH", executable);
        Environment.SetEnvironmentVariable("XDG_CACHE_HOME", fetcher.CacheDir);
    }
}

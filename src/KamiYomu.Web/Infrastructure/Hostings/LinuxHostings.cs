using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Storage;

using Microsoft.Extensions.Options;

using PuppeteerSharp;
using PuppeteerSharp.BrowserData;

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

        Log.Logger.Debug("Chromium: Ensure Chromium is downloaded.");
        BrowserFetcher fetcher = new();

        if (fetcher.GetInstalledBrowsers().Any())
        {
            Log.Logger.Debug("Chromium: executable already exists. Skipping download.");
        }
        else
        {
            Log.Logger.Debug("Chromium: executable not found. Downloading...");
            _ = fetcher.DownloadAsync(BrowserTag.Stable).GetAwaiter().GetResult();
        }

        DownloadChromium();

        Log.Logger.Information("Linux hostings configured successfully.");
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

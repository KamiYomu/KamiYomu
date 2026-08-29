using System.IO.Compression;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Browser.Interfaces;
using KamiYomu.Web.Infrastructure.Storage;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Infrastructure.Browser;
/// <summary>
/// Chronium bootstrapper for Windows. Downloads and sets up Chromium for Puppeteer usage on Windows systems.
/// </summary>
/// <param name="options"></param>
/// <param name="logger"></param>
public class WindowsChromiumBootstrapper(
    IOptions<ChromiumOptions> options,
    ILogger<WindowsChromiumBootstrapper> logger) : IChromiumBootstrapper
{
    private readonly ChromiumOptions _options = options.Value;

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (FileNameHelper.IsRunningInDocker())
            {
                logger.LogInformation("Running in Docker. Skipping Chromium bootstrap for Windows.");
                return;
            }

            if (!OperatingSystem.IsWindows())
            {
                logger.LogInformation("Not running on Windows. Skipping Chromium bootstrap for Windows.");
                return;
            }

            _ = Directory.CreateDirectory(options.Value.GetBaseDirectory());

            // ✔ If Chromium already exists, skip download
            if (options.Value.IsExecutableExists())
            {
                SetEnvironmentVariables();
                logger.LogInformation("Chromium already installed at {Path}", options.Value.GetExecutablePath());
                return;
            }

            logger.LogInformation("Chromium not found. Installing into {Dir}", options.Value.GetBaseDirectory());

            string zipPath = Path.Combine(options.Value.GetBaseDirectory(), "chromium.zip");

            using HttpClient client = new();
            logger.LogInformation("Downloading Chromium from {Url}", _options.DownloadUrl);

            byte[] data = await client.GetByteArrayAsync(_options.DownloadUrl, cancellationToken);
            await File.WriteAllBytesAsync(zipPath, data, cancellationToken);

            logger.LogInformation("Extracting Chromium archive...");
            ZipFile.ExtractToDirectory(zipPath, _options.GetBaseDirectory(), true);
            File.Delete(zipPath);

            SetEnvironmentVariables();

            logger.LogInformation("Chromium installation completed. Executable at {Path}", _options.GetExecutablePath());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize Chromium");
            throw;
        }
    }

    private void SetEnvironmentVariables()
    {
        string configPath = Path.Combine(_options.GetWindowsDirectory(), ".config");
        string cachePath = Path.Combine(_options.GetWindowsDirectory(), ".cache");

        Environment.SetEnvironmentVariable("PUPPETEER_SKIP_CHROMIUM_DOWNLOAD", "true");
        Environment.SetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH", _options.GetExecutablePath());

        // Linux-specific Puppeteer/XDG requirements
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", configPath);
        Environment.SetEnvironmentVariable("XDG_CACHE_HOME", cachePath);

        logger.LogInformation(@"
                                Environment variables set for Chromium: 
                                PUPPETEER_EXECUTABLE_PATH={ExecutablePath}, 
                                XDG_CONFIG_HOME={ConfigPath}, 
                                XDG_CACHE_HOME={CachePath}",
                                options.Value.GetExecutablePath(),
                                configPath, cachePath);
    }
}

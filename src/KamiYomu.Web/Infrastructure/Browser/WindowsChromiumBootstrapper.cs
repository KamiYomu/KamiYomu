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
            string executablePath = Path.Combine(options.Value.GetBaseDirectory(), "chrome-win", _options.ExecutableName);

            if (File.Exists(executablePath))
            {
                SetEnvironmentVariables(Path.Combine(options.Value.GetBaseDirectory(), "chrome-win"), executablePath);
                logger.LogInformation("Chromium already installed at {Path}", executablePath);
                return;
            }

            logger.LogInformation("Chromium not found. Installing into {Dir}", options.Value.GetBaseDirectory());

            string zipPath = Path.Combine(options.Value.GetBaseDirectory(), "chromium.zip");

            using HttpClient client = new();
            logger.LogInformation("Downloading Chromium from {Url}", _options.DownloadUrl);

            byte[] data = await client.GetByteArrayAsync(_options.DownloadUrl, cancellationToken);
            await File.WriteAllBytesAsync(zipPath, data, cancellationToken);

            logger.LogInformation("Extracting Chromium archive...");
            ZipFile.ExtractToDirectory(zipPath, options.Value.GetBaseDirectory(), true);

            File.Delete(zipPath);

            SetEnvironmentVariables(Path.Combine(options.Value.GetBaseDirectory(), "chrome-win"), executablePath);

            logger.LogInformation("Chromium installation completed. Executable at {Path}", executablePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize Chromium");
            throw;
        }
    }

    private void SetEnvironmentVariables(string baseDir, string executablePath)
    {
        string configPath = Path.Combine(baseDir, ".config");
        string cachePath = Path.Combine(baseDir, ".cache");

        Environment.SetEnvironmentVariable("PUPPETEER_SKIP_CHROMIUM_DOWNLOAD", "true");
        Environment.SetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH", executablePath);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", configPath);
        Environment.SetEnvironmentVariable("XDG_CACHE_HOME", cachePath);

        logger.LogInformation("Environment variables set for Chromium: PUPPETEER_EXECUTABLE_PATH={ExecutablePath}, XDG_CONFIG_HOME={ConfigPath}, XDG_CACHE_HOME={CachePath}", executablePath, configPath, cachePath);
    }
}


using System.IO.Compression;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Browser.Interfaces;
using KamiYomu.Web.Infrastructure.Storage;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Infrastructure.Browser;

/// <summary>
/// Chronium bootstrapper for Linux. Downloads and sets up Chromium for Puppeteer usage on Linux systems.
/// </summary>
/// <param name="options"></param>
/// <param name="logger"></param>
public class LinuxChromiumBootstrapper(
    IOptions<ChromiumOptions> options,
    ILogger<LinuxChromiumBootstrapper> logger) : IChromiumBootstrapper
{

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (FileNameHelper.IsRunningInDocker())
            {
                logger.LogInformation("Running in Docker. Skipping Chromium bootstrap for Linux.");
                return;
            }

            if (!OperatingSystem.IsLinux())
            {
                logger.LogInformation("Not running on Linux. Skipping Chromium bootstrap for Linux.");
                return;
            }

            string baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KamiYomu",
                "chromium"
            );

            _ = Directory.CreateDirectory(baseDir);

            // Linux snapshot folder name is usually "chrome-linux"
            string executablePath = Path.Combine(baseDir, "chrome-linux", options.Value.ExecutableName);

            // ✔ If Chromium already exists, skip download
            if (File.Exists(executablePath))
            {
                SetEnvironmentVariables(Path.Combine(baseDir, "chrome-linux"), executablePath);
                logger.LogInformation("Chromium already installed at {Path}", executablePath);
                return;
            }

            logger.LogInformation("Chromium not found. Installing into {Dir}", baseDir);

            string zipPath = Path.Combine(baseDir, "chromium.zip");

            using HttpClient client = new();
            logger.LogInformation("Downloading Chromium from {Url}", options.Value.DownloadUrl);

            byte[] data = await client.GetByteArrayAsync(options.Value.DownloadUrl, cancellationToken);
            await File.WriteAllBytesAsync(zipPath, data, cancellationToken);

            logger.LogInformation("Extracting Chromium archive...");
            ZipFile.ExtractToDirectory(zipPath, baseDir, true);

            File.Delete(zipPath);

            // Chromium snapshot folder for Linux
            baseDir = Path.Combine(baseDir, "chrome-linux");

            SetEnvironmentVariables(baseDir, executablePath);

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

        // Linux-specific Puppeteer/XDG requirements
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", configPath);
        Environment.SetEnvironmentVariable("XDG_CACHE_HOME", cachePath);

        logger.LogInformation("Environment variables set for Chromium: PUPPETEER_EXECUTABLE_PATH={ExecutablePath}, XDG_CONFIG_HOME={ConfigPath}, XDG_CACHE_HOME={CachePath}", executablePath, configPath, cachePath);
    }
}

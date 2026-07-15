
using System.IO.Compression;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Browser.Interfaces;
using KamiYomu.Web.Infrastructure.Storage;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Infrastructure.Browser;

public class LinuxChromiumBootstrapper(
    IOptions<ChromiumOptions> options,
    ILogger<LinuxChromiumBootstrapper> logger) : IChromiumBootstrapper
{

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!FileNameHelper.IsRunningInDocker() && !OperatingSystem.IsLinux())
            {
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

            string configPath = Path.Combine(baseDir, ".config");
            string cachePath = Path.Combine(baseDir, ".cache");

            _ = Directory.CreateDirectory(configPath);
            _ = Directory.CreateDirectory(cachePath);

            Environment.SetEnvironmentVariable("PUPPETEER_SKIP_CHROMIUM_DOWNLOAD", "true");
            Environment.SetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH", executablePath);

            // Linux-specific Puppeteer/XDG requirements
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", configPath);
            Environment.SetEnvironmentVariable("XDG_CACHE_HOME", cachePath);

            logger.LogInformation("Chromium installation completed. Executable at {Path}", executablePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize Chromium");
            throw;
        }
    }

}

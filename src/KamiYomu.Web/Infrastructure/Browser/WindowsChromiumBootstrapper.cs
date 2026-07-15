using System.IO.Compression;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Browser.Interfaces;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Infrastructure.Browser;

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
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            string baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KamiYomu",
                "chromium"
            );

            _ = Directory.CreateDirectory(baseDir);

            // ✔ If Chromium already exists, skip download
            string executablePath = Path.Combine(baseDir, "chrome-win", _options.ExecutableName);

            if (File.Exists(executablePath))
            {
                logger.LogInformation("Chromium already installed at {Path}", executablePath);
                return;
            }

            logger.LogInformation("Chromium not found. Installing into {Dir}", baseDir);

            string zipPath = Path.Combine(baseDir, "chromium.zip");

            using HttpClient client = new();
            logger.LogInformation("Downloading Chromium from {Url}", _options.DownloadUrl);

            byte[] data = await client.GetByteArrayAsync(_options.DownloadUrl, cancellationToken);
            await File.WriteAllBytesAsync(zipPath, data, cancellationToken);

            logger.LogInformation("Extracting Chromium archive...");
            ZipFile.ExtractToDirectory(zipPath, baseDir, true);

            File.Delete(zipPath);

            baseDir = Path.Combine(baseDir, "chrome-win");

            string configPath = Path.Combine(baseDir, ".config");
            string cachePath = Path.Combine(baseDir, ".cache");

            Environment.SetEnvironmentVariable("PUPPETEER_SKIP_CHROMIUM_DOWNLOAD", "true");
            Environment.SetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH", executablePath);
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

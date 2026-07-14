using System.IO.Compression;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Storage;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Infrastructure.Browser;

public class ChromiumBootstrapper(
    IOptions<ChromiumOptions> options,
    ILogger<ChromiumBootstrapper> logger)
{
    private readonly ChromiumOptions _options = options.Value;
    /// <summary>
    /// Downloads and installs the Chromium browser if it is not already present, and sets required environment
    /// variables for its usage.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (FileNameHelper.IsRunningInDocker())
            {
                return;
            }

            string baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KamiYomu",
                "chromium"
            );

            _ = Directory.CreateDirectory(baseDir);

            baseDir = OperatingSystem.IsWindows()
                ? Path.Combine(baseDir, "chrome-win")
                : Path.Combine(baseDir, "chrome-linux");

            string executablePath = Path.Combine(baseDir, _options.ExecutableName);
            string configPath = Path.Combine(baseDir, ".config");
            string cachePath = Path.Combine(baseDir, ".cache");

            // ✔ If Chromium already exists, skip download
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

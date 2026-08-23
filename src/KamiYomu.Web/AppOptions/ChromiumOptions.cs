namespace KamiYomu.Web.AppOptions;

public class ChromiumOptions
{
    public bool Enabled { get; init; }
    public string DownloadUrl { get; init; }
    public string ExecutableName { get; init; }
    public int RequestTimeout { get; init; }
    public string[] Arguments { get; init; }
    public string GetExecutablePath()
    {
        string baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KamiYomu",
                "chromium"
            );

        return OperatingSystem.IsWindows()
            ? Path.Combine(baseDir, "chrome-win", ExecutableName)
            : OperatingSystem.IsLinux()
                ? Path.Combine(baseDir, "chrome-linux", ExecutableName)
                : throw new PlatformNotSupportedException("Unsupported operating system for Chromium.");
    }
}

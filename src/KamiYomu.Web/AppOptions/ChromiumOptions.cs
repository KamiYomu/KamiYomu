namespace KamiYomu.Web.AppOptions;

public class ChromiumOptions
{
    public bool Enabled { get; init; }
    public string DownloadUrl { get; init; }
    public string ExecutableName { get; init; }
    public int RequestTimeout { get; init; }
    public string[] Arguments { get; init; }

    public string GetBaseDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AppData",
            "chromium"
        );
    }

    public string GetLinuxDirectory()
    {
        return Path.Combine(GetBaseDirectory(), "chrome-linux");
    }

    public string GetWindowsDirectory()
    {
        return Path.Combine(GetBaseDirectory(), "chrome-win");
    }

    public bool IsExecutableExists()
    {
        string executablePath = GetExecutablePath();
        return File.Exists(executablePath);
    }
    /// <summary>
    /// Gets the full path to the Chromium executable based on the operating system.
    /// </summary>
    /// <returns>The full path to the Chromium executable.</returns>
    /// <exception cref="PlatformNotSupportedException"></exception>
    public string GetExecutablePath()
    {
        string baseDir = GetBaseDirectory();

        return OperatingSystem.IsWindows()
            ? Path.Combine(baseDir, "chrome-win", ExecutableName)
            : OperatingSystem.IsLinux()
                ? Path.Combine(baseDir, "chrome-linux", ExecutableName)
                : throw new PlatformNotSupportedException("Unsupported operating system for Chromium.");
    }
}

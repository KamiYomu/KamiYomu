using PuppeteerSharp;
using Serilog;
using System.Diagnostics;

const string ServiceName = "KamiYomu";
const string ServiceDescription = "A Manga Downloader - KamiYomu is a powerful, extensible manga downloader built for manga enthusiasts who want full control over their collection. It scans and downloads manga from supported websites, stores them locally, and lets you host your own private manga reade";

string logFilePath = Path.Combine(Path.GetTempPath(), $"{ServiceName}InstallerLog.txt");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
    .CreateLogger();

Log.Information("--- {ServiceName} Installer Helper ---", ServiceName);
Log.Information("Log file created at: {Path}", logFilePath);


if (args.Length == 0)
{
    Log.Warning("Please provide an argument: 'install' or 'uninstall'.");
    return;
}

string command = args[0].ToLower();
string webAppExePath = null;
string commandKey = null;

try
{
    // --- Robust Argument Parsing ---

    // Check if the argument contains the path assignment (e.g., /install=C:\Path)
    if (command.StartsWith("/install=") || command.StartsWith("install="))
    {
        var parts = command.Split('=', 2);
        Log.Information("part[1] = {0} | part[2] = {1}", parts[0], parts[1]);
        commandKey = parts[0].TrimStart('/'); // Get 'install'
        webAppExePath = parts[1].Trim('"'); // Get "C:\Program Files\..." and remove quotes
    }
    else if (command == "/install" || command == "install")
    {
        // Fallback for split arguments, expecting path in args[1]
        if (args.Length < 2)
        {
            throw new ArgumentException("Installation command requires the full path to the web application executable.");
        }
        commandKey = command.TrimStart('/');
        webAppExePath = args[1].Trim('"');
        Log.Information("part[1] = {0} | part[2] = {1}", args[0], args[1]);
    }
    else if (command == "/uninstall" || command == "uninstall")
    {
        commandKey = command.TrimStart('/');
    }

    // --- Action Execution ---

    switch (commandKey)
    {
        case "install":
            InstallAsync(webAppExePath).Wait();
            break;

        case "uninstall":
            Uninstall();
            break;

        default:
            Log.Warning("Unknown argument '{Argument}'. Use 'install' or 'uninstall'.", args[0]);
            break;
    }
}
catch (Exception ex)
{
    Exception baseException = (ex is AggregateException aggEx) ? aggEx.InnerException ?? ex : ex;
    // Log the exception to the file with full stack trace and details
    Log.Error(baseException, "\n[ERROR] Operation failed: {Message}", baseException.Message);

    // Only use Console.WriteLine for the final, critical exit message for the MSI user
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n[ERROR] Operation failed. Check log file at: {logFilePath} for details.");
    Console.ResetColor();
    Environment.Exit(1);
}
finally
{
    Log.CloseAndFlush();
}

static async Task InstallAsync(string webAppExePath)
{
    Log.Information("Starting installation process...");

    if (string.IsNullOrEmpty(webAppExePath) || !File.Exists(webAppExePath))
    {
        Log.Error("Target web application executable not found or path missing: {Path}", webAppExePath);
        throw new FileNotFoundException($"Target web application executable not found or path missing at: {webAppExePath}");
    }

    string binPath = $"\"{webAppExePath}\"";

    Log.Information("Service Executable Path: {Path}", binPath);

    Log.Information("\n[1/3] Registering Windows Service...");

    RunCommand("sc.exe", $"create {ServiceName} binPath= {binPath} start= auto");
    RunCommand("sc.exe", $"description {ServiceName} \"{ServiceDescription}\"");
    RunCommand("sc.exe", $"start {ServiceName}");

    Log.Information("Service '{ServiceName}' created and started successfully.", ServiceName);
    Console.WriteLine($"Service '{ServiceName}' created and started successfully.");

    Log.Information("\n[2/3] Ensuring Chromium dependency is installed...");
    await EnsureChromiumInstalledAsync();
    Log.Information("Chromium dependency ready.");

    Log.Information("\n[3/3] Cleaning up installer logs...");


    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n--- INSTALLATION COMPLETE ---");
    Console.ResetColor();
}

static async Task EnsureChromiumInstalledAsync()
{
    var fetcher = new BrowserFetcher();

    var installedRevision = fetcher.GetInstalledBrowsers()
                                    .FirstOrDefault(b => b.Browser == SupportedBrowser.Chromium);

    if (installedRevision != null && File.Exists(installedRevision.GetExecutablePath()))
    {
        Log.Information("Chromium is already installed. Skipping download.");
        Console.WriteLine("\tChromium is already installed. Skipping download.");
        return;
    }

    Log.Information("Chromium is missing. Downloading stable version...");
    Console.WriteLine("\tChromium is missing. Downloading stable version...");
    await fetcher.DownloadAsync(BrowserTag.Stable);
    Log.Information("Chromium download complete.");
    Console.WriteLine("\tChromium download complete.");
}

static void Uninstall()
{
    Log.Information("Starting uninstallation process...");

    Log.Information("\n[1/1] Stopping and deleting Windows Service...");
    RunCommand("sc.exe", $"stop {ServiceName}", false);
    RunCommand("sc.exe", $"delete {ServiceName}", false);

    Log.Information("Service '{ServiceName}' deleted.", ServiceName);
    Console.WriteLine($"Service '{ServiceName}' deleted.");

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n--- UNINSTALLATION COMPLETE ---");
    Console.ResetColor();
}

static void RunCommand(string fileName, string arguments, bool ignoreError = false)
{
    Log.Debug("Executing system command: {FileName} {Arguments}", fileName, arguments);

    var psi = new ProcessStartInfo
    {
        FileName = fileName,
        Arguments = arguments,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using var process = Process.Start(psi);

    string output = process!.StandardOutput.ReadToEnd();
    string error = process.StandardError.ReadToEnd();

    process.WaitForExit();

    if (!string.IsNullOrEmpty(output))
        Log.Debug("Command Output: {Output}", output.Trim());

    if (process.ExitCode != 0)
    {
        if (!string.IsNullOrEmpty(error))
            Log.Error("Command Error: {Error}", error.Trim());

        if (!ignoreError)
        {
            throw new InvalidOperationException(
                $"Command failed with exit code {process.ExitCode}. Executable: {fileName}, Arguments: {arguments}. Error: {error.Trim()}");
        }
    }
}
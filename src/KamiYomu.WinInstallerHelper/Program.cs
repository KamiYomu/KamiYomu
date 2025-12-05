using System.Diagnostics;
using System.Reflection;
using PuppeteerSharp;
using Serilog;
using Serilog.Core;

// --- Configuration ---
const string ServiceName = "KamiYomu";
const string ServiceDescription = "KamiYomu ASP.NET Web Application Host Service.";
const int WebAppPort = 8080;
// ---------------------

// --- Serilog Initialization ---
// The log file will be placed in the user's temporary directory, ensuring the MSI has permissions to write it.
string logFilePath = Path.Combine(Path.GetTempPath(), $"{ServiceName}InstallerLog.txt");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
    .CreateLogger();

Log.Information("--- {ServiceName} Service Installer Helper ---", ServiceName);
Log.Information("Log file created at: {Path}", logFilePath);


if (args.Length == 0)
{
    Log.Warning("Please provide an argument: 'install', 'uninstall', or 'download-chromium'.");
    return;
}

try
{
    string command = args[0].ToLower();
    string installDirectoryArgument = string.Empty;

    // Check if the argument contains a path assignment (e.g., install=C:\App)
    if (command.Contains("="))
    {
        var parts = command.Split('=', 2);
        command = parts[0];
        // The path will be quoted by MSI, so trim quotes
        installDirectoryArgument = parts[1].Trim('"');
    }

    switch (command)
    {
        case "install":
            // Use the explicitly provided path from MSI [TARGETDIR] property, 
            // falling back to AppContext.BaseDirectory only if parsing failed (which should not happen with correct MSI config)
            if (string.IsNullOrEmpty(installDirectoryArgument))
            {
                installDirectoryArgument = AppContext.BaseDirectory;
                Log.Warning("Installation directory was not explicitly passed via argument. Using AppContext.BaseDirectory: {Dir}", installDirectoryArgument);
            }
            else
            {
                Log.Information("Installation directory received from MSI argument: {Dir}", installDirectoryArgument);
            }
            await InstallAsync(installDirectoryArgument);
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
    Log.Error(ex, "Operation failed: {ErrorMessage}", ex.Message);
    Environment.Exit(1);
}
finally
{
    Log.CloseAndFlush();
}

// Updated InstallAsync to explicitly take the installation directory argument
static async Task InstallAsync(string installDirectory)
{
    Log.Information("Starting installation process...");
    Log.Information("Using determined installation directory: {InstallDirectory}", installDirectory);

    // The rest of the function uses the passed 'installDirectory' variable
    const string WebAppExeName = "KamiYomu.Web.exe";
    string exePath = Path.Combine(installDirectory, WebAppExeName);
    string binPath = $"\"{exePath}\"";
    Log.Information("Service Path: {BinPath}", binPath);

    if (!File.Exists(exePath))
    {
        Log.Error("The main application executable was not found at the expected path: {ExePath}", exePath);
        Log.Information("Please ensure the project is published correctly and '{ExeName}' exists in the installer directory.", WebAppExeName);

        // Retaining Console interaction for immediate feedback on critical error
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\nERROR: The main application executable was not found at the expected path:\n{exePath}");
        Console.WriteLine("Please ensure the project is published correctly and 'KamiYomu.Web.exe' exists in the installer directory.");
        Console.ResetColor();
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
        return;
    }

    Log.Information("\n[1/4] Registering Windows Service...");

    string createCommand = $"create {ServiceName} binPath= {binPath} start= auto";
    Log.Information("Executing: sc.exe {Command}", createCommand);
    RunCommand("sc.exe", createCommand);

    string descCommand = $"description {ServiceName} \"{ServiceDescription}\"";
    Log.Information("Executing: sc.exe {Command}", descCommand);
    RunCommand("sc.exe", descCommand);

    Log.Information("\n[2/4] Setting Service-Specific Environment Variables in Registry...");

    string aspnetUrls = $"ASPNETCORE_URLS=http://+:{WebAppPort}";
    string aspnetEnvironment = "ASPNETCORE_ENVIRONMENT=Windows";
    string registryData = $"{aspnetUrls} {aspnetEnvironment}";

    string regCommand = $"ADD \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\{ServiceName}\" /v Environment /t REG_MULTI_SZ /d \"{registryData}\" /f";

    Log.Information("Executing: reg.exe {Command}", regCommand);
    RunCommand("reg.exe", regCommand);

    Log.Information("Environment variables set in service registry key.");

    string startCommand = $"start {ServiceName}";
    Log.Information("Executing: sc.exe {Command}", startCommand);
    RunCommand("sc.exe", startCommand);

    Log.Information("Service '{ServiceName}' created and started successfully.", ServiceName);

    Log.Information("\n[3/4] Configuring Windows Firewall for Port {Port}...", WebAppPort);
    string firewallCommand = $"advfirewall firewall add rule name=\"{ServiceName} HTTP Port\" dir=in action=allow protocol=TCP localport={WebAppPort}";
    Log.Information("Executing: netsh.exe {Command}", firewallCommand);
    RunCommand("netsh.exe", firewallCommand);
    Log.Information("Firewall rule for port {Port} added successfully.", WebAppPort);

    Log.Information("\n[4/4] Handling Chromium dependency...");
    await DownloadChromiumAsync();

    Log.Information("\n--- INSTALLATION COMPLETE ---");

    Console.WriteLine("\nInstallation log complete. Press any key to exit...");
    Console.ReadKey();
}

static void Uninstall()
{
    Log.Information("Starting uninstallation process...");

    // --- 1. Registry Cleanup (Environment Variables) ---
    Log.Information("\n[1/3] Removing service environment variables from Registry...");
    string regDeleteCommand = $"DELETE \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\{ServiceName}\" /v Environment /f";
    Log.Information("Executing: reg.exe {Command}", regDeleteCommand);
    // Ignore error in case the service or environment key already failed to be created/deleted
    RunCommand("reg.exe", regDeleteCommand, false);
    Log.Information("Service environment variables removed (if they existed).");

    // --- 2. Service Cleanup ---
    Log.Information("\n[2/3] Stopping and deleting Windows Service...");
    Log.Information("Executing: sc.exe stop {ServiceName}", ServiceName);
    RunCommand("sc.exe", $"stop {ServiceName}", false);
    Log.Information("Executing: sc.exe delete {ServiceName}", ServiceName);
    RunCommand("sc.exe", $"delete {ServiceName}", false);

    Log.Information("Service '{ServiceName}' deleted successfully (if it existed).", ServiceName);

    // --- 3. Firewall Cleanup ---
    Log.Information("\n[3/3] Removing Windows Firewall rule for Port {Port}...", WebAppPort);
    Log.Information("Executing: netsh.exe advfirewall firewall delete rule name=\"{ServiceName} HTTP Port\"", ServiceName);
    RunCommand("netsh.exe", $"advfirewall firewall delete rule name=\"{ServiceName} HTTP Port\"", false);

    Log.Information("\n--- UNINSTALLATION COMPLETE ---");
}

static async Task DownloadChromiumAsync()
{
    Log.Information("Downloading Chromium (PuppeteerSharp dependency)...");
    var fetcher = new BrowserFetcher();
    await fetcher.DownloadAsync(BrowserTag.Stable);
    Log.Information("Chromium download complete.");
}

static void RunCommand(string fileName, string arguments, bool throwOnError = true)
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

        if (throwOnError)
        {
            throw new InvalidOperationException(
                $"Command failed with exit code {process.ExitCode}. Executable: {fileName}, Arguments: {arguments}. Error: {error.Trim()}");
        }
    }
}
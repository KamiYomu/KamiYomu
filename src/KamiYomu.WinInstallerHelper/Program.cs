using System.Diagnostics;
using System.Reflection;
using PuppeteerSharp;

// --- Configuration ---
const string ServiceName = "KamiYomu";
const string ServiceDescription = "KamiYomu ASP.NET Web Application Host Service.";
const int WebAppPort = 8080;
// ---------------------

Console.WriteLine($"--- {ServiceName} Service Installer Helper ---");

if (args.Length == 0)
{
    Console.WriteLine("Please provide an argument: 'install', 'uninstall', or 'download-chromium'.");
    return;
}

try
{
    switch (args[0].ToLower())
    {
        case "install":
            await InstallAsync();
            break;

        case "uninstall":
            Uninstall();
            break;

        case "download-chromium":
            await DownloadChromiumAsync();
            break;

        default:
            Console.WriteLine($"Unknown argument '{args[0]}'. Use 'install', 'uninstall', or 'download-chromium'.");
            break;
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n[ERROR] Operation failed: {ex.Message}");
    Console.ResetColor();
    Environment.Exit(1);
}

// --- Installation / Uninstallation Logic ---

static async Task InstallAsync()
{
    Console.WriteLine("Starting installation process...");

    // 1. Determine the path to the executable that will run as the service.
    // For a published .NET 8 app, this will be the main entry point executable.
    string exePath = Assembly.GetEntryAssembly()!.Location;

    // Ensure the path is enclosed in quotes for spaces
    string binPath = $"\"{exePath}\"";

    Console.WriteLine($"Service Path: {binPath}");

    // --- Service Registration ---
    Console.WriteLine("\n[1/3] Registering Windows Service...");

    // Command: sc create [ServiceName] binPath="[PathToExe]" start=auto
    // The main executable must be configured to use .UseWindowsService() for this to work.
    RunCommand("sc.exe", $"create {ServiceName} binPath= {binPath} start= auto");
    RunCommand("sc.exe", $"description {ServiceName} \"{ServiceDescription}\"");
    RunCommand("sc.exe", $"start {ServiceName}"); // Start the service immediately

    Console.WriteLine($"Service '{ServiceName}' created and started successfully.");

    // --- Firewall Configuration ---
    Console.WriteLine($"\n[2/3] Configuring Windows Firewall for Port {WebAppPort}...");
    RunCommand("netsh.exe", $"advfirewall firewall add rule name=\"{ServiceName} HTTP Port\" dir=in action=allow protocol=TCP localport={WebAppPort}");
    Console.WriteLine($"Firewall rule for port {WebAppPort} added successfully.");

    // --- Dependency Download (PuppeteerSharp) ---
    Console.WriteLine("\n[3/3] Handling Chromium dependency...");
    // NOTE: If Chromium is a required runtime dependency for your service, 
    // it's usually better to handle the check/download inside the service 
    // or through a separate, dedicated dependency script.
    await DownloadChromiumAsync();

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n--- INSTALLATION COMPLETE ---");
    Console.ResetColor();
}

static void Uninstall()
{
    Console.WriteLine("Starting uninstallation process...");

    // --- Service Cleanup ---
    Console.WriteLine("\n[1/2] Stopping and deleting Windows Service...");
    // Always attempt stop first, then delete. Ignore errors if it's already stopped/missing.
    RunCommand("sc.exe", $"stop {ServiceName}", false);
    RunCommand("sc.exe", $"delete {ServiceName}");

    Console.WriteLine($"Service '{ServiceName}' deleted successfully.");

    // --- Firewall Cleanup ---
    Console.WriteLine($"\n[2/2] Removing Windows Firewall rule for Port {WebAppPort}...");
    // Ignore errors if the rule doesn't exist
    RunCommand("netsh.exe", $"advfirewall firewall delete rule name=\"{ServiceName} HTTP Port\"", false);

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n--- UNINSTALLATION COMPLETE ---");
    Console.ResetColor();
}

// --- Utility Methods ---

static async Task DownloadChromiumAsync()
{
    Console.WriteLine("Downloading Chromium (PuppeteerSharp dependency)...");
    var fetcher = new BrowserFetcher();
    await fetcher.DownloadAsync(BrowserTag.Stable);
    Console.WriteLine("Chromium download complete.");
}

/// <summary>
/// Runs a system command, capturing output and checking the exit code.
/// </summary>
/// <param name="fileName">The command executable (e.g., "sc.exe").</param>
/// <param name="arguments">The arguments to pass to the command.</param>
/// <param name="throwOnError">If true, throws an exception if the command exits with a non-zero code.</param>
static void RunCommand(string fileName, string arguments, bool throwOnError = true)
{
    Console.WriteLine($"\tExecuting: {fileName} {arguments}");

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

    // Using asynchronous read to avoid deadlock on large outputs
    string output = process!.StandardOutput.ReadToEnd();
    string error = process.StandardError.ReadToEnd();

    process.WaitForExit();

    if (!string.IsNullOrEmpty(output))
        Console.WriteLine($"\t[OUT] {output.Trim()}");

    if (process.ExitCode != 0)
    {
        if (!string.IsNullOrEmpty(error))
            Console.WriteLine($"\t[ERR] {error.Trim()}");

        if (throwOnError)
        {
            throw new InvalidOperationException(
                $"Command failed with exit code {process.ExitCode}. Executable: {fileName}, Arguments: {arguments}. Error: {error.Trim()}");
        }
    }
}
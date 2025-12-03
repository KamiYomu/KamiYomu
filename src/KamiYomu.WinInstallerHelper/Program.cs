using PuppeteerSharp;
using System.Diagnostics;

const string KamiYomu = nameof(KamiYomu);
const int Port = 8080;

if (args.Length > 0)
{
    switch (args[0].ToLower())
    {
        case "install":
            await InstallAsync();
            break;

        case "uninstall":
            Uninstall();
            break;

        default:
            Console.WriteLine("Unknown argument. Use 'install' or 'uninstall'.");
            break;
    }
}
 static async Task DownloadChromiumAsync()
{
    Console.WriteLine("Downloading Chromium...");
    var fetcher = new BrowserFetcher();
    await fetcher.DownloadAsync(BrowserTag.Stable);
    Console.WriteLine("Chromium download complete.");
}

 static async Task InstallAsync()
{
    Console.WriteLine("Installing service...");

    string exePath = Process.GetCurrentProcess().MainModule.FileName;

    // Register Windows Service
    RunCommand("sc.exe", $"create {KamiYomu} binPath= \"{exePath}\" start= auto");
    RunCommand("sc.exe", $"description {KamiYomu} \"{KamiYomu} Worker Service\"");

    // Open firewall port 8080
    RunCommand("netsh", $"advfirewall firewall add rule name=\"{KamiYomu} Port {Port}\" dir=in action=allow protocol=TCP localport={Port}");


    await DownloadChromiumAsync();

    Console.WriteLine("Install complete.");
}

 static void Uninstall()
{
    Console.WriteLine("Uninstalling service...");

    // Stop and delete service
    RunCommand("sc.exe", $"stop {KamiYomu}");
    RunCommand("sc.exe", $"delete {KamiYomu}");

    // Remove firewall rule
    RunCommand("netsh", $"advfirewall firewall delete rule name=\"{KamiYomu} Port {Port}\"");

    Console.WriteLine("Uninstall complete.");
}

 static void RunCommand(string fileName, string arguments)
{
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
    process.WaitForExit();

    string output = process.StandardOutput.ReadToEnd();
    string error = process.StandardError.ReadToEnd();

    if (!string.IsNullOrEmpty(output))
        Console.WriteLine(output);

    if (!string.IsNullOrEmpty(error))
        Console.WriteLine(error);
}



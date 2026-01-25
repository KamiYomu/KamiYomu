using System.Reflection;

using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.ViewComponents;

public class AppVersionViewComponent(IGitHubService gitHubService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(CancellationToken cancellationToken = default)
    {
        Assembly assembly = typeof(Program).Assembly;
        Assembly coreAssembly = typeof(AbstractCrawlerAgent).Assembly;
        string? coreInformationalVersion = coreAssembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        string? fullVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        string? version = fullVersion?.Split('+')[0];
        string? copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
        bool updateAvailable = await gitHubService.CheckForUpdatesAsync(version, cancellationToken);
        string updateVersionAvailable = await gitHubService.GetLatestVersionAsync(cancellationToken);

        return View(new AppVersionViewComponentModel(
            coreInformationalVersion,
            version,
            copyright,
            updateAvailable,
            updateVersionAvailable));
    }
}

public record AppVersionViewComponentModel(
    string? CoreInformationalVersion,
    string Version,
    string? Copyright,
    bool UpdateAvailable,
    string UpdateVersionAvailable);

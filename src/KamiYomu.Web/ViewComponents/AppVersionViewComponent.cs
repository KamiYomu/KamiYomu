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
        AssemblyName name = assembly.GetName();
        string? coreInformationalVersion = coreAssembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        string? fullVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        string? version = fullVersion?.Split('+')[0];
        string? company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
        string? product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        string? copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
        string? description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;
        bool updateAvailable = await gitHubService.CheckForUpdatesAsync(version, cancellationToken);
        string updateVersionAvailable = await gitHubService.GetLatestVersionAsync(cancellationToken);

        return View(new AppVersionViewComponentModel(
            name,
            fullVersion,
            coreInformationalVersion,
            version,
            company,
            product,
            copyright,
            description,
            updateAvailable,
            updateVersionAvailable));
    }
}

public record AppVersionViewComponentModel(
    AssemblyName Name,
    string? FullVersion,
    string? CoreInformationalVersion,
    string Version,
    string? Company,
    string? Product,
    string? Copyright,
    string? Description,
    bool UpdateAvailable,
    string UpdateVersionAvailable);

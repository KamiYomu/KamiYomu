using KamiYomu.Web.Entities.Addons;

using Microsoft.AspNetCore.Mvc;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Areas.Settings.ViewComponents;

public class PackageItemViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(NugetPackageGroupedViewModel viewModel)
    {
        return View(viewModel);
    }
}

public class NugetPackageGroupedViewModel
{
    public Guid SourceId { get; set; }
    public string Id { get; set; } = string.Empty;
    public Uri? IconUrl { get; set; }
    public string? Description { get; set; }
    public string[] Authors { get; set; } = [];
    public string[] Tags { get; set; } = [];
    public int TotalDownloads { get; set; }
    public required NugetPackageInfo VersionSelected { get; set; }
    public IEnumerable<string?> Versions { get; set; } = [];
    public Uri? LicenseUrl { get; set; }
    public Uri? RepositoryUrl { get; set; }
    public Dictionary<string, List<NugetDependencyInfo>> DependenciesByVersion { get; set; } = [];

    public string GetCardId()
    {
        return $"package-card-{Id}".Replace(".", "-");
    }
    public bool IsNsfw()
    {
        return Tags.Any(p => string.Equals(p, Package.NotSafeForWorkTag, StringComparison.OrdinalIgnoreCase));
    }
}

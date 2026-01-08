using KamiYomu.Web.Entities.Addons;

namespace KamiYomu.Web.Areas.Settings.Pages.Add_ons.ViewModels;

public class PackageListViewModel
{
    public Guid SourceId { get; set; }
    public IEnumerable<NugetPackageGroupedViewModel> PackageItems { get; set; } = [];
}

public class NugetPackageGroupedViewModel
{
    private const string NotSafeForWork = "nsfw";
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

    public bool IsNsfw()
    {
        return Tags.Any(p => string.Equals(p, NotSafeForWork, StringComparison.OrdinalIgnoreCase));
    }
}

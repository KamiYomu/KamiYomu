using NuGet.Versioning;

namespace KamiYomu.Web.Entities.Addons;

public class NugetPackageInfo
{
    public string? Id { get; init; }
    public Uri? IconUrl { get; init; }
    public string? Version { get; init; }
    public string? Description { get; init; }
    public string[] Authors { get; init; } = [];
    public string[] Tags { get; init; } = [];
    public int? TotalDownloads { get; init; }
    public Uri? LicenseUrl { get; init; }
    public Uri? RepositoryUrl { get; init; }
    public required List<NugetDependencyInfo> Dependencies { get; set; }


    public string GetKamiYomuCoreRangeVersion()
    {
        NugetDependencyInfo? kamiYomuCoreDependency = Dependencies?
            .FirstOrDefault(d => d.Id?.Equals("KamiYomu.CrawlerAgents.Core", StringComparison.OrdinalIgnoreCase) == true);
        return kamiYomuCoreDependency?.VersionRange ?? "Unknown";
    }

    public string GetKamiYomuCoreVersion()
    {
        string range = GetKamiYomuCoreRangeVersion();

        if (string.IsNullOrWhiteSpace(range) || range == "Unknown")
        {
            return "Unknown";
        }

        string cleaned = range.Trim('[', ']', '(', ')');

        string[] parts = cleaned.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length > 0 ? parts[0] : "Unknown";
    }


    public bool IsVersionCompatible()
    {
        string versionRangeString = GetKamiYomuCoreRangeVersion(); // e.g. "[1.1.0, )"
        Version currentVersion = typeof(KamiYomu.CrawlerAgents.Core.ICrawlerAgent)
            .Assembly
            .GetName()
            .Version ?? new Version(0, 0, 0);

        if (VersionRange.TryParse(versionRangeString, out VersionRange? range))
        {
            NuGetVersion nugetVersion = new(currentVersion);
            return range.Satisfies(nugetVersion) &&
                  range.MinVersion != null &&
                  nugetVersion == range.MinVersion;

        }

        // If parsing fails, treat as incompatible
        return false;
    }
}

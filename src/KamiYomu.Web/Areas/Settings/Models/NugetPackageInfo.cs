using NuGet.Versioning;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Areas.Settings.Models;

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


    public string GetNugetPackageKamiYomuCoreRangeVersion()
    {
        NugetDependencyInfo? kamiYomuCoreDependency = Dependencies?
            .FirstOrDefault(d => d.Id?.Equals("KamiYomu.CrawlerAgents.Core", StringComparison.OrdinalIgnoreCase) == true);
        return kamiYomuCoreDependency?.VersionRange ?? "Unknown";
    }

    public string GetKamiYomuCoreVersion()
    {
        string range = GetNugetPackageKamiYomuCoreRangeVersion();

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
        string coreVersionRangeString = GetNugetPackageKamiYomuCoreRangeVersion();

        if (string.IsNullOrWhiteSpace(coreVersionRangeString) ||
            coreVersionRangeString.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!VersionRange.TryParse(coreVersionRangeString, out VersionRange? range))
        {
            return false;
        }

        // Extract the minimum version from the range
        NuGetVersion? minVersion = range.MinVersion;

        if (minVersion is null)
        {
            return false;
        }

        // Reject anything below 1.1.4
        NuGetVersion minimumRequired = new NuGetVersion(1, 1, 4);
        if (minVersion < minimumRequired)
        {
            return false;
        }

        // Now check if the range itself is valid for the current agent version
        Version? currentVersion = typeof(ICrawlerAgent)
            .Assembly
            .GetName()
            .Version;

        if (currentVersion is null)
        {
            return false;
        }

        NuGetVersion agentVersion = new(
            currentVersion.Major,
            currentVersion.Minor,
            currentVersion.Build);

        return range.Satisfies(agentVersion);
    }



    public bool IsNsfw()
    {
        return Tags.Any(p => string.Equals(p, Package.NotSafeForWorkTag, StringComparison.OrdinalIgnoreCase));
    }
}

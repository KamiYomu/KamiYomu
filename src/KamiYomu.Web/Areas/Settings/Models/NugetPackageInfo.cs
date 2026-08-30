using System.Text.Json.Nodes;

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
    public long? TotalDownloads { get; init; }
    public Uri? LicenseUrl { get; init; }
    public Uri? RepositoryUrl { get; init; }
    public List<string> Dependencies { get; set; } = [];

    public static Uri GetIconUri(JsonNode? info, string packageId)
    {
        string? iconUrl = info?["iconUrl"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(iconUrl))
        {
            return new Uri(iconUrl, UriKind.Absolute);
        }
        else
        {
            string repository = info?["projectUrl"]?.GetValue<string>() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(repository)
                ? new Uri(repository.TrimEnd('/') + $"/raw/branch/main/src/{packageId}/Resources/logo.png", UriKind.Absolute)
                : new Uri("/images/favicon.ico", UriKind.Relative);
        }
    }
    public string GetNugetPackageKamiYomuCoreRangeVersion()
    {
        if (Dependencies?.Count > 0)
        {
            string? kamiYomuCoreDependency = Dependencies?
            .FirstOrDefault(d => d.StartsWith("KamiYomu.CrawlerAgents.Core", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(kamiYomuCoreDependency))
            {
                string[] parts = kamiYomuCoreDependency.Split(':', 2);
                if (parts.Length == 2)
                {
                    return parts[1];
                }
            }
        }
        return I18n.Unknown;
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

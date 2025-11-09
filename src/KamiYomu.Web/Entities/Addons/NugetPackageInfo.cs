namespace KamiYomu.Web.Entities.Addons
{
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
    }
}

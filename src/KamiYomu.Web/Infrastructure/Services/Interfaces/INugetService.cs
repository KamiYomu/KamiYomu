using KamiYomu.Web.Entities.Addons;

namespace KamiYomu.Web.Infrastructure.Services.Interfaces
{
    public interface INugetService
    {
        Task<NugetPackageInfo?> GetPackageMetadataAsync(string packageName, Guid sourceId, CancellationToken cancellationToken);
        Task<IEnumerable<NugetPackageInfo>> SearchPackagesAsync(string query, bool includePreRelease, Guid sourceId, CancellationToken cancellationToken);
        Task<Stream[]> OnGetDownloadAsync(Guid sourceId, string packageId, string packageVersion, CancellationToken cancellationToken);
    }
}

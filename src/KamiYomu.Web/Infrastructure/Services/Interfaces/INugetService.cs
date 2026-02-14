using KamiYomu.Web.Areas.Settings.Models;

namespace KamiYomu.Web.Infrastructure.Services.Interfaces;

public interface INugetService
{
    Task<NugetPackageInfo?> GetPackageMetadataAsync(Guid sourceId, string packageId, string version, CancellationToken cancellationToken);
    Task<IEnumerable<NugetPackageInfo>> GetAllPackageVersionsAsync(Guid sourceId, string packageId, CancellationToken cancellationToken);
    Task<IEnumerable<NugetPackageInfo>> SearchPackagesAsync(Guid sourceId, string query, bool includePreRelease, CancellationToken cancellationToken);
    Task<Stream[]> OnGetDownloadAsync(Guid sourceId, string packageId, string packageVersion, CancellationToken cancellationToken);
}

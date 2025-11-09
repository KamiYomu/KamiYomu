using KamiYomu.Web.Entities.Addons;

namespace KamiYomu.Web.Infrastructure.Services.Interfaces
{
    public interface INugetService
    {
        Task<NugetPackageInfo?> GetPackageMetadataAsync(string packageName, Guid sourceId);
        Task<IEnumerable<NugetPackageInfo>> SearchPackagesAsync(string query, Guid sourceId);
        Task<Stream> OnGetDownloadAsync(Guid sourceId, string packageId, string packageVersion);
    }
}

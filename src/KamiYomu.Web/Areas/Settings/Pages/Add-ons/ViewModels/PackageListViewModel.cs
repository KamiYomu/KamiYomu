using KamiYomu.Web.Entities.Addons;

namespace KamiYomu.Web.Areas.Settings.Pages.Add_ons.ViewModels
{
    public class PackageListViewModel
    {
        public Guid SourceId { get; set; }
        public IEnumerable<NugetPackageInfo> Packages { get; set; } = Enumerable.Empty<NugetPackageInfo>();
    }

}

using KamiYomu.Web.Entities.Addons;

namespace KamiYomu.Web.Areas.Settings.Pages.Add_ons.ViewModels
{
    public class PackageListViewModel
    {
        public Guid SourceId { get; set; }
        public IEnumerable<PackageItemViewModel> PackageItems { get; set; } = Enumerable.Empty<PackageItemViewModel>();
    }

    public class PackageItemViewModel
    {
        public Guid SourceId { get; set; }
        public NugetPackageInfo? Package { get; set; }
    }

}

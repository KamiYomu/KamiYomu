using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Areas.Settings.ViewComponents;

public class PackageListViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(PackageListViewModel viewModel)
    {
        return View(viewModel);
    }
}
public class PackageListViewModel
{
    public Guid SourceId { get; set; }
    public IEnumerable<NugetPackageGroupedViewModel> PackageItems { get; set; } = [];
}


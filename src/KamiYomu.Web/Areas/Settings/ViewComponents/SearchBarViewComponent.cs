using KamiYomu.Web.Entities.Addons;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Areas.Settings.ViewComponents;

public class SearchBarViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(SearchBarViewModel viewModel)
    {
        return View(viewModel);
    }
}


public class SearchBarViewModel
{
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public bool IncludePrerelease { get; set; } = false;

    [BindProperty(SupportsGet = true)]
    public Guid SourceId { get; set; } = Guid.Empty;

    public IEnumerable<NugetSource> Sources { get; set; } = [];

}


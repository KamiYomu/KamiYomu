using KamiYomu.Web.Areas.Libraries.Pages.Collection.Dialogs;
using KamiYomu.Web.Areas.Libraries.ViewComponents;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.Areas.Libraries.ViewComponents;

public class ScanNowButtonViewComponentTests
{
    [Fact]
    public void Invoke_ReturnsViewWithProvidedViewModel()
    {
        ScanNowButtonViewModel viewModel = new()
        {
            IsScanning = true,
            LibraryId = Guid.NewGuid()
        };
        ScanNowButtonViewComponent component = new();

        IViewComponentResult result = component.Invoke(viewModel);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        Assert.Same(viewModel, viewResult.ViewData.Model);
    }
}

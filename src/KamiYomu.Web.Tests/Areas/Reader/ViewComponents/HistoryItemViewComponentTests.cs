using KamiYomu.Web.Areas.Reader.Models;
using KamiYomu.Web.Areas.Reader.ViewComponents;
using KamiYomu.Web.Areas.Reader.ViewModels;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.Areas.Reader.ViewComponents;

public class HistoryItemViewComponentTests
{
    [Fact]
    public void Invoke_ReturnsViewWithProvidedModel()
    {
        ChapterViewModel viewModel = new()
        {
            ChapterProgress = new ChapterProgress(Guid.NewGuid(), Guid.NewGuid(), 1),
            Library = ServiceTestHelpers.CreateLibrary("Alpha")
        };
        HistoryItemViewComponent component = new();

        IViewComponentResult result = component.Invoke(viewModel);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        Assert.Same(viewModel, viewResult.ViewData.Model);
    }
}

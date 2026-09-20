using KamiYomu.Web.Areas.Libraries.Pages.Collection.Dialogs;
using KamiYomu.Web.Areas.Libraries.ViewComponents;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.Areas.Libraries.ViewComponents;

public class FollowButtonViewComponentTests
{
    [Fact]
    public void Invoke_ReturnsViewWithProvidedViewModel()
    {
        FollowButtonViewModel viewModel = new()
        {
            IsFollowing = true,
            LibraryId = Guid.NewGuid(),
            DailyExecutionSchedule = TimeSpan.FromHours(3)
        };
        FollowButtonViewComponent component = new();

        IViewComponentResult result = component.Invoke(viewModel);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        Assert.Same(viewModel, viewResult.ViewData.Model);
    }
}

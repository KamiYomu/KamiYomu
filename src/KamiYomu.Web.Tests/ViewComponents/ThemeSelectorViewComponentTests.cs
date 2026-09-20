using KamiYomu.Web.ViewComponents;

using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.ViewComponents;

public class ThemeSelectorViewComponentTests
{
    [Fact]
    public void Invoke_ReturnsDefaultViewWithoutModel()
    {
        // Arrange
        ThemeSelectorViewComponent component = new();

        // Act
        Microsoft.AspNetCore.Mvc.IViewComponentResult result = component.Invoke();

        // Assert
        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);

        Assert.Null(viewResult.ViewName);
        Assert.Null(viewResult.ViewData.Model);
    }
}

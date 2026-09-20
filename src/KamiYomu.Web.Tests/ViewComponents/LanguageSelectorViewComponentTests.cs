using KamiYomu.Web.ViewComponents;

using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.ViewComponents;

public class LanguageSelectorViewComponentTests
{
    [Fact]
    public void Invoke_ReturnsDefaultViewWithoutModel()
    {
        // Arrange
        LanguageSelectorViewComponent component = new();

        // Act
        Microsoft.AspNetCore.Mvc.IViewComponentResult result = component.Invoke();

        // Assert
        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);

        Assert.Null(viewResult.ViewName);
        Assert.Null(viewResult.ViewData.Model);
    }
}

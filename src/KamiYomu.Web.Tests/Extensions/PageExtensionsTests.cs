using KamiYomu.Web.Extensions;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Tests.Extensions;

public class PageExtensionsTests
{
    [Fact]
    public void RedirectToAreaPage_ReturnsRedirectWithAreaAndRouteValues()
    {
        IActionResult actionResult = PageExtensions.RedirectToAreaPage("Libraries", "/Index", new { id = 42 });

        RedirectToPageResult result = Assert.IsType<RedirectToPageResult>(actionResult);
        Assert.Equal("/Index", result.PageName);
        Assert.Equal("Libraries", result.RouteValues!["area"]);
        Assert.Equal(42, result.RouteValues["id"]);
    }

    [Fact]
    public void RedirectToAreaPage_CreatesRouteValuesWhenNoneAreProvided()
    {
        IActionResult actionResult = PageExtensions.RedirectToAreaPage("Settings", "/Index");

        RedirectToPageResult result = Assert.IsType<RedirectToPageResult>(actionResult);
        Assert.Equal("Settings", result.RouteValues!["area"]);
        _ = Assert.Single(result.RouteValues);
    }
}

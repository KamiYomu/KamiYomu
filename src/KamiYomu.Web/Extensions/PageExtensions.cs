using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Extensions;

public static class PageExtensions
{
    public static IActionResult RedirectToAreaPage(string area, string page, object routeValues = null!)
    {
        return new RedirectToPageResult(page, routeValues)
        {
            RouteValues = new RouteValueDictionary(routeValues) { ["area"] = area }
        };
    }

}

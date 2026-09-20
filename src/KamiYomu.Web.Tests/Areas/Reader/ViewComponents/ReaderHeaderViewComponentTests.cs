using KamiYomu.Web.Areas.Reader.ViewComponents;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace KamiYomu.Web.Tests.Areas.Reader.ViewComponents;

public class ReaderHeaderViewComponentTests
{
    [Fact]
    public void Invoke_WhenReturnUrlProvided_UsesProvidedValue()
    {
        ReaderHeaderViewComponent component = CreateComponent(queryString: "");

        IViewComponentResult result = component.Invoke("/some/path");

        ReaderHeaderComponentModel model = GetModel(result);
        Assert.Equal("/some/path", model.ReturnUrl);
    }

    [Fact]
    public void Invoke_WhenReturnUrlMissing_FallsBackToQueryString()
    {
        ReaderHeaderViewComponent component = CreateComponent(queryString: "?returnUrl=/from/query");

        IViewComponentResult result = component.Invoke(null);

        ReaderHeaderComponentModel model = GetModel(result);
        Assert.Equal("/from/query", model.ReturnUrl);
    }

    [Fact]
    public void Invoke_WhenReturnUrlWhitespaceAndNoQueryString_ReturnsEmptyString()
    {
        ReaderHeaderViewComponent component = CreateComponent(queryString: "");

        IViewComponentResult result = component.Invoke("   ");

        ReaderHeaderComponentModel model = GetModel(result);
        Assert.Equal(string.Empty, model.ReturnUrl);
    }

    private static ReaderHeaderComponentModel GetModel(IViewComponentResult result)
    {
        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        return Assert.IsType<ReaderHeaderComponentModel>(viewResult.ViewData.Model);
    }

    private static ReaderHeaderViewComponent CreateComponent(string queryString)
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.QueryString = new QueryString(queryString);

        ViewContext viewContext = new() { HttpContext = httpContext };
        ViewComponentContext viewComponentContext = new() { ViewContext = viewContext };

        return new ReaderHeaderViewComponent { ViewComponentContext = viewComponentContext };
    }
}

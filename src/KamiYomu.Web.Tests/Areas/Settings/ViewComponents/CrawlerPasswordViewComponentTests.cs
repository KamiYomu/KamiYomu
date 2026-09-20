using KamiYomu.CrawlerAgents.Core.Inputs;
using KamiYomu.Web.Areas.Settings.ViewComponents;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.Areas.Settings.ViewComponents;

public class CrawlerPasswordViewComponentTests
{
    [Fact]
    public void Invoke_ReturnsViewWithProvidedModel()
    {
        CrawlerPasswordAttribute attribute = new("password", "Password");
        CrawlerPasswordViewComponent component = new();

        IViewComponentResult result = component.Invoke(attribute, "field-name", "field-value", null);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        CrawlerPasswordViewModel model = Assert.IsType<CrawlerPasswordViewModel>(viewResult.ViewData.Model);
        Assert.Same(attribute, model.CrawlerPasswordAttribute);
        Assert.Equal("field-name", model.FieldName);
        Assert.Equal("field-value", model.FieldValue);
        Assert.Null(model.ModelStateEntry);
    }
}

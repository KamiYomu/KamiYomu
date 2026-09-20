using KamiYomu.CrawlerAgents.Core.Inputs;
using KamiYomu.Web.Areas.Settings.ViewComponents;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.Areas.Settings.ViewComponents;

public class CrawlerTextViewComponentTests
{
    [Fact]
    public void Invoke_ReturnsViewWithProvidedModel()
    {
        CrawlerTextAttribute attribute = new("text", "Text");
        CrawlerTextViewComponent component = new();

        IViewComponentResult result = component.Invoke(attribute, "field-name", "field-value", null);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        CrawlerTextViewModel model = Assert.IsType<CrawlerTextViewModel>(viewResult.ViewData.Model);
        Assert.Same(attribute, model.CrawlerTextAttribute);
        Assert.Equal("field-name", model.FieldName);
        Assert.Equal("field-value", model.FieldValue);
        Assert.Null(model.ModelStateEntry);
    }
}

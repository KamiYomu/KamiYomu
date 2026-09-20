using KamiYomu.CrawlerAgents.Core.Inputs;
using KamiYomu.Web.Areas.Settings.ViewComponents;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.Areas.Settings.ViewComponents;

public class CrawlerSelectViewComponentTests
{
    [Fact]
    public void Invoke_ReturnsViewWithProvidedModel()
    {
        CrawlerSelectAttribute attribute = new("select", "Select", ["a", "b"]);
        CrawlerSelectViewComponent component = new();

        IViewComponentResult result = component.Invoke(attribute, "field-name", "field-value", null);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        CrawlerSelectViewModel model = Assert.IsType<CrawlerSelectViewModel>(viewResult.ViewData.Model);
        Assert.Same(attribute, model.CrawlerSelectAttribute);
        Assert.Equal("field-name", model.FieldName);
        Assert.Equal("field-value", model.FieldValue);
        Assert.Null(model.ModelStateEntry);
    }
}

public class CrawlerSelectViewModelTests
{
    [Fact]
    public void FieldId_SlugifiesAttributeName()
    {
        CrawlerSelectAttribute attribute = new("My Select Field", "Select", ["a"]);
        CrawlerSelectViewModel model = new(attribute, "field-name", "field-value", null);

        Assert.Equal("select-my-select-field", model.FieldId);
    }

    [Fact]
    public void FieldId_CollapsesConsecutiveInvalidCharacters()
    {
        CrawlerSelectAttribute attribute = new("A__B  C", "Select", ["a"]);
        CrawlerSelectViewModel model = new(attribute, "field-name", "field-value", null);

        Assert.Equal("select-a-b-c", model.FieldId);
    }
}

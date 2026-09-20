using KamiYomu.CrawlerAgents.Core.Inputs;
using KamiYomu.Web.Areas.Settings.ViewComponents;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace KamiYomu.Web.Tests.Areas.Settings.ViewComponents;

public class CrawlerCheckBoxViewComponentTests
{
    [Fact]
    public void Invoke_ReturnsViewWithProvidedModel()
    {
        CrawlerCheckBoxAttribute attribute = new("options", "Options", ["a", "b"]);
        Dictionary<string, string> selectedData = new() { ["options.a"] = "true" };
        CrawlerCheckBoxViewComponent component = new();

        IViewComponentResult result = component.Invoke(attribute, selectedData, null);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        CrawlerCheckBoxViewModel model = Assert.IsType<CrawlerCheckBoxViewModel>(viewResult.ViewData.Model);
        Assert.Same(attribute, model.CrawlerCheckBoxAttribute);
        Assert.Same(selectedData, model.SelectedData);
        Assert.Null(model.ModelStateEntry);
    }
}

public class CrawlerCheckBoxViewModelTests
{
    [Fact]
    public void IsChecked_ReturnsTrue_WhenSelectedDataHasTrueValue()
    {
        CrawlerCheckBoxAttribute attribute = new("options", "Options", ["a"]);
        Dictionary<string, string> selectedData = new() { ["options.a"] = "true" };
        CrawlerCheckBoxViewModel model = new(attribute, selectedData, null);

        Assert.True(model.IsChecked("a"));
    }

    [Fact]
    public void IsChecked_ReturnsFalse_WhenSelectedDataHasFalseValue()
    {
        CrawlerCheckBoxAttribute attribute = new("options", "Options", true, "a", ["a"]);
        Dictionary<string, string> selectedData = new() { ["options.a"] = "false" };
        CrawlerCheckBoxViewModel model = new(attribute, selectedData, null);

        // Even though "a" is present in DefaultValue, the second clause of the OR only applies
        // when the dictionary lookup produced no value at all (value == null). Since "options.a"
        // was found (albeit "false"), the fallback to DefaultValue never triggers here.
        Assert.False(model.IsChecked("a"));
    }

    [Fact]
    public void IsChecked_FallsBackToDefaultValue_WhenKeyNotInSelectedData()
    {
        // DefaultValue is a plain string, so "a" being contained in the DefaultValue means
        // DefaultValue.Contains("a") performs a *substring* check, not a collection lookup.
        CrawlerCheckBoxAttribute attribute = new("options", "Options", true, "a", ["a"]);
        Dictionary<string, string> selectedData = [];
        CrawlerCheckBoxViewModel model = new(attribute, selectedData, null);

        Assert.True(model.IsChecked("a"));
    }

    [Fact]
    public void IsChecked_ReturnsFalse_WhenKeyNotInSelectedDataAndNotInDefaultValue()
    {
        CrawlerCheckBoxAttribute attribute = new("options", "Options", true, "z", ["a"]);
        Dictionary<string, string> selectedData = [];
        CrawlerCheckBoxViewModel model = new(attribute, selectedData, null);

        Assert.False(model.IsChecked("a"));
    }

    [Fact]
    public void IsChecked_ReturnsFalse_WhenSelectedDataHasUnparseableValue_EvenIfDefaultContainsKey()
    {
        // Documents actual (bug-prone) behavior: when the key IS present in SelectedData but its
        // value is not a valid bool, "value" is non-null, so the DefaultValue fallback never
        // triggers - even though DefaultValue.Contains(key) would be true.
        CrawlerCheckBoxAttribute attribute = new("options", "Options", true, "a", ["a"]);
        Dictionary<string, string> selectedData = new() { ["options.a"] = "not-a-bool" };
        CrawlerCheckBoxViewModel model = new(attribute, selectedData, null);

        Assert.False(model.IsChecked("a"));
    }

    [Fact]
    public void GetFieldId_ReplacesInvalidCharactersWithHyphens()
    {
        CrawlerCheckBoxAttribute attribute = new("options", "Options", ["a"]);
        CrawlerCheckBoxViewModel model = new(attribute, [], null);

        Assert.Equal("checkbox-my-key-1", model.GetFieldId("My Key.1"));
    }

    [Fact]
    public void GetFieldName_UsesAttributeNameAndKey()
    {
        CrawlerCheckBoxAttribute attribute = new("options", "Options", ["a"]);
        CrawlerCheckBoxViewModel model = new(attribute, [], null);

        Assert.Equal("Input.CrawlerInputsViewModel.AgentMetadata[options.a]", model.GetFieldName("a"));
    }
}

using KamiYomu.Web.Extensions;

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace KamiYomu.Web.Tests.Extensions;

public class ModelStateExtensionsTests
{
    [Fact]
    public void RemoveWithPrefix_RemovesExactAndChildKeys()
    {
        ModelStateDictionary modelState = new();
        modelState.AddModelError("item", "error");
        modelState.AddModelError("item.name", "error");
        modelState.AddModelError("other.item", "error");

        modelState.RemoveWithPrefix("item");

        Assert.DoesNotContain("item", modelState.Keys);
        Assert.DoesNotContain("item.name", modelState.Keys);
        Assert.Contains("other.item", modelState.Keys);
    }

    [Fact]
    public void RemoveWithSuffix_RemovesExactAndParentKeys()
    {
        ModelStateDictionary modelState = new();
        modelState.AddModelError("name", "error");
        modelState.AddModelError("person.name", "error");
        modelState.AddModelError("name.value", "error");

        modelState.RemoveWithSuffix("name");

        Assert.DoesNotContain("name", modelState.Keys);
        Assert.DoesNotContain("person.name", modelState.Keys);
        Assert.Contains("name.value", modelState.Keys);
    }
}

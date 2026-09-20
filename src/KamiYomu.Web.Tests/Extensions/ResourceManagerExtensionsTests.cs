using System.Globalization;
using System.Resources;

using KamiYomu.Web.Extensions;

namespace KamiYomu.Web.Tests.Extensions;

public class ResourceManagerExtensionsTests
{
    [Fact]
    public void GetStringSafe_ReturnsResourceValue()
    {
        FakeResourceManager resourceManager = new((name, culture) => $"value-{culture!.Name}");
        CultureInfo culture = new("pt-BR");

        string? result = resourceManager.GetStringSafe("Hello", culture);

        Assert.Equal("value-pt-BR", result);
    }

    [Fact]
    public void GetStringSafe_ReturnsNameWhenResourceIsMissingOrWhitespace()
    {
        FakeResourceManager resourceManager = new((name, culture) => " ");

        string? result = resourceManager.GetStringSafe("MissingKey", CultureInfo.InvariantCulture);

        Assert.Equal("MissingKey", result);
    }

    [Fact]
    public void GetStringSafe_ReturnsNameWhenResourceManagerThrows()
    {
        FakeResourceManager resourceManager = new((name, culture) => throw new MissingManifestResourceException());

        string? result = resourceManager.GetStringSafe("MissingKey", CultureInfo.InvariantCulture);

        Assert.Equal("MissingKey", result);
    }

    private sealed class FakeResourceManager(Func<string, CultureInfo?, string?> handler) : ResourceManager
    {
        public override string? GetString(string name, CultureInfo? culture)
        {
            return handler(name, culture);
        }
    }
}

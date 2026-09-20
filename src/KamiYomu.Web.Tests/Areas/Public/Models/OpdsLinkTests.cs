using KamiYomu.Web.Areas.Public.Models;

namespace KamiYomu.Web.Tests.Areas.Public.Models;

public class OpdsLinkTests
{
    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        OpdsLink link = new()
        {
            Href = "/public/api/v1/opds/1",
            Rel = "alternate",
            Type = "application/atom+xml"
        };

        Assert.Equal("/public/api/v1/opds/1", link.Href);
        Assert.Equal("alternate", link.Rel);
        Assert.Equal("application/atom+xml", link.Type);
    }
}

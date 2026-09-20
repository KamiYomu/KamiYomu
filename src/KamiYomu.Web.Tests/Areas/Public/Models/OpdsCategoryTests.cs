using KamiYomu.Web.Areas.Public.Models;

namespace KamiYomu.Web.Tests.Areas.Public.Models;

public class OpdsCategoryTests
{
    [Fact]
    public void Term_CanBeSetAndRetrieved()
    {
        OpdsCategory category = new() { Term = "action" };

        Assert.Equal("action", category.Term);
    }
}

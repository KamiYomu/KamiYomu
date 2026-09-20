using KamiYomu.Web.Filters;

namespace KamiYomu.Web.Tests.Filters;

public class AllowAllDashboardAuthorizationFilterTests
{
    [Fact]
    public void Authorize_AlwaysReturnsTrue()
    {
        AllowAllDashboardAuthorizationFilter filter = new();

        bool result = filter.Authorize(null!);

        Assert.True(result);
    }
}

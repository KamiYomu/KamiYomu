using KamiYomu.Web.HealthCheckers;
using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace KamiYomu.Web.Tests.HealthCheckers;

public class DatabaseHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthyWhenDatabaseIsAvailable()
    {
        using DbContext dbContext = new(":memory:");
        DatabaseHealthCheck healthCheck = new(new Mock<ILogger<CachingHealthCheck>>().Object, dbContext);

        HealthCheckResult result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("Database is operational.", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthyWhenDatabaseThrows()
    {
        // Intentionally not disposed via `using`: the invalid path also makes DbContext.Dispose
        // throw (it lazily (re)opens the same broken Raw database on disposal), which is not what
        // this test is about and would otherwise escape and fail the test after the assertions.
        DbContext dbContext = new("invalid\0path.db");
        DatabaseHealthCheck healthCheck = new(new Mock<ILogger<CachingHealthCheck>>().Object, dbContext);

        HealthCheckResult result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }
}

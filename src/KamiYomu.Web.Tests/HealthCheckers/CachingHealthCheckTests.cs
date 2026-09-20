using KamiYomu.Web.HealthCheckers;
using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using MonkeyCache;
using MonkeyCache.LiteDB;

namespace KamiYomu.Web.Tests.HealthCheckers;

public class CachingHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthyWhenCacheOperationsSucceed()
    {
        string originalApplicationId = Barrel.ApplicationId;
        Barrel.ApplicationId = $"KamiYomuTests-{Guid.NewGuid():N}";
        CachingHealthCheck healthCheck = new(new Mock<ILogger<CachingHealthCheck>>().Object, new CacheContext());

        try
        {
            HealthCheckResult result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Equal("Cache is operational.", result.Description);
        }
        finally
        {
            new CacheContext().EmptyAll();
            Barrel.ApplicationId = originalApplicationId;
        }
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthyWhenCacheAccessFails()
    {
        // MonkeyCache's Barrel.Current is a process-wide singleton that is lazily created once
        // and never rebuilt, so toggling Barrel.ApplicationId after another test has already
        // touched Barrel.Current has no effect. Substitute a fake IBarrel via CacheContext's
        // test-only resolver seam instead, so this test is independent of run order.
        Func<IBarrel> originalResolver = CacheContext.CurrentResolver;
        Mock<IBarrel> barrel = new();
        _ = barrel.Setup(b => b.Add(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<System.Text.Json.JsonSerializerOptions>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("Cache is not available."));
        CacheContext.CurrentResolver = () => barrel.Object;

        CachingHealthCheck healthCheck = new(new Mock<ILogger<CachingHealthCheck>>().Object, new CacheContext());

        try
        {
            HealthCheckResult result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.NotNull(result.Exception);
        }
        finally
        {
            CacheContext.CurrentResolver = originalResolver;
        }
    }
}

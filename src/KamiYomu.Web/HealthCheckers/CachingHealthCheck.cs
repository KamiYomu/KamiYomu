using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KamiYomu.Web.HealthCheckers;

public class CachingHealthCheck(CacheContext cacheContext) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cacheContext.Current.Add("health-check", "operational", TimeSpan.FromSeconds(1));
            string value = cacheContext.Current.Get<string>("health-check");
            return Task.FromResult(HealthCheckResult.Healthy($"Cache is {value}."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Database is not available.", ex));
        }
    }
}

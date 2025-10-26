using KamiYomu.Web.Infrastructure.Contexts;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KamiYomu.Web.HealthCheckers
{
    public class CachingHealthCheck(CacheContext cacheContext) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                cacheContext.Current.Add("health-check", "operational", TimeSpan.FromSeconds(1));
                var result = cacheContext.GetOrSet("health-check", () => "operational", TimeSpan.FromSeconds(1));
                return Task.FromResult(HealthCheckResult.Healthy($"Cache is {result}."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Database is not available.", ex));
            }
        }
    }
}

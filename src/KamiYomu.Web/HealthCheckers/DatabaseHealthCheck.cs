using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.HealthCheckers;

public class DatabaseHealthCheck(ILogger<CachingHealthCheck> logger, [FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _ = dbContext.Raw.GetCollection("system");

            return Task.FromResult(HealthCheckResult.Healthy("Database is operational."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Database is not available.", ex));
        }
    }
}

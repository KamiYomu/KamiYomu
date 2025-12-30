using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KamiYomu.Web.HealthCheckers;

public class DatabaseHealthCheck(DbContext dbContext) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IEnumerable<string> col = dbContext.Raw.GetCollectionNames();

            return Task.FromResult(HealthCheckResult.Healthy("Database is operational."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Database is not available.", ex));
        }
    }
}

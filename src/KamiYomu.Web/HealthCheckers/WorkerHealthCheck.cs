using Hangfire;
using Hangfire.Storage;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KamiYomu.Web.HealthCheckers;

public class WorkerHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            using IStorageConnection connection = JobStorage.Current.GetConnection();

            _ = connection.GetRecurringJobs();

            return Task.FromResult(HealthCheckResult.Healthy("Hangfire is operational."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Hangfire is not available.", ex));
        }
    }
}

using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KamiYomu.Web.HealthCheckers
{
    public class WorkerHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = JobStorage.Current.GetConnection();
                var recurringJobs = connection.GetRecurringJobs();

                return Task.FromResult(HealthCheckResult.Healthy("Hangfire is operational."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Hangfire is not available.", ex));
            }
        }
    }

}

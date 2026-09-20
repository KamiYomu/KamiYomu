using Hangfire;
using Hangfire.Storage;

using KamiYomu.Web.Tests.Extensions;
using KamiYomu.Web.HealthCheckers;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KamiYomu.Web.Tests.HealthCheckers;

[Collection(JobStorageTestCollection.Name)]
public class WorkerHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthyWhenHangfireStorageIsAvailable()
    {
        Mock<IStorageConnection> connection = new();
        Mock<JobStorage> jobStorage = new();
        JobStorage? originalJobStorage = JobStorageTestHelper.GetCurrentOrNull();

        _ = connection.Setup(x => x.GetAllItemsFromSet("recurring-jobs")).Returns(new HashSet<string>());
        _ = jobStorage.Setup(x => x.GetConnection()).Returns(connection.Object);

        JobStorage.Current = jobStorage.Object;

        try
        {
            WorkerHealthCheck healthCheck = new();

            HealthCheckResult result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.Equal("Hangfire is operational.", result.Description);
        }
        finally
        {
            JobStorageTestHelper.Restore(originalJobStorage);
        }
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthyWhenHangfireStorageThrows()
    {
        Mock<JobStorage> jobStorage = new();
        JobStorage? originalJobStorage = JobStorageTestHelper.GetCurrentOrNull();

        _ = jobStorage.Setup(x => x.GetConnection()).Throws(new InvalidOperationException("Hangfire unavailable"));

        JobStorage.Current = jobStorage.Object;

        try
        {
            WorkerHealthCheck healthCheck = new();

            HealthCheckResult result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.NotNull(result.Exception);
        }
        finally
        {
            JobStorageTestHelper.Restore(originalJobStorage);
        }
    }
}

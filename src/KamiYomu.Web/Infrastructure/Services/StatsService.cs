using System.Reflection;

using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;

using KamiYomu.Web.Areas.Public.Models;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Infrastructure.Services;

public class StatsService(
    [FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext,
    CacheContext cacheContext,
    JobStorage jobStorage) : IStatsService
{
    public StatsResponse GetStats()
    {
        return cacheContext.GetOrSet($"{nameof(StatsService)}{nameof(GetStats)}", () =>
        {
            IMonitoringApi monitoring = jobStorage.GetMonitoringApi();
            IStorageConnection connection = jobStorage.GetConnection();
            int activeAgents = monitoring.Servers().Count;
            IList<QueueWithTopEnqueuedJobsDto> queues = monitoring.Queues();
            long queuedTasks = queues.Sum(q => q.Length);
            long failedTasks = monitoring.FailedCount();

            Assembly assembly = typeof(Program).Assembly;
            Assembly coreAssembly = typeof(AbstractCrawlerAgent).Assembly;
            string? coreInformationalVersion = coreAssembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
            string? fullVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            string? version = fullVersion?.Split('+')[0];
            int collectionSize = dbContext.Libraries.Count();

            return new StatsResponse(
                Version: version,
                CoreVersion: coreInformationalVersion,
                CollectionSize: collectionSize,
                WorkerQueuedTasks: queuedTasks,
                WorkerFailedTasks: failedTasks
            );
        }, TimeSpan.FromMinutes(1));
    }
}

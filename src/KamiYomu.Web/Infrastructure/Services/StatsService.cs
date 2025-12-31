using System.Reflection;

using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;

using KamiYomu.Web.Entities.Stats;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

namespace KamiYomu.Web.Infrastructure.Services;

public class StatsService(JobStorage jobStorage, DbContext dbContext) : IStatsService
{
    public StatsResponse GetStats()
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
    }
}

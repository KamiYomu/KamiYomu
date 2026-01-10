using Hangfire;
using Hangfire.States;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Infrastructure.Repositories;

public class HangfireRepository(IOptions<WorkerOptions> options) : IHangfireRepository
{
    public EnqueuedState GetLeastLoadedDownloadChapterQueue()
    {
        Hangfire.Storage.IMonitoringApi monitor = JobStorage.Current.GetMonitoringApi();
        Dictionary<string, long> activeQueues = monitor.Queues().ToDictionary(q => q.Name, q => q.Length);

        var allQueuesWithStats = options.Value.DownloadChapterQueues
            .Select(name => new
            {
                Name = name,
                Length = activeQueues.TryGetValue(name, out long count) ? count : 0
            })
            .ToList();
        return new EnqueuedState(allQueuesWithStats.OrderBy(q => q.Length).First().Name);
    }

    public EnqueuedState GetLeastLoadedMangaDownloadSchedulerQueue()
    {
        Hangfire.Storage.IMonitoringApi monitor = JobStorage.Current.GetMonitoringApi();
        Dictionary<string, long> activeQueues = monitor.Queues().ToDictionary(q => q.Name, q => q.Length);

        var allQueuesWithStats = options.Value.MangaDownloadSchedulerQueues
            .Select(name => new
            {
                Name = name,
                Length = activeQueues.TryGetValue(name, out long count) ? count : 0
            })
            .ToList();
        return new EnqueuedState(allQueuesWithStats.OrderBy(q => q.Length).First().Name);
    }

    public EnqueuedState GetNotifyQueue()
    {
        return new EnqueuedState(Defaults.Worker.NotificationQueue);
    }
}

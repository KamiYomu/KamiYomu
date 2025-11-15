using Hangfire;
using Hangfire.States;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;

namespace KamiYomu.Web.Infrastructure.Repositories
{
    public class HangfireRepository : IHangfireRepository
    {
        public EnqueuedState GetLeastLoadedDownloadChapterQueue()
        {
            var monitor = JobStorage.Current.GetMonitoringApi();
            var activeQueues = monitor.Queues().ToDictionary(q => q.Name, q => q.Length);

            var allQueuesWithStats = Defaults.Worker.DownloadChapterQueues
                .Select(name => new
                {
                    Name = name,
                    Length = activeQueues.TryGetValue(name, out var count) ? count : 0
                })
                .ToList();
            return new EnqueuedState(allQueuesWithStats.OrderBy(q => q.Length).First().Name);
        }

        public EnqueuedState GetLeastLoadedMangaDownloadSchedulerQueue()
        {
            var monitor = JobStorage.Current.GetMonitoringApi();
            var activeQueues = monitor.Queues().ToDictionary(q => q.Name, q => q.Length);

            var allQueuesWithStats = Defaults.Worker.MangaDownloadSchedulerQueues
                .Select(name => new
                {
                    Name = name,
                    Length = activeQueues.TryGetValue(name, out var count) ? count : 0
                })
                .ToList();
            return new EnqueuedState(allQueuesWithStats.OrderBy(q => q.Length).First().Name);
        }

    }
}

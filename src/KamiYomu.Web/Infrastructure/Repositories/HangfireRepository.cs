using Hangfire;
using Hangfire.States;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;

namespace KamiYomu.Web.Infrastructure.Repositories
{
    public class HangfireRepository : IHangfireRepository
    {
        public EnqueuedState GetLeastLoadedCrawlerQueue()
        {
            var monitor = JobStorage.Current.GetMonitoringApi();
            var activeQueues = monitor.Queues().ToDictionary(q => q.Name, q => q.Length);

            var allQueuesWithStats = Settings.Worker.CrawlerQueues
                .Select(name => new
                {
                    Name = name,
                    Length = activeQueues.TryGetValue(name, out var count) ? count : 0
                })
                .ToList();
            return new EnqueuedState(allQueuesWithStats.OrderBy(q => q.Length).First().Name);
        }

        public EnqueuedState GetLeastLoadedSearchQueue()
        {
            var monitor = JobStorage.Current.GetMonitoringApi();
            var activeQueues = monitor.Queues().ToDictionary(q => q.Name, q => q.Length);

            var allQueuesWithStats = Settings.Worker.SearchQueues
                .Select(name => new
                {
                    Name = name,
                    Length = activeQueues.TryGetValue(name, out var count) ? count : 0
                })
                .ToList();
            return new EnqueuedState(allQueuesWithStats.OrderBy(q => q.Length).First().Name);
        }

        public EnqueuedState GetLeastLoadedFetchMangaQueue()
        {
            var monitor = JobStorage.Current.GetMonitoringApi();
            var activeQueues = monitor.Queues().ToDictionary(q => q.Name, q => q.Length);

            var allQueuesWithStats = Settings.Worker.FetchMangaQueues
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

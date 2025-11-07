using Hangfire.States;

namespace KamiYomu.Web.Infrastructure.Repositories.Interfaces
{
    public interface IHangfireRepository
    {
        EnqueuedState GetLeastLoadedCrawlerQueue();
        EnqueuedState GetLeastLoadedSearchQueue();
        EnqueuedState GetLeastLoadedFetchMangaQueue();

    }
}

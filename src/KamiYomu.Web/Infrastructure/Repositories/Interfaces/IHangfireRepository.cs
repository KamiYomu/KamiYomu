using Hangfire.States;

namespace KamiYomu.Web.Infrastructure.Repositories.Interfaces;

public interface IHangfireRepository
{
    EnqueuedState GetLeastLoadedDownloadChapterQueue();
    EnqueuedState GetLeastLoadedMangaDownloadSchedulerQueue();
}

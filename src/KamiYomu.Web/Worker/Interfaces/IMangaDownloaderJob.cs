using Hangfire;
using Hangfire.Server;
using KamiYomu.Web.Worker.Attributes;

namespace KamiYomu.Web.Worker.Interfaces
{
    public interface IMangaDownloaderJob
    {
        [JobDisplayName("Down Manga {3}")]
        [PerKeyConcurrency("crawlerId")]
        Task DispatchAsync(Guid crawlerId, Guid libraryId, Guid mangaDownloadId, string title, PerformContext context, CancellationToken cancellationToken);
    }
}

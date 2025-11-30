using Hangfire;
using Hangfire.Server;
using KamiYomu.Web.Worker.Attributes;

namespace KamiYomu.Web.Worker.Interfaces
{
    public interface IMangaDownloaderJob
    {
        [Queue("{0}")]
        [JobDisplayName("Down Manga {4}")]
        [PerKeyConcurrency("crawlerId")]
        [MangaCancelOnFail("libraryId")]
        Task DispatchAsync(string queue, Guid crawlerId, Guid libraryId, Guid mangaDownloadId, string title, PerformContext context, CancellationToken cancellationToken);
    }
}

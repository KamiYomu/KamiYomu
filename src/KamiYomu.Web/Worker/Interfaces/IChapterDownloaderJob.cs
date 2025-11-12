using Hangfire;
using Hangfire.Server;
using KamiYomu.Web.Worker.Attributes;
using System.ComponentModel;

namespace KamiYomu.Web.Worker.Interfaces
{
    public interface IChapterDownloaderJob
    {
        [PerKeyConcurrency("crawlerId", 5)]
        [DisplayName("Down Chapter {4}")]
        Task DispatchAsync(Guid crawlerId, Guid libraryId, Guid mangaDownloadId, Guid chapterDownloadId, string title, PerformContext context, CancellationToken cancellationToken);
    }
}

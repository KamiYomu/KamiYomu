using Hangfire;
using Hangfire.Server;
using KamiYomu.Web.Worker.Attributes;
using System.ComponentModel;

namespace KamiYomu.Web.Worker.Interfaces
{
    public interface IChapterDownloaderJob
    {
        [Queue("{0}")]
        [PerKeyConcurrency("crawlerId")]
        [DisplayName("Down Chapter {5}")]
        Task DispatchAsync(string queue, Guid crawlerId, Guid libraryId, Guid mangaDownloadId, Guid chapterDownloadId, string title, PerformContext context, CancellationToken cancellationToken);
    }
}

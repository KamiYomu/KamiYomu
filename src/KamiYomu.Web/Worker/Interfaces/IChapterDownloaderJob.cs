using Hangfire;
using Hangfire.Server;
using System.ComponentModel;

namespace KamiYomu.Web.Worker.Interfaces
{
    public interface IChapterDownloaderJob
    {
        [DisplayName("Down Chapter {3}")]
        Task DispatchAsync(Guid libraryId, Guid mangaDownloadId, Guid chapterDownloadId, string title, PerformContext context, CancellationToken cancellationToken);
    }
}

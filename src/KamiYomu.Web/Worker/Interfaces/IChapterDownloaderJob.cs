using Hangfire;
using Hangfire.Server;

using KamiYomu.Web.Worker.Attributes;

using System.ComponentModel;

namespace KamiYomu.Web.Worker.Interfaces;

public interface IChapterDownloaderJob
{
    [Queue("{0}")]
    [DisplayName("Down Chapter {5}")]
    [PerKeyConcurrency("crawlerAgentId")]
    [ChapterCancelOnFail("libraryId", "title")]
    Task DispatchAsync(string queue, Guid crawlerAgentId, Guid libraryId, Guid mangaDownloadId, Guid chapterDownloadId, string title, PerformContext context, CancellationToken cancellationToken);
}

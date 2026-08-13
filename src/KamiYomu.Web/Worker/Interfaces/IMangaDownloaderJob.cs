using Hangfire;
using Hangfire.Server;

using KamiYomu.Web.Worker.Attributes;

namespace KamiYomu.Web.Worker.Interfaces;
/// <summary>
/// Defines the contract for a manga downloader job.
/// </summary>
public interface IMangaDownloaderJob
{
    /// <summary>
    /// Dispatches a manga download job to the specified queue for asynchronous processing.
    /// </summary>
    /// <param name="queue">The name of the queue to dispatch the job to.</param>
    /// <param name="crawlerId">The unique identifier of the crawler instance.</param>
    /// <param name="libraryId">The unique identifier of the manga library.</param>
    /// <param name="mangaDownloadId">The unique identifier of the manga download request.</param>
    /// <param name="title">The title of the manga to download.</param>
    /// <param name="context">The context for the job execution.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A task representing the asynchronous dispatch operation.</returns>
    [Queue("{0}")]
    [JobDisplayName("Down Manga {4}")]
    [PerKeyConcurrency("crawlerId")]
    [MangaCancelOnFail("libraryId", "title")]
    Task DispatchAsync(string queue, Guid crawlerId, Guid libraryId, Guid mangaDownloadId, string title, PerformContext context, CancellationToken cancellationToken);
}

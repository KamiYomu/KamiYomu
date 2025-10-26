using Hangfire;
using Hangfire.Server;
using System.ComponentModel;

namespace KamiYomu.Web.Worker.Interfaces
{
    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    public interface IMangaDownloaderJob
    {
        [JobDisplayName("Down Manga {2}")]
        Task DispatchAsync(Guid libraryId, Guid mangaDownloadId, string title, PerformContext context, CancellationToken cancellationToken);
    }
}

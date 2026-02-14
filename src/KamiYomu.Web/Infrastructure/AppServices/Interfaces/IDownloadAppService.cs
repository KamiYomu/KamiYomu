using KamiYomu.Web.Entities;
using KamiYomu.Web.Models;

namespace KamiYomu.Web.Infrastructure.AppServices.Interfaces;

public interface IDownloadAppService
{
    Task<Library> AddToCollectionAsync(AddItemCollection addItemCollection, CancellationToken cancellationToken);
    Task<Library> RemoveFromCollectionAsync(RemoveItemCollection removeItemCollection, CancellationToken cancellationToken);
    Task<ChapterDownloadRecord?> CancelAsync(Guid libraryId, Guid chapterDownloadId, CancellationToken cancellationToken);
    Task<ChapterDownloadRecord?> RescheduleAsync(Guid libraryId, Guid chapterDownloadId, CancellationToken cancellationToken);
}

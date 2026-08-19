using KamiYomu.Web.Entities;
using KamiYomu.Web.Models;

namespace KamiYomu.Web.Infrastructure.AppServices.Interfaces;

/// <summary>
/// DownloadAppService is responsible for managing manga downloads, 
/// including adding and removing items from the collection, 
/// as well as handling chapter download records. It interacts with the database context, 
/// crawler agent repository, worker service, Hangfire repository, and notification service to perform its operations.
/// </summary>
public interface IDownloadAppService
{
    /// <summary>
    /// Adds a manga item to the collection based on the provided AddItemCollection model.
    /// </summary>
    /// <param name="addItemCollection"></param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns></returns>
    Task<Library> AddToCollectionAsync(AddItemCollection addItemCollection, CancellationToken cancellationToken);


    /// <summary>
    /// Removes a manga item from the collection based on the provided RemoveItemCollection model.
    /// </summary>
    /// <param name="removeItemCollection"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Library> RemoveFromCollectionAsync(RemoveItemCollection removeItemCollection, CancellationToken cancellationToken);
    /// <summary>
    /// Cancels a chapter download record based on the provided libraryId and chapterDownloadId.
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="chapterDownloadId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ChapterDownloadRecord?> CancelAsync(Guid libraryId, Guid chapterDownloadId, CancellationToken cancellationToken);
    /// <summary>
    /// Reschedules a chapter download record based on the provided libraryId and chapterDownloadId.
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="chapterDownloadId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ChapterDownloadRecord?> RescheduleAsync(Guid libraryId, Guid chapterDownloadId, CancellationToken cancellationToken);
}

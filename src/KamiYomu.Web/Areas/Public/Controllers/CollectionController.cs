using System.Net.Mime;

using KamiYomu.Web.Areas.Public.Models;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Infrastructure.AppServices.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Models;

using LiteDB;

using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Areas.Public.Controllers;

[Area(nameof(Public))]
[Route("[area]/api/v{version:apiVersion}/[controller]")]
[ApiController]
[SwaggerTag(description: "Provides access to the user's public collection, including listing collection " +
                  "items and retrieving individual entries. Routes are versioned and organized " +
                  "under the Public area."
)]
public class CollectionController : ControllerBase
{
    [HttpGet]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(IEnumerable<CollectionItem>), StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "List collection items",
        Description = "Returns a paginated list of items in the user's collection. Supports optional "
                    + "search filtering by manga title, as well as offset and limit parameters for "
                    + "pagination."
    )]
    public IActionResult List(
        [FromQuery] string? search,
        [FromQuery] int offSet = 0,
        [FromQuery] int limit = 20,
        [FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext = default!)
    {
        ILiteQueryable<Library> query = dbContext.Libraries.Query();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => p.Manga.Title.Contains(search));
        }

        return Ok(query.Offset(offSet)
                    .Limit(limit)
                    .ToList()
                    .Select(p =>
                    new CollectionItem
                    {
                        CrawlerAgentId = p.CrawlerAgent.Id,
                        LibraryId = p.Id,
                        Manga = p.Manga
                    }));
    }

    [HttpGet]
    [Route("{libraryId:guid}")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(CollectionItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(
    Summary = "Get a collection item",
    Description = "Retrieves a single collection item by its library identifier. Returns detailed "
                + "information about the manga and its associated crawler agent. Returns 404 if "
                + "the item does not exist."
    )]
    public IActionResult Get(
        [FromRoute] Guid libraryId,
        [FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext)
    {
        Library library = dbContext.Libraries.Query().Where(p => p.Id == libraryId).FirstOrDefault();

        return library == null
            ? NotFound()
            : Ok(new CollectionItem
            {
                CrawlerAgentId = library.CrawlerAgent.Id,
                LibraryId = library.Id,
                Manga = library.Manga
            });
    }


    [HttpDelete]
    [Route("{libraryId:guid}")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(CollectionItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(
    Summary = "Remove a collection item",
    Description = "Removes the specified item from the user's collection. Returns 200 when the item "
                + "is successfully removed, or 404 if the collection item does not exist."
    )]
    public async Task<IActionResult> RemoveAsync(
        [FromRoute] Guid libraryId,
        [FromServices] IDownloadAppService downloadAppService,
        [FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext,
        CancellationToken cancellationToken)
    {
        Library library = dbContext.Libraries.Query().Where(p => p.Id == libraryId).FirstOrDefault();

        if (library == null)
        {
            return NotFound();
        }

        _ = await downloadAppService.RemoveFromCollectionAsync(new RemoveItemCollection
        {
            CrawlerAgentId = library.CrawlerAgent.Id,
            MangaId = library.Manga.Id
        }, cancellationToken);

        return Ok();
    }

    [HttpGet]
    [Route("{libraryId:guid}/chapters")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(IEnumerable<ChapterItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(
    Summary = "List chapters for a collection item",
    Description = "Returns a paginated list of chapter download records associated with the specified "
                + "library. Includes chapter metadata, download status, timestamps, and related "
                + "information. Returns 404 if the library does not exist."
    )]
    public IActionResult GetChapters(
        [FromRoute] Guid libraryId,
        [FromQuery] int offSet = 0,
        [FromQuery] int limit = 20,
        [FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext = default!)
    {
        Library library = dbContext.Libraries.Query().Where(p => p.Id == libraryId).FirstOrDefault();

        if (library == null)
        {
            return NotFound();
        }

        using LibraryDbContext libContext = library.GetReadOnlyDbContext();

        List<ChapterDownloadRecord> chapters = libContext.ChapterDownloadRecords
                  .Query()
                  .Where(p => p.MangaDownload.Library.Id == libraryId)
                  .Skip(offSet)
                  .Limit(limit)
                  .ToList();

        return Ok(chapters.Select(p => ChapterItem.Create(libraryId, p)));

    }

    [HttpGet]
    [Route("{libraryId:guid}/chapters/{chapterDownloadId:guid}")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(ChapterItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(
    Summary = "List chapters for a collection item",
    Description = "Returns a paginated list of chapter download records associated with the specified "
                + "library. Includes chapter metadata, download status, timestamps, and related "
                + "information. Returns 404 if the library does not exist."
    )]
    public IActionResult GetChapter(
        [FromRoute] Guid libraryId,
        [FromRoute] Guid chapterDownloadId,
        [FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext = default!)
    {
        Library library = dbContext.Libraries.Query().Where(p => p.Id == libraryId).FirstOrDefault();

        if (library == null)
        {
            return NotFound();
        }

        using LibraryDbContext libContext = library.GetReadOnlyDbContext();

        ChapterDownloadRecord chapter = libContext.ChapterDownloadRecords
                  .Query()
                  .Where(p => p.MangaDownload.Library.Id == libraryId && p.Id == chapterDownloadId)
                  .FirstOrDefault();

        return chapter == null ? NotFound() : Ok(ChapterItem.Create(libraryId, chapter));
    }

    [HttpGet]
    [Route("{libraryId:guid}/available-chapters")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(IEnumerable<ChapterItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(
    Summary = "List available chapters for a collection item",
    Description = "Returns a paginated list of chapters that have completed downloading for the "
                + "specified library. Only chapters with a Completed download status are included. "
                + "Returns 404 if the library does not exist."
    )]
    public IActionResult GetChaptersAvailable(
        [FromRoute] Guid libraryId,
        [FromQuery] int offSet = 0,
        [FromQuery] int limit = 20,
        [FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext = default!)
    {
        Library library = dbContext.Libraries.Query().Where(p => p.Id == libraryId).FirstOrDefault();

        if (library == null)
        {
            return NotFound();
        }

        using LibraryDbContext libContext = library.GetReadOnlyDbContext();

        List<ChapterDownloadRecord> chapters = libContext.ChapterDownloadRecords
                  .Query()
                  .Where(p => p.MangaDownload.Library.Id == libraryId
                  && (int)(object)p.DownloadStatus == (int)(object)DownloadStatus.Completed)
                  .Skip(offSet)
                  .Limit(limit)
                  .ToList();

        return Ok(chapters.Select(p => ChapterItem.Create(libraryId, p)));
    }


    [HttpPatch]
    [Route("{libraryId:guid}/chapters/{chapterDownloadId:guid}/reschedule")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(ChapterItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(
    Summary = "Reschedule a chapter download",
    Description = "Reschedules the download job for the specified chapter within the given library. "
                + "Returns the updated chapter information after rescheduling, or 404 if the chapter "
                + "or library does not exist."
    )]
    public async Task<IActionResult> RescheduleChapterAsync(
        [FromRoute] Guid libraryId,
        [FromRoute] Guid chapterDownloadId,
        [FromServices] IDownloadAppService downloadAppService,
        CancellationToken cancellationToken)
    {
        ChapterDownloadRecord? chapterDownloadRecord = await downloadAppService.RescheduleAsync(libraryId, chapterDownloadId, cancellationToken);

        return chapterDownloadRecord == null ? NotFound() : Ok(ChapterItem.Create(libraryId, chapterDownloadRecord));
    }

    [HttpPatch]
    [Route("{libraryId:guid}/chapters/{chapterDownloadId:guid}/cancel")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(ChapterItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(
    Summary = "Cancel a chapter download",
    Description = "Cancels the download job for the specified chapter within the given library. "
                + "Returns the updated chapter information after cancellation, or 404 if the chapter "
                + "or library does not exist."
    )]
    public async Task<IActionResult> CancelChapterAsync(
        [FromRoute] Guid libraryId,
        [FromRoute] Guid chapterDownloadId,
        [FromServices] IDownloadAppService downloadAppService,
        CancellationToken cancellationToken)
    {
        ChapterDownloadRecord? chapterDownloadRecord = await downloadAppService.CancelAsync(libraryId, chapterDownloadId, cancellationToken);

        return chapterDownloadRecord == null ? NotFound() : Ok(ChapterItem.Create(libraryId, chapterDownloadRecord));
    }
}

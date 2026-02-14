using System.ComponentModel.DataAnnotations;
using System.Net.Mime;

using KamiYomu.Web.Areas.Public.Models;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Models;

using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Areas.Public.Controllers;

[Area(nameof(Public))]
[Route("[area]/api/v{version:apiVersion}/[controller]")]
[ApiController]
[SwaggerTag(description: "Provides OPDS feeds and download endpoints for the public catalog, including " +
                  "manga libraries, chapters, and acquisition links. Routes are versioned and " +
                  "exposed under the Public area."
)]
public class OpdsController(
    [FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext) : ControllerBase
{
    [HttpGet]
    [Produces(MediaTypeNames.Text.Xml)]
    [ProducesResponseType(typeof(AtomXmlResult<OpdsFeed>), StatusCodes.Status200OK)]
    [SwaggerOperation(
    Summary = "List manga libraries (OPDS)",
    Description = "Returns an OPDS feed containing a paginated list of available manga libraries. "
                + "Supports page and pageSize parameters for navigation."
    )]
    public async Task<IActionResult> GetMangaList(
     [FromQuery, Range(1, int.MaxValue, ErrorMessage = "Page must be at least 1")] int page = 1,
     [FromQuery, Range(1, 100, ErrorMessage = "Page size must be between 1 and 100")] int pageSize = 20)
    {
        pageSize = Math.Min(pageSize, 100);

        int totalCount = dbContext.Libraries.Count();
        int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        List<Library> libraries = dbContext.Libraries
            .Query()
            .Offset((page - 1) * pageSize)
            .Limit(pageSize)
            .ToList();

        OpdsFeed feed = new()
        {
            Id = $"urn:opds:manga:list:page:{page}",
            Title = I18n.KamiYomuCatalog,
            Updated = DateTime.UtcNow,
            Icon = "/images/favicon.png",
            Links = [
                new OpdsLink
                {
                    Href = "/images/logo.svg", // URL to the cover image
                    Rel = "http://opds-spec.org/image",
                    Type = MediaTypeNames.Image.Svg
                },
                new OpdsLink
                {
                    Href = "/images/logo.svg",
                    Rel = "http://opds-spec.org/image/thumbnail",
                    Type = MediaTypeNames.Image.Svg
                }
            ]
        };

        foreach (Library library in libraries)
        {
            feed.Entries.Add(OpdsEntry.Create(library));
        }

        string baseUrl = "/public/api/v1/opds";

        if (page > 1)
        {
            feed.Links.Add(new OpdsLink { Href = $"{baseUrl}?page={page - 1}&pageSize={pageSize}", Rel = "previous", Type = "application/atom+xml" });
        }

        if (page < totalPages)
        {
            feed.Links.Add(new OpdsLink { Href = $"{baseUrl}?page={page + 1}&pageSize={pageSize}", Rel = "next", Type = "application/atom+xml" });
        }

        feed.Links.Add(new OpdsLink { Href = $"{baseUrl}?page=1&pageSize={pageSize}", Rel = "first", Type = "application/atom+xml" });
        feed.Links.Add(new OpdsLink { Href = $"{baseUrl}?page={totalPages}&pageSize={pageSize}", Rel = "last", Type = "application/atom+xml" });

        return new AtomXmlResult<OpdsFeed>(feed);
    }

    [HttpGet("{libraryId:guid}")]
    [Produces(MediaTypeNames.Text.Xml)]
    [ProducesResponseType(typeof(AtomXmlResult<OpdsFeed>), StatusCodes.Status200OK)]
    [SwaggerOperation(
    Summary = "Get manga library (OPDS)",
    Description = "Returns an OPDS feed representing the specified manga library, including "
                + "its metadata and available manga entries."
    )]
    public async Task<IActionResult> GetManga([FromRoute] Guid libraryId)
    {
        Library library = dbContext.Libraries
            .Query()
            .Where(l => l.Id == libraryId)
            .FirstOrDefault();

        if (library == null)
        {
            return NotFound();
        }

        using LibraryDbContext libraryContext = library.GetReadOnlyDbContext();

        IEnumerable<ChapterDownloadRecord> chapterDownloadRecord = libraryContext.ChapterDownloadRecords
            .Query()
            .Where(r => r.MangaDownload.Library.Id == libraryId &&
                        (int)(object)r.DownloadStatus == (int)(object)DownloadStatus.Completed)
            .OrderBy(p => p.Chapter.Number)
            .ToList();

        OpdsFeed feed = new()
        {
            Id = $"urn:opds:manga:{library.Id}",
            Title = library.Manga.Title,
            Icon = "/images/favicon.png",
            Updated = DateTime.UtcNow,
            Links = [
                new OpdsLink
                {
                    Href = library.Manga.CoverUrl.ToInternalImageUrl().ToString(), // URL to the cover image
                    Rel = "http://opds-spec.org/image",
                    Type = MediaTypeNames.Image.Jpeg
                },
                new OpdsLink
                {
                    Href = library.Manga.CoverUrl.ToInternalImageUrl().ToString(),
                    Rel = "http://opds-spec.org/image/thumbnail",
                    Type = MediaTypeNames.Image.Jpeg
                }
            ]
        };

        feed.Entries.AddRange(OpdsEntry.CreateChapterEntries(library, chapterDownloadRecord));

        return new AtomXmlResult<OpdsFeed>(feed);
    }


    [HttpGet("{libraryId:guid}/chapters/{chapterDownloadId:guid}")]
    [Produces(MediaTypeNames.Text.Xml)]
    [ProducesResponseType(typeof(AtomXmlResult<OpdsFeed>), StatusCodes.Status200OK)]
    [SwaggerOperation(
    Summary = "Get chapter details (OPDS)",
    Description = "Returns an OPDS feed describing the specified chapter, including metadata "
                + "and available acquisition links for downloading the chapter in various formats."
    )]
    public IActionResult GetChapter(
        [FromRoute] Guid libraryId,
        [FromRoute] Guid chapterDownloadId)
    {
        Library library = dbContext.Libraries
            .Query()
            .Where(l => l.Id == libraryId)
            .FirstOrDefault();

        if (library == null)
        {
            return NotFound();
        }

        using LibraryDbContext libraryContext = library.GetReadOnlyDbContext();

        ChapterDownloadRecord chapterDownloadRecord = libraryContext.ChapterDownloadRecords
            .Query()
            .Where(r => r.Id == chapterDownloadId)
            .FirstOrDefault();

        if (chapterDownloadRecord == null)
        {
            return NotFound();
        }

        OpdsFeed feed = new()
        {
            Id = $"urn:opds:manga:{library.Id}:chapter:{chapterDownloadId}",
            Title = library.GetComicInfoTitleTemplateResolved(chapterDownloadRecord.Chapter),
            Updated = chapterDownloadRecord.StatusUpdateAt.Value.ToLocalTime().DateTime
        };

        feed.Entries.Add(OpdsEntry.Create(library, chapterDownloadRecord));

        return new AtomXmlResult<OpdsFeed>(feed);
    }



    [HttpGet("{libraryId:guid}/chapters/{chapterDownloadId:guid}/download/epub")]
    [Produces("application/epub+zip", "application/epub")]
    [SwaggerOperation(
    Summary = "Download chapter as EPUB",
    Description = "Generates and returns the specified chapter in EPUB format using the EPUB service."
    )]
    public IActionResult DownloadChapterEpub(
        [FromRoute] Guid libraryId,
        [FromRoute] Guid chapterDownloadId,
        [FromServices] IEpubService epubService)
    {
        return epubService.GetDownloadResponse(libraryId, chapterDownloadId) is not DownloadResponse downloadResponse
             ? NotFound()
             : File(downloadResponse.Content, downloadResponse.ContentType, downloadResponse.FileName);
    }

    [HttpGet("{libraryId:guid}/chapters/{chapterDownloadId:guid}/download/cbz")]
    [Produces("application/vnd.comicbook+zip", "application/x-cbz")]
    [SwaggerOperation(
    Summary = "Download chapter as CBZ",
    Description = "Generates and returns the specified chapter in CBZ format (ComicBook ZIP)."
    )]
    public IActionResult DownloadChapterCbz(
        [FromRoute] Guid libraryId,
        [FromRoute] Guid chapterDownloadId,
        [FromServices] IZipService zipService)
    {
        return zipService.GetDownloadCbzResponse(libraryId, chapterDownloadId) is not DownloadResponse downloadResponse
             ? NotFound()
             : File(downloadResponse.Content, downloadResponse.ContentType, downloadResponse.FileName);
    }

    [HttpGet("{libraryId:guid}/chapters/{chapterDownloadId:guid}/download/zip")]
    [Produces(MediaTypeNames.Application.Zip)]
    [SwaggerOperation(
    Summary = "Download chapter as ZIP",
    Description = "Returns the specified chapter packaged as a standard ZIP archive."
    )]
    public IActionResult DownloadChapterZip(
        [FromRoute] Guid libraryId,
        [FromRoute] Guid chapterDownloadId,
        [FromServices] IZipService zipService)
    {
        return zipService.GetDownloadZipResponse(libraryId, chapterDownloadId) is not DownloadResponse downloadResponse
             ? NotFound()
             : File(downloadResponse.Content, downloadResponse.ContentType, downloadResponse.FileName);
    }

    [HttpGet("{libraryId:guid}/chapters/{chapterDownloadId:guid}/download/pdf")]
    [Produces(MediaTypeNames.Application.Pdf)]
    [SwaggerOperation(
    Summary = "Download chapter as PDF",
    Description = "Generates and returns the specified chapter in PDF format using the PDF service."
    )]
    public IActionResult DownloadChapterZip(
        [FromRoute] Guid libraryId,
        [FromRoute] Guid chapterDownloadId,
        [FromServices] IPdfService pdfService)
    {
        return pdfService.GetDownloadResponse(libraryId, chapterDownloadId) is not DownloadResponse downloadResponse
             ? NotFound()
             : File(downloadResponse.Content, downloadResponse.ContentType, downloadResponse.FileName);
    }
}

using System.ComponentModel.DataAnnotations;
using System.IO.Compression;
using System.Text.Json;

using KamiYomu.Web.Areas.Public.Models;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.AspNetCore.Mvc;

using NuGet.Packaging;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Areas.Public.Controllers;

[Area(nameof(Public))]
[Route("[area]/api/v{version:apiVersion}/[controller]")]
[ApiController]
public class OpdsController([FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext) : ControllerBase
{
    [HttpGet]
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
            Title = "Manga List",
            Updated = DateTime.UtcNow
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
    public async Task<IActionResult> GetManga(Guid libraryId)
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
            Updated = DateTime.UtcNow
        };

        feed.Entries.AddRange(OpdsEntry.CreateChapterEntries(library, chapterDownloadRecord));

        return new AtomXmlResult<OpdsFeed>(feed);
    }


    [HttpGet("{libraryId:guid}/chapters/{chapterDownloadId:guid}")]
    public IActionResult GetChapter(Guid libraryId, Guid chapterDownloadId)
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


    [HttpGet("{libraryId:guid}/chapters/{chapterDownloadId:guid}/download")]
    public IActionResult DownloadChapter(Guid libraryId, Guid chapterDownloadId, [FromQuery] string format = "comic")
    {
        Library library = dbContext.Libraries
            .Query()
            .Where(l => l.Id == libraryId)
            .FirstOrDefault();

        if (library == null)
            return NotFound();

        using LibraryDbContext libraryContext = library.GetReadOnlyDbContext();
        ChapterDownloadRecord chapterDownloadRecord = libraryContext.ChapterDownloadRecords
            .Query()
            .Where(r => r.Id == chapterDownloadId && (int)(object)r.DownloadStatus == (int)(object)DownloadStatus.Completed)
            .FirstOrDefault();

        if (chapterDownloadRecord == null)
            return NotFound();

        string filePath = library.GetCbzFilePath(chapterDownloadRecord.Chapter);
        if (!System.IO.File.Exists(filePath))
            return NotFound();

        string mimeType = format.ToLower() switch
        {
            "zip" => "application/zip",
            "comic" => "application/vnd.comicbook+zip",
            _ => "application/octet-stream"
        };

        string fileName = Path.GetFileName(filePath);
        if (format.ToLower() == "zip")
        {
            fileName = Path.ChangeExtension(fileName, ".zip");
        }

        return PhysicalFile(filePath, mimeType, fileName);
    }
    [HttpGet("{libraryId:guid}/chapters/{chapterDownloadId:guid}/download/epub")]
    public IActionResult DownloadChapterEpub(Guid libraryId, Guid chapterDownloadId)
    {
        Library library = dbContext.Libraries
            .Query()
            .Where(l => l.Id == libraryId)
            .FirstOrDefault();

        if (library == null)
            return NotFound();

        using LibraryDbContext libraryContext = library.GetReadOnlyDbContext();
        ChapterDownloadRecord chapterDownloadRecord = libraryContext.ChapterDownloadRecords
            .Query()
            .Where(r => r.Id == chapterDownloadId && (int)(object)r.DownloadStatus == (int)(object)DownloadStatus.Completed)
            .FirstOrDefault();

        if (chapterDownloadRecord == null)
            return NotFound();

        string cbzFilePath = library.GetCbzFilePath(chapterDownloadRecord.Chapter);
        if (!System.IO.File.Exists(cbzFilePath))
            return NotFound();

        var outputStream = new MemoryStream();
        var pageNames = new List<string>();

        // Create EPUB archive (leaveOpen: false so the ZIP is finalized when disposed)
        using (var epubArchive = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // 1. Add mimetype file (must be first and uncompressed)
            var mimeEntry = epubArchive.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var writer = new StreamWriter(mimeEntry.Open()))
            {
                writer.Write("application/epub+zip");
            }

            // 2. Add META-INF/container.xml
            var containerEntry = epubArchive.CreateEntry("META-INF/container.xml", CompressionLevel.Fastest);
            using (var writer = new StreamWriter(containerEntry.Open()))
            {
                writer.Write(@"<?xml version=""1.0""?>
<container version=""1.0"" xmlns=""urn:oasis:names:tc:opendocument:xmlns:container"">
  <rootfiles>
    <rootfile full-path=""OEBPS/content.opf"" media-type=""application/oebps-package+xml""/>
  </rootfiles>
</container>");
            }

            // 3. Add images from CBZ to OEBPS/images
            using (var cbzArchive = ZipFile.OpenRead(cbzFilePath))
            {
                int pageNumber = 1;
                string coverFileName = null;

                foreach (var entry in cbzArchive.Entries.OrderBy(p => p.FullName))
                {
                    if (!entry.FullName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) &&
                        !entry.FullName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) &&
                        !entry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                        !entry.FullName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                        continue;

                    bool isCover = entry.FullName.Contains("cover", StringComparison.OrdinalIgnoreCase);
                    string ext = Path.GetExtension(entry.FullName).ToLower();
                    string pageFileName = isCover ? "images/cover" + ext : $"images/page{pageNumber:D3}{ext}";

                    if (!isCover)
                    {
                        pageNames.Add(pageFileName);
                        pageNumber++;
                    }
                    else
                    {
                        coverFileName = pageFileName;
                    }

                    var zipEntry = epubArchive.CreateEntry($"OEBPS/{pageFileName}", CompressionLevel.Fastest);
                    using var entryStream = entry.Open();
                    using var zipEntryStream = zipEntry.Open();
                    entryStream.CopyTo(zipEntryStream);
                }

                // 4. Create content.opf
                var contentOpfEntry = epubArchive.CreateEntry("OEBPS/content.opf", CompressionLevel.Fastest);
                using (var writer = new StreamWriter(contentOpfEntry.Open()))
                {
                    writer.Write($@"<?xml version=""1.0"" encoding=""UTF-8""?>
<package xmlns=""http://www.idpf.org/2007/opf"" version=""2.0"" unique-identifier=""BookId"">
  <metadata xmlns:dc=""http://purl.org/dc/elements/1.1/"">
    <dc:title>{library.GetComicInfoTitleTemplateResolved(chapterDownloadRecord.Chapter)}</dc:title>
    <dc:language>{library.Manga.OriginalLanguage}</dc:language>
    <dc:identifier id=""BookId"">{chapterDownloadRecord.Id}</dc:identifier>
    {(coverFileName != null ? $@"<meta name=""cover"" content=""cover-image""/>" : "")}
  </metadata>
  <manifest>
    {(coverFileName != null ? $@"<item id=""cover-image"" href=""{coverFileName}"" media-type=""image/{Path.GetExtension(coverFileName).TrimStart('.')}""></item>" : "")}
    {string.Join("\n", pageNames.Select(p => $@"<item id=""{Path.GetFileNameWithoutExtension(p)}"" href=""{p}"" media-type=""image/{Path.GetExtension(p).TrimStart('.')}""/>"))}
    <item id=""ncx"" href=""toc.ncx"" media-type=""application/x-dtbncx+xml""/>
  </manifest>
  <spine toc=""ncx"">
    {(coverFileName != null ? $@"<itemref idref=""cover-image""/>" : "")}
    {string.Join("\n", pageNames.Select(p => $@"<itemref idref=""{Path.GetFileNameWithoutExtension(p)}""/>"))}
  </spine>
</package>");
                }

                // 5. Create toc.ncx
                var tocEntry = epubArchive.CreateEntry("OEBPS/toc.ncx", CompressionLevel.Fastest);
                using (var writer = new StreamWriter(tocEntry.Open()))
                {
                    writer.Write($@"<?xml version=""1.0"" encoding=""UTF-8""?>
<ncx xmlns=""http://www.daisy.org/z3986/2005/ncx/"" version=""2005-1"">
  <head>
    <meta name=""dtb:uid"" content=""{chapterDownloadRecord.Id}""/>
  </head>
  <docTitle><text>{library.GetComicInfoTitleTemplateResolved(chapterDownloadRecord.Chapter)}</text></docTitle>
  <navMap>
    {string.Join("\n", pageNames.Select((p, i) => $@"<navPoint id=""navPoint-{i + 1}"" playOrder=""{i + 1}""><navLabel><text>Page {i + 1}</text></navLabel><content src=""{p}""/></navPoint>"))}
  </navMap>
</ncx>");
                }
            }
        }

        // Rewind the stream after the ZIP is fully disposed
        outputStream.Position = 0;
        string downloadFileName = $"{library.GetComicInfoTitleTemplateResolved(chapterDownloadRecord.Chapter)}.epub";
        return File(outputStream, "application/epub+zip", downloadFileName);
    }



}

using System.IO.Compression;

using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Models;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Infrastructure.Services;

public class EpubService([FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext) : IEpubService
{
    public DownloadResponse? GetDownloadResponse(Guid libraryId, Guid chapterDownloadId)
    {
        Library library = dbContext.Libraries.Query().Where(l => l.Id == libraryId).FirstOrDefault();
        if (library == null)
        {
            return null;
        }

        using LibraryDbContext libraryContext = library.GetReadOnlyDbContext();
        ChapterDownloadRecord chapterDownloadRecord = libraryContext.ChapterDownloadRecords.Query()
            .Where(r => r.Id == chapterDownloadId && (int)(object)r.DownloadStatus == (int)(object)DownloadStatus.Completed)
            .FirstOrDefault();

        if (chapterDownloadRecord == null)
        {
            return null;
        }

        string cbzFilePath = library.GetCbzFilePath(chapterDownloadRecord.Chapter);
        if (!File.Exists(cbzFilePath))
        {
            return null;
        }

        MemoryStream outputStream = new();

        // Tracking lists for the manifest/spine
        List<(string id, string href, string type)> images = [];
        List<(string id, string href)> pages = [];

        using (ZipArchive epubArchive = new(outputStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // 1. Mimetype (Must be first, uncompressed)
            ZipArchiveEntry mimeEntry = epubArchive.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (StreamWriter writer = new(mimeEntry.Open()))
            {
                writer.Write("application/epub+zip");
            }

            // 2. Container
            ZipArchiveEntry containerEntry = epubArchive.CreateEntry("META-INF/container.xml");
            using (StreamWriter writer = new(containerEntry.Open()))
            {
                writer.Write(@"<?xml version=""1.0""?><container version=""1.0"" xmlns=""urn:oasis:names:tc:opendocument:xmlns:container""><rootfiles><rootfile full-path=""OEBPS/content.opf"" media-type=""application/oebps-package+xml""/></rootfiles></container>");
            }

            using ZipArchive cbzArchive = ZipFile.OpenRead(cbzFilePath);
            List<ZipArchiveEntry> entries = [.. cbzArchive.Entries
                .Where(e => new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(Path.GetExtension(e.FullName).ToLower()))
                .OrderBy(e => e.FullName)];

            int i = 0;
            foreach (ZipArchiveEntry? entry in entries)
            {
                string ext = Path.GetExtension(entry.FullName).ToLower();
                string imgId = $"img_{i}";
                string imgHref = $"images/page_{i}{ext}";
                string pageId = $"page_{i}";
                string pageHref = $"text/page_{i}.xhtml";

                // Add Image to Zip
                ZipArchiveEntry imgEntry = epubArchive.CreateEntry($"OEBPS/{imgHref}", CompressionLevel.Fastest);
                using (Stream s = entry.Open())
                {
                    using Stream ds = imgEntry.Open();
                    s.CopyTo(ds);
                }

                // Create XHTML wrapper for the image
                ZipArchiveEntry htmlEntry = epubArchive.CreateEntry($"OEBPS/{pageHref}", CompressionLevel.Fastest);
                using (StreamWriter writer = new(htmlEntry.Open()))
                {
                    writer.Write($@"<?xml version=""1.0"" encoding=""UTF-8""?>
                                    <html xmlns=""http://www.w3.org/1999/xhtml"">
                                    <head><title>Page {i}</title></head>
                                    <body style=""margin:0;padding:0;background-color:#000;"">
                                        <img src=""../{imgHref}"" style=""width:100%;height:auto;display:block;margin:0 auto;"" />
                                    </body></html>");
                }

                images.Add((imgId, imgHref, $"image/{ext.TrimStart('.')}"));
                pages.Add((pageId, pageHref));
                i++;
            }

            // 4. content.opf
            ZipArchiveEntry opfEntry = epubArchive.CreateEntry("OEBPS/content.opf");
            using (StreamWriter writer = new(opfEntry.Open()))
            {
                string title = library.GetComicInfoTitleTemplateResolved(chapterDownloadRecord.Chapter);
                writer.Write($@"<?xml version=""1.0"" encoding=""UTF-8""?>
                                <package xmlns=""http://www.idpf.org/2007/opf"" version=""3.0"" xml:lang=""en"" unique-identifier=""pub-id"" page-progression-direction=""rtl"">
                                  <metadata xmlns:dc=""http://purl.org/dc/elements/1.1/"">
                                    <dc:identifier id=""pub-id"">{chapterDownloadRecord.Id}</dc:identifier>
                                    <dc:title>{title}</dc:title>
                                    <dc:language>{library.Manga.OriginalLanguage ?? "ja"}</dc:language>
                                    <meta name=""cover"" content=""img_0"" />
                                  </metadata>
                                  <manifest>
                                    {string.Join("\n", images.Select(img => $@"<item id=""{img.id}"" href=""{img.href}"" media-type=""{img.type}"" {(img.id == "img_0" ? "properties=\"cover-image\"" : "")}/>"))}
                                    {string.Join("\n", pages.Select(p => $@"<item id=""{p.id}"" href=""{p.href}"" media-type=""application/xhtml+xml""/>"))}
                                    <item id=""ncx"" href=""toc.ncx"" media-type=""application/x-dtbncx+xml""/>
                                  </manifest>
                                  <spine toc=""ncx"">
                                    {string.Join("\n", pages.Select(p => $@"<itemref idref=""{p.id}""/>"))}
                                  </spine>
                                </package>");
            }

            // 5. toc.ncx (for legacy support)
            ZipArchiveEntry ncxEntry = epubArchive.CreateEntry("OEBPS/toc.ncx");
            using (StreamWriter writer = new(ncxEntry.Open()))
            {
                writer.Write($@"<?xml version=""1.0"" encoding=""UTF-8""?>
                                <ncx xmlns=""http://www.daisy.org/z3986/2005/ncx/"" version=""2005-1"">
                                  <head><meta name=""dtb:uid"" content=""{chapterDownloadRecord.Id}""/></head>
                                  <docTitle><text>{library.GetComicInfoTitleTemplateResolved(chapterDownloadRecord.Chapter)}</text></docTitle>
                                  <navMap>
                                    {string.Join("\n", pages.Select((p, idx) => $@"<navPoint id=""{p.id}"" playOrder=""{idx + 1}""><navLabel><text>Page {idx + 1}</text></navLabel><content src=""{p.href}""/></navPoint>"))}
                                  </navMap>
                                </ncx>");
            }
        }

        string filePath = library.GetCbzFilePath(chapterDownloadRecord.Chapter);
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        string fileName = Path.GetFileNameWithoutExtension(filePath) + ".epub";

        outputStream.Position = 0;
        return new DownloadResponse(outputStream, fileName, "application/epub+zip");
    }
}

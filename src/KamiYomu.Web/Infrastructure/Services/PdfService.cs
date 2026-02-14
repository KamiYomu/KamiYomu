using System.IO.Compression;

using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Reports;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Models;

using QuestPDF.Fluent;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Infrastructure.Services;

public class PdfService(
    [FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext,
    IWebHostEnvironment webHostEnvironment) : IPdfService
{
    public DownloadResponse? GetDownloadResponse(Guid libraryId, Guid chapterDownloadId)
    {
        Library library = dbContext.Libraries.FindById(libraryId);
        if (library == null)
        {
            return null;
        }

        using LibraryDbContext db = library.GetReadOnlyDbContext();

        ChapterDownloadRecord record = db.ChapterDownloadRecords.FindById(chapterDownloadId);
        if (record == null || !record.IsCompleted())
        {
            return null;
        }

        string filePath = library.GetCbzFilePath(record.Chapter);
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        string tempDir = Path.Combine(Path.GetTempPath(), AppOptions.Defaults.Worker.TempDirName, Guid.NewGuid().ToString());
        _ = Directory.CreateDirectory(tempDir);

        ZipFile.ExtractToDirectory(filePath, tempDir);

        List<string> images = [.. Directory.GetFiles(tempDir, "*.*")
                              .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                          f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                          f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                          f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
                                          f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                              .OrderBy(f => f)];

        string logoPath = Path.Combine(webHostEnvironment.ContentRootPath, "wwwroot", "images", "logo-watermark.svg");
        MangaChaptersPdfReport document = new(images, Path.GetFileNameWithoutExtension(filePath), logoPath);
        string fileName = Path.GetFileNameWithoutExtension(filePath) + ".pdf";

        FileStream stream = new(
            Path.GetTempFileName(),
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            4096,
            FileOptions.DeleteOnClose | FileOptions.SequentialScan
        );

        document.GeneratePdf(stream);

        stream.Position = 0;

        return new DownloadResponse(stream, fileName, "application/pdf");
    }
}

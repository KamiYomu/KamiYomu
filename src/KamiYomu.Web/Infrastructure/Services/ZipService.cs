
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Models;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Infrastructure.Services;

public class ZipService([FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext) : IZipService
{
    public DownloadResponse? GetDownloadCbzResponse(Guid libraryId, Guid chapterDownloadId)
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

        string fileName = Path.GetFileName(filePath);
        FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        )
        {
            Position = 0
        };

        return new DownloadResponse(stream, fileName, "application/x-cbz");
    }

    public DownloadResponse? GetDownloadZipResponse(Guid libraryId, Guid chapterDownloadId)
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

        string fileName = Path.GetFileNameWithoutExtension(filePath) + ".zip";

        FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        )
        {
            Position = 0
        };
        return new DownloadResponse(stream, fileName, "application/zip");
    }
}

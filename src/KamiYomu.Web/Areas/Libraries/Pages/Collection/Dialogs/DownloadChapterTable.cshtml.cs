using Hangfire;

using KamiYomu.Web.Entities;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Reports;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Worker.Interfaces;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using QuestPDF.Fluent;

using System.IO.Compression;

namespace KamiYomu.Web.Areas.Libraries.Pages.Collection.Dialogs;

public class DownloadChapterTableModel(DbContext dbContext,
                                       IHangfireRepository hangfireRepository,
                                       INotificationService notificationService,
                                       IWebHostEnvironment webHostEnvironment) : PageModel
{
    public IEnumerable<ChapterDownloadRecord> Records { get; set; } = [];
    public int CurrentPage { get; set; } = 0;
    public int TotalPages { get; set; } = 0;
    public Guid LibraryId { get; set; } = Guid.Empty;
    public string SortColumn { get; set; } = nameof(ChapterDownloadRecord.StatusUpdateAt);
    public bool SortAsc { get; set; } = true;

    public void OnGet(Guid libraryId, string sort = nameof(ChapterDownloadRecord.StatusUpdateAt), bool asc = true, int pageIndex = 1, int pageSize = 10)
    {
        if (libraryId == Guid.Empty)
        {
            return;
        }

        LibraryId = libraryId;
        SortColumn = sort;
        SortAsc = asc;
        CurrentPage = pageIndex;

        Library downloadChapterRecords = dbContext.Libraries.FindById(LibraryId);
        using LibraryDbContext db = downloadChapterRecords.GetDbContext();

        // Get all records for this library
        List<ChapterDownloadRecord> allRecords = [.. db.ChapterDownloadRecords.Find(p => true)];

        // Sorting
        IEnumerable<ChapterDownloadRecord> query = sort switch
        {
            nameof(ChapterDownloadRecord.StatusUpdateAt) => asc ? allRecords.OrderBy(r => r.StatusUpdateAt) : allRecords.OrderByDescending(r => r.CreateAt),
            nameof(ChapterDownloadRecord.DownloadStatus) => asc ? allRecords.OrderBy(r => r.DownloadStatus) : allRecords.OrderByDescending(r => r.DownloadStatus),
            nameof(ChapterDownloadRecord.Chapter) => asc ? allRecords.OrderBy(r => r.Chapter.Number) : allRecords.OrderByDescending(r => r.Chapter.Number),
            _ => allRecords.OrderByDescending(r => r.Chapter.Number).ThenBy(r => r.StatusUpdateAt)
        };

        // Pagination
        int totalCount = query.Count();
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        Records = [.. query.Skip((pageIndex - 1) * pageSize).Take(pageSize)];
    }

    public async Task<IActionResult> OnGetDownloadCbzAsync(Guid libraryId, Guid recordId, CancellationToken cancellationToken)
    {
        Library library = dbContext.Libraries.FindById(libraryId);
        if (library == null)
        {
            return NotFound();
        }

        using LibraryDbContext db = library.GetDbContext();

        ChapterDownloadRecord record = db.ChapterDownloadRecords.FindById(recordId);
        if (record == null || !record.IsCompleted())
        {
            return NotFound();
        }

        string filePath = library.GetCbzFilePath(record.Chapter);
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        string fileName = Path.GetFileName(filePath);
        FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );

        return File(stream, "application/x-cbz", fileName);

    }

    public async Task<IActionResult> OnGetDownloadZipAsync(Guid libraryId, Guid recordId, CancellationToken cancellationToken)
    {
        Library library = dbContext.Libraries.FindById(libraryId);
        if (library == null)
        {
            return NotFound();
        }

        using LibraryDbContext db = library.GetDbContext();

        ChapterDownloadRecord record = db.ChapterDownloadRecords.FindById(recordId);
        if (record == null || !record.IsCompleted())
        {
            return NotFound();
        }

        string filePath = library.GetCbzFilePath(record.Chapter);
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        string fileName = Path.GetFileNameWithoutExtension(filePath) + ".zip";

        FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );

        return File(stream, "application/zip", fileName);

    }

    public async Task<IActionResult> OnGetDownloadPdfAsync(Guid libraryId, Guid recordId, CancellationToken cancellationToken)
    {
        Library library = dbContext.Libraries.FindById(libraryId);
        if (library == null)
        {
            return NotFound();
        }

        using LibraryDbContext db = library.GetDbContext();

        ChapterDownloadRecord record = db.ChapterDownloadRecords.FindById(recordId);
        if (record == null || !record.IsCompleted())
        {
            return NotFound();
        }

        string filePath = library.GetCbzFilePath(record.Chapter);
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
            return NotFound();
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

        return File(stream, "application/pdf", fileName);
    }


    public async Task<IActionResult> OnPostRescheduleAsync(Guid libraryId, Guid recordId, CancellationToken cancellationToken)
    {
        Library library = dbContext.Libraries.FindById(libraryId);
        if (library == null)
        {
            return NotFound();
        }

        using LibraryDbContext db = library.GetDbContext();
        ChapterDownloadRecord record = db.ChapterDownloadRecords.FindById(recordId);
        if (record == null || !(record.IsCompleted() || record.IsCancelled()))
        {
            return NotFound();
        }

        record.DeleteDownloadedFileIfExists(library);

        Hangfire.States.EnqueuedState queueState = hangfireRepository.GetLeastLoadedDownloadChapterQueue();
        string jobId = BackgroundJob.Enqueue<IChapterDownloaderJob>(queueState.Queue, worker => worker.DispatchAsync(queueState.Queue,
                                                                                    record.CrawlerAgent.Id,
                                                                                    record.MangaDownload.Library.Id,
                                                                                    record.MangaDownload.Id,
                                                                                    record.Id,
                                                                                    record.MangaDownload.Library.GetCbzFileName(record.Chapter),
                                                                                    null!, CancellationToken.None));

        record.Scheduled(jobId);
        _ = db.ChapterDownloadRecords.Update(record);
        await notificationService.PushSuccessAsync($"{I18n.DownloadChapterSchedule}: {record.MangaDownload.Library.GetCbzFileName(record.Chapter)}", cancellationToken);
        return Partial("_DownloadChapterTableRow", record);
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid libraryId, Guid recordId, CancellationToken cancellationToken)
    {
        Library library = dbContext.Libraries.FindById(libraryId);
        if (library == null)
        {
            return NotFound();
        }

        using LibraryDbContext db = library.GetDbContext();
        ChapterDownloadRecord record = db.ChapterDownloadRecords.FindById(recordId);
        if (record == null)
        {
            return NotFound();
        }

        _ = BackgroundJob.Delete(record.BackgroundJobId);

        record.Cancelled("Cancelled by the user.");
        _ = db.ChapterDownloadRecords.Update(record);
        await notificationService.PushSuccessAsync($"{I18n.DownloadChapterHasBeenCancelled}: {library.GetCbzFileName(record.Chapter)}", cancellationToken);

        return Partial("_DownloadChapterTableRow", record);
    }
}

using Hangfire;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Reports;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Worker.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QuestPDF.Fluent;
using System.IO.Compression;

namespace KamiYomu.Web.Areas.Libraries.Pages.Collection.Dialogs
{
    public class DownloadChapterTableModel(DbContext dbContext,
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
            if (libraryId == Guid.Empty) return;

            LibraryId = libraryId;
            SortColumn = sort;
            SortAsc = asc;
            CurrentPage = pageIndex;

            var downloadChapterRecords = dbContext.Libraries.FindById(LibraryId);
            using var db = downloadChapterRecords.GetDbContext();

            // Get all records for this library
            var allRecords = db.ChapterDownloadRecords.Find(p => true).ToList();

            // Sorting
            IEnumerable<ChapterDownloadRecord> query = sort switch
            {
                nameof(ChapterDownloadRecord.StatusUpdateAt) => asc ? allRecords.OrderBy(r => r.StatusUpdateAt) : allRecords.OrderByDescending(r => r.CreateAt),
                nameof(ChapterDownloadRecord.DownloadStatus) => asc ? allRecords.OrderBy(r => r.DownloadStatus) : allRecords.OrderByDescending(r => r.DownloadStatus),
                nameof(ChapterDownloadRecord.Chapter) => asc ? allRecords.OrderBy(r => r.Chapter.Number) : allRecords.OrderByDescending(r => r.Chapter.Number),
                _ => allRecords.OrderByDescending(r => r.StatusUpdateAt)
            };

            // Pagination
            int totalCount = query.Count();
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            Records = [.. query.Skip((pageIndex - 1) * pageSize).Take(pageSize)];
        }

        public async Task<IActionResult> OnGetDownloadCbzAsync(Guid libraryId, Guid recordId, CancellationToken cancellationToken)
        {
            var downloadChapterRecords = dbContext.Libraries.FindById(libraryId);
            if (downloadChapterRecords == null)
            {
                return NotFound();
            }

            using var db = downloadChapterRecords.GetDbContext();

            var record = db.ChapterDownloadRecords.FindById(recordId);
            if (record == null || !record.IsCompleted())
            {
                return NotFound();
            }

            var filePath = record.Chapter.GetCbzFilePath();
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath, cancellationToken);

            var fileName = System.IO.Path.GetFileName(filePath);
            return File(fileBytes, "application/x-cbz", fileName);
        }

        public async Task<IActionResult> OnGetDownloadZipAsync(Guid libraryId, Guid recordId, CancellationToken cancellationToken)
        {
            var downloadChapterRecords = dbContext.Libraries.FindById(libraryId);
            if (downloadChapterRecords == null)
            {
                return NotFound();
            }

            using var db = downloadChapterRecords.GetDbContext();

            var record = db.ChapterDownloadRecords.FindById(recordId);
            if (record == null || !record.IsCompleted())
            {
                return NotFound();
            }

            var filePath = record.Chapter.GetCbzFilePath();
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath, cancellationToken);

            var fileName = Path.GetFileNameWithoutExtension(filePath) + ".zip";
            return File(fileBytes, "application/zip", fileName);
        }

        public async Task<IActionResult> OnGetDownloadPdfAsync(Guid libraryId, Guid recordId, CancellationToken cancellationToken)
        {
            var downloadChapterRecords = dbContext.Libraries.FindById(libraryId);
            if (downloadChapterRecords == null)
            {
                return NotFound();
            }

            using var db = downloadChapterRecords.GetDbContext();

            var record = db.ChapterDownloadRecords.FindById(recordId);
            if (record == null || !record.IsCompleted())
            {
                return NotFound();
            }

            var filePath = record.Chapter.GetCbzFilePath();
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            ZipFile.ExtractToDirectory(filePath, tempDir);

            var images = Directory.GetFiles(tempDir, "*.*")
                                  .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                              f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                              f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                              f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                                  .OrderBy(f => f)
                                  .ToList();

            var logoPath = Path.Combine(webHostEnvironment.ContentRootPath, "wwwroot", "images", "logo-watermark.svg");
            var document = new MangaChaptersPdfReport(images, Path.GetFileNameWithoutExtension(filePath), logoPath);
            var fileBytes = document.GeneratePdf();

            return File(fileBytes, "application/pdf", Path.GetFileNameWithoutExtension(filePath) + ".pdf");
        }


        public async Task<IActionResult> OnPostRescheduleAsync(Guid libraryId, Guid recordId, CancellationToken cancellationToken)
        {
            var downloadChapterRecords = dbContext.Libraries.FindById(libraryId);
            if (downloadChapterRecords == null)
            {
                return NotFound();
            }

            using var db = downloadChapterRecords.GetDbContext();
            var record = db.ChapterDownloadRecords.FindById(recordId);
            if (record == null || !(record.IsCompleted() || record.IsCancelled()))
            {
                return NotFound();
            }

            record.DeleteDownloadedFileIfExists();

            var jobId = BackgroundJob.Enqueue<IChapterDownloaderJob>(worker => worker.DispatchAsync(record.CrawlerAgent.Id,
                                                                                        record.MangaDownload.Library.Id,
                                                                                        record.MangaDownload.Id,
                                                                                        record.Id,
                                                                                        record.Chapter.GetCbzFileName(),
                                                                                        null!, CancellationToken.None));

            record.Scheduled(jobId);
            db.ChapterDownloadRecords.Update(record);
            await notificationService.PushSuccessAsync($"{I18n.DownloadChapterSchedule}: {record.Chapter.GetCbzFileName()}", cancellationToken);
            return Partial("_DownloadChapterTableRow", record);
        }

        public async Task<IActionResult> OnPostCancelAsync(Guid libraryId, Guid recordId, CancellationToken cancellationToken)
        {
            var downloadChapterRecords = dbContext.Libraries.FindById(libraryId);
            if (downloadChapterRecords == null)
            {
                return NotFound();
            }

            using var db = downloadChapterRecords.GetDbContext();
            var record = db.ChapterDownloadRecords.FindById(recordId);
            if (record == null)
            {
                return NotFound();
            }

            BackgroundJob.Delete(record.BackgroundJobId);

            record.Cancelled("Cancelled by the user.");
            db.ChapterDownloadRecords.Update(record);
            await notificationService.PushSuccessAsync($"{I18n.DownloadChapterHasBeenCancelled}: {record.Chapter.GetCbzFileName()}", cancellationToken);

            return Partial("_DownloadChapterTableRow", record);
        }
    }
}

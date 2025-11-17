using KamiYomu.Web.Entities;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Libraries.Pages.Collection.Dialogs
{
    public class DownloadChapterTableModel(DbContext dbContext) : PageModel
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

        public async Task<IActionResult> OnGetDownloadAsync(Guid libraryId, Guid recordId, CancellationToken cancellationToken)
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
    }
}

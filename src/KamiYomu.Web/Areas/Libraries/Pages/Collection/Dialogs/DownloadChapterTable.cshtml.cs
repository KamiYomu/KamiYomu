using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.AppServices.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Libraries.Pages.Collection.Dialogs;

public class DownloadChapterTableModel(DbContext dbContext,
                                       IDownloadAppService downloadAppService) : PageModel
{
    public IEnumerable<ChapterDownloadRecord> Records { get; set; } = [];
    public Guid LibraryId { get; set; } = Guid.Empty;
    public int TotalItems { get; set; } = 0;

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string SortColumn { get; set; } = nameof(ChapterDownloadRecord.Chapter);

    [BindProperty(SupportsGet = true)]
    public bool SortAsc { get; set; } = true;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 10;

    public void OnGet(Guid libraryId)
    {
        if (libraryId == Guid.Empty)
        {
            return;
        }

        LibraryId = libraryId;

        Library downloadChapterRecords = dbContext.Libraries.FindById(LibraryId);
        using LibraryDbContext db = downloadChapterRecords.GetReadOnlyDbContext();

        // Get all records for this library
        List<ChapterDownloadRecord> allRecords = [.. db.ChapterDownloadRecords.Find(p => true)];

        // Sorting
        IEnumerable<ChapterDownloadRecord> query = SortColumn switch
        {
            nameof(ChapterDownloadRecord.StatusUpdateAt) => SortAsc ? allRecords.OrderBy(r => r.StatusUpdateAt) : allRecords.OrderByDescending(r => r.CreateAt),
            nameof(ChapterDownloadRecord.DownloadStatus) => SortAsc ? allRecords.OrderBy(r => r.DownloadStatus) : allRecords.OrderByDescending(r => r.DownloadStatus),
            nameof(ChapterDownloadRecord.Chapter) => SortAsc ? allRecords.OrderBy(r => r.Chapter.Number) : allRecords.OrderByDescending(r => r.Chapter.Number),
            _ => allRecords.OrderByDescending(r => r.Chapter.Number).ThenBy(r => r.StatusUpdateAt)
        };

        // Pagination
        TotalItems = query.Count();

        Records = [.. query.Skip((CurrentPage - 1) * PageSize).Take(PageSize)];
    }

    public IActionResult OnGetDownloadCbz(Guid libraryId, Guid recordId, [FromServices] IZipService zipService)
    {
        return zipService.GetDownloadCbzResponse(libraryId, recordId) is not DownloadResponse downloadResponse
                  ? NotFound()
                  : File(downloadResponse.Content, downloadResponse.ContentType, downloadResponse.FileName);
    }

    public IActionResult OnGetDownloadZip(Guid libraryId, Guid recordId, [FromServices] IZipService zipService)
    {
        return zipService.GetDownloadZipResponse(libraryId, recordId) is not DownloadResponse downloadResponse
                  ? NotFound()
                  : File(downloadResponse.Content, downloadResponse.ContentType, downloadResponse.FileName);
    }

    public IActionResult OnGetDownloadPdf(Guid libraryId, Guid recordId, [FromServices] IPdfService pdfService)
    {
        return pdfService.GetDownloadResponse(libraryId, recordId) is not DownloadResponse downloadResponse
                ? NotFound()
                : File(downloadResponse.Content, downloadResponse.ContentType, downloadResponse.FileName);
    }

    public IActionResult OnGetDownloadEpub(Guid libraryId, Guid recordId, [FromServices] IEpubService epubService)
    {
        return epubService.GetDownloadResponse(libraryId, recordId) is not DownloadResponse downloadResponse
                ? NotFound()
                : File(downloadResponse.Content, downloadResponse.ContentType, downloadResponse.FileName);
    }


    public async Task<IActionResult> OnPostRescheduleAsync(Guid libraryId, Guid recordId, CancellationToken cancellationToken)
    {
        ChapterDownloadRecord? chapterDownloadRecord = await downloadAppService.RescheduleAsync(libraryId, recordId, cancellationToken);

        return chapterDownloadRecord == null ? NotFound() : ViewComponent("DownloadChapterTableRow", chapterDownloadRecord);
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid libraryId, Guid recordId, CancellationToken cancellationToken)
    {
        ChapterDownloadRecord? chapterDownloadRecord = await downloadAppService.CancelAsync(libraryId, recordId, cancellationToken);

        return chapterDownloadRecord == null ? NotFound() : ViewComponent("DownloadChapterTableRow", chapterDownloadRecord);
    }
}

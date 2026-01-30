using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Areas.Libraries.ViewComponents;

public class DownloadChapterTableRowViewComponent(IUserClockManager userClockService) : ViewComponent
{
    public IViewComponentResult Invoke(ChapterDownloadRecord chapterDownloadRecord)
    {
        string rowId = $"row-{chapterDownloadRecord.Id}";
        string statusUpdateAt = userClockService.ConvertToUserTime(chapterDownloadRecord.StatusUpdateAt.GetValueOrDefault(chapterDownloadRecord.CreateAt)).ToString("g");
        string? rescheduleUrl = chapterDownloadRecord.IsReschedulable() ? @Url.Page("DownloadChapterTable", values: new
        {
            handler = "Reschedule",
            libraryId = chapterDownloadRecord.MangaDownload.Library.Id,
            recordId = chapterDownloadRecord.Id
        }) : "#";

        string? downloadCbzUrl = chapterDownloadRecord.IsCompleted() ? Url.Page("DownloadChapterTable", values: new
        {
            handler = "DownloadCbz",
            libraryId = chapterDownloadRecord.MangaDownload.Library.Id,
            recordId = chapterDownloadRecord.Id
        }) : "#";

        string? downloadZipUrl = chapterDownloadRecord.IsCompleted() ? Url.Page("DownloadChapterTable", values: new
        {
            handler = "DownloadZip",
            libraryId = chapterDownloadRecord.MangaDownload.Library.Id,
            recordId = chapterDownloadRecord.Id
        }) : "#";

        string? downloadPdfUrl = chapterDownloadRecord.IsCompleted() ? Url.Page("DownloadChapterTable", values: new
        {
            handler = "DownloadPdf",
            libraryId = chapterDownloadRecord.MangaDownload.Library.Id,
            recordId = chapterDownloadRecord.Id
        }) : "#";

        string builtInReaderUrl = chapterDownloadRecord.IsCompleted()
            ? Url.Page("/MangaReader/Index", new
            {
                area = "Reader",
                LibraryId = chapterDownloadRecord.MangaDownload.Library.Id,
                ChapterDownloadId = chapterDownloadRecord.Id,
                ReturnUrl = Url.Page("/Collection/Index", new { area = "Libraries" })
            }) ?? "#"
            : "#";

        string? cancelUrl = chapterDownloadRecord.IsInProgress() ? Url.Page("DownloadChapterTable", values: new
        {
            handler = "Cancel",
            libraryId = chapterDownloadRecord.MangaDownload.Library.Id,
            recordId = chapterDownloadRecord.Id
        }) : "#";

        return View(new DownloadChapterTableRowViewComponentModel(
            chapterDownloadRecord,
            statusUpdateAt,
            rowId,
            rescheduleUrl,
            downloadCbzUrl,
            downloadZipUrl,
            downloadPdfUrl,
            builtInReaderUrl,
            cancelUrl));
    }
}

public record DownloadChapterTableRowViewComponentModel(
    ChapterDownloadRecord ChapterDownloadRecord,
    string StatusUpdateAt,
    string? RowId,
    string? RescheduleUrl,
    string? DownloadCbzUrl,
    string? DownloadZipUrl,
    string? DownloadPdfUrl,
    string? BuiltInReaderUrl,
    string? CancelUrl);


using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;

namespace KamiYomu.Web.Areas.Public.Models;

public class ChapterItem
{
    public static ChapterItem? Create(Guid libraryId, ChapterDownloadRecord chapterDownloadRecord)
    {
        return chapterDownloadRecord == null
            ? null
            : new ChapterItem
            {
                ChapterDownloadId = chapterDownloadRecord.Id,
                LibraryId = libraryId,
                Volume = chapterDownloadRecord.Chapter.Volume,
                Number = chapterDownloadRecord.Chapter.Number,
                OnlineSource = chapterDownloadRecord.Chapter.Uri,
                BackgroundJobId = chapterDownloadRecord.BackgroundJobId,
                CreateAt = chapterDownloadRecord.CreateAt,
                StatusUpdateAt = chapterDownloadRecord.StatusUpdateAt,
                DownloadStatus = chapterDownloadRecord.DownloadStatus,
                EpubDownloadUri = new Uri($"/public/api/v1/opds/{libraryId}/chapters/{chapterDownloadRecord.Id}/download/epub", UriKind.Relative),
                ZipDownloadUri = new Uri($"/public/api/v1/opds/{libraryId}/chapters/{chapterDownloadRecord.Id}/download/zip", UriKind.Relative),
                PdfDownloadUri = new Uri($"/public/api/v1/opds/{libraryId}/chapters/{chapterDownloadRecord.Id}/download/pdf", UriKind.Relative),
                CbzDownloadUri = new Uri($"/public/api/v1/opds/{libraryId}/chapters/{chapterDownloadRecord.Id}/download/cbz", UriKind.Relative),
            };
    }
    public Guid ChapterDownloadId { get; set; }
    public decimal Volume { get; set; }
    public decimal Number { get; set; }
    public Uri OnlineSource { get; set; }
    public string BackgroundJobId { get; set; }
    public DateTimeOffset CreateAt { get; set; }
    public DateTimeOffset? StatusUpdateAt { get; set; }
    public DownloadStatus DownloadStatus { get; set; }
    public Guid LibraryId { get; set; }
    public Uri EpubDownloadUri { get; private set; }
    public Uri CbzDownloadUri { get; private set; }
    public Uri PdfDownloadUri { get; private set; }
    public Uri ZipDownloadUri { get; private set; }
}


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
    public Guid ChapterDownloadId { get; internal set; }
    public decimal Volume { get; internal set; }
    public decimal Number { get; internal set; }
    public Uri OnlineSource { get; internal set; }
    public string BackgroundJobId { get; internal set; }
    public DateTimeOffset CreateAt { get; internal set; }
    public DateTimeOffset? StatusUpdateAt { get; internal set; }
    public DownloadStatus DownloadStatus { get; internal set; }
    public Guid LibraryId { get; internal set; }
    public Uri EpubDownloadUri { get; private set; }
    public Uri CbzDownloadUri { get; private set; }
    public Uri PdfDownloadUri { get; private set; }
    public Uri ZipDownloadUri { get; private set; }
}

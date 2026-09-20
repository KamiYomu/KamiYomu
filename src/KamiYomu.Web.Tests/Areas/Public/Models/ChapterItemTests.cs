using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Areas.Public.Models;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Tests.Infrastructure.Services;

namespace KamiYomu.Web.Tests.Areas.Public.Models;

public class ChapterItemTests
{
    [Fact]
    public void Create_WhenRecordIsNull_ReturnsNull()
    {
        ChapterItem? result = ChapterItem.Create(Guid.NewGuid(), null!);

        Assert.Null(result);
    }

    [Fact]
    public void Create_WhenRecordProvided_MapsFieldsAndBuildsDownloadUris()
    {
        Library library = ServiceTestHelpers.CreateLibrary();
        Chapter chapter = ServiceTestHelpers.CreateChapter(2.5m, "Chapter Test");
        MangaDownloadRecord mangaDownload = new(library, "manga-job-1");
        ChapterDownloadRecord record = new(library.CrawlerAgent, mangaDownload, chapter);
        ServiceTestHelpers.AssignId(record);
        record.Complete();

        Guid libraryId = library.Id;

        ChapterItem? item = ChapterItem.Create(libraryId, record);

        Assert.NotNull(item);
        Assert.Equal(record.Id, item!.ChapterDownloadId);
        Assert.Equal(libraryId, item.LibraryId);
        Assert.Equal(chapter.Volume, item.Volume);
        Assert.Equal(chapter.Number, item.Number);
        Assert.Equal(chapter.Uri, item.OnlineSource);
        Assert.Equal(record.BackgroundJobId, item.BackgroundJobId);
        Assert.Equal(record.CreateAt, item.CreateAt);
        Assert.Equal(record.StatusUpdateAt, item.StatusUpdateAt);
        Assert.Equal(DownloadStatus.Completed, item.DownloadStatus);

        Assert.Equal($"/public/api/v1/opds/{libraryId}/chapters/{record.Id}/download/epub", item.EpubDownloadUri.ToString());
        Assert.Equal($"/public/api/v1/opds/{libraryId}/chapters/{record.Id}/download/zip", item.ZipDownloadUri.ToString());
        Assert.Equal($"/public/api/v1/opds/{libraryId}/chapters/{record.Id}/download/pdf", item.PdfDownloadUri.ToString());
        Assert.Equal($"/public/api/v1/opds/{libraryId}/chapters/{record.Id}/download/cbz", item.CbzDownloadUri.ToString());
    }
}

using KamiYomu.Web.Areas.Reader.ViewModels;
using KamiYomu.Web.Entities.Definitions;

namespace KamiYomu.Web.Tests.Areas.Reader.ViewModels;

public class WeeklyChapterViewModelTests
{
    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        Guid libraryId = Guid.NewGuid();
        Uri coverUrl = new("https://example.com/cover.png");
        List<WeeklyChapterItemViewModel> items = [new WeeklyChapterItemViewModel
        {
            ChapterDownloadId = Guid.NewGuid(),
            DownloadStatus = DownloadStatus.Completed,
            ChapterNumber = 1.5m,
            StatusUpdateAt = new DateTime(2024, 1, 1)
        }];

        WeeklyChapterViewModel model = new()
        {
            LibraryId = libraryId,
            MangaId = "manga-1",
            MangaTitle = "Alpha",
            MangaCoverUrl = coverUrl,
            Items = items
        };

        Assert.Equal(libraryId, model.LibraryId);
        Assert.Equal("manga-1", model.MangaId);
        Assert.Equal("Alpha", model.MangaTitle);
        Assert.Same(coverUrl, model.MangaCoverUrl);
        Assert.Same(items, model.Items);

        WeeklyChapterItemViewModel item = Assert.Single(model.Items);
        Assert.Equal(DownloadStatus.Completed, item.DownloadStatus);
        Assert.Equal(1.5m, item.ChapterNumber);
        Assert.Equal(new DateTime(2024, 1, 1), item.StatusUpdateAt);
    }
}

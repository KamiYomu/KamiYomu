using KamiYomu.Web.Areas.Reader.Models;

namespace KamiYomu.Web.Tests.Areas.Reader.Models;

public class ChapterProgressTests
{
    [Fact]
    public void Constructor_SetsLibraryChapterAndNumber()
    {
        Guid libraryId = Guid.NewGuid();
        Guid chapterId = Guid.NewGuid();

        ChapterProgress progress = new(libraryId, chapterId, 4.5m);

        Assert.Equal(libraryId, progress.LibraryId);
        Assert.Equal(chapterId, progress.ChapterDownloadId);
        Assert.Equal(4.5m, progress.ChapterNumber);
        Assert.False(progress.IsCompleted);
        Assert.Equal(0, progress.LastPageRead);
        Assert.Equal(0, progress.TotalPages);
    }

    [Fact]
    public void SetLastPageRead_UpdatesPageAndTotalsAndClearsCompletedFlag()
    {
        ChapterProgress progress = new(Guid.NewGuid(), Guid.NewGuid(), 1);
        progress.SetAsCompleted(10);

        progress.SetLastPageRead(3, 10);

        Assert.Equal(3, progress.LastPageRead);
        Assert.Equal(10, progress.TotalPages);
        Assert.False(progress.IsCompleted);
        Assert.True(progress.LastReadAt <= DateTimeOffset.UtcNow);
        Assert.True(progress.LastReadAt > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void SetAsCompleted_SetsLastPageToTotalAndMarksCompleted()
    {
        ChapterProgress progress = new(Guid.NewGuid(), Guid.NewGuid(), 1);

        progress.SetAsCompleted(15);

        Assert.Equal(15, progress.LastPageRead);
        Assert.Equal(15, progress.TotalPages);
        Assert.True(progress.IsCompleted);
    }
}

using KamiYomu.Web.Areas.Reader.Models;
using KamiYomu.Web.Areas.Reader.Pages.History;
using KamiYomu.Web.Areas.Reader.Repositories.Interfaces;
using KamiYomu.Web.Areas.Reader.ViewModels;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Tests.Areas.Reader.Pages.History;

public class IndexModelTests
{
    [Fact]
    public void OnGet_PopulatesGroupedHistoryAndNextOffset()
    {
        IEnumerable<IGrouping<DateTime, ChapterViewModel>> grouped = BuildGrouping();
        Mock<IChapterProgressRepository> repository = new();
        _ = repository.Setup(r => r.FetchHistory(0, 20)).Returns(grouped);

        IndexModel model = new(repository.Object);

        model.OnGet();

        Assert.Same(grouped, model.GroupedHistory);
        Assert.Equal(20, model.NextOffset);
        repository.Verify(r => r.FetchHistory(0, 20), Times.Once);
    }

    [Fact]
    public void OnGet_WithCustomOffsetAndLength_ComputesNextOffset()
    {
        Mock<IChapterProgressRepository> repository = new();
        _ = repository.Setup(r => r.FetchHistory(10, 5)).Returns([]);

        IndexModel model = new(repository.Object);

        model.OnGet(offset: 10, length: 5);

        Assert.Equal(15, model.NextOffset);
    }

    [Fact]
    public void OnGetScroll_WhenHistoryEmpty_ReturnsEmptyContent()
    {
        Mock<IChapterProgressRepository> repository = new();
        _ = repository.Setup(r => r.FetchHistory(It.IsAny<int>(), It.IsAny<int>())).Returns([]);

        IndexModel model = new(repository.Object);

        IActionResult result = model.OnGetScroll(0, 20);

        ContentResult content = Assert.IsType<ContentResult>(result);
        Assert.Equal(string.Empty, content.Content);
    }

    [Fact]
    public void OnGetScroll_WhenHistoryHasItems_ReturnsPartialView()
    {
        IEnumerable<IGrouping<DateTime, ChapterViewModel>> grouped = BuildGrouping();
        Mock<IChapterProgressRepository> repository = new();
        _ = repository.Setup(r => r.FetchHistory(0, 20)).Returns(grouped);

        IndexModel model = new(repository.Object)
        {
            PageContext = ServiceTestHelpers.CreatePageContext()
        };

        IActionResult result = model.OnGetScroll(0, 20);

        PartialViewResult partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_HistoryListPartial", partial.ViewName);
        Assert.Same(model, partial.Model);
    }

    private static IEnumerable<IGrouping<DateTime, ChapterViewModel>> BuildGrouping()
    {
        ChapterViewModel viewModel = new()
        {
            ChapterProgress = new ChapterProgress(Guid.NewGuid(), Guid.NewGuid(), 1),
            Library = null
        };

        return new[] { viewModel }.GroupBy(_ => DateTime.UtcNow.Date);
    }
}

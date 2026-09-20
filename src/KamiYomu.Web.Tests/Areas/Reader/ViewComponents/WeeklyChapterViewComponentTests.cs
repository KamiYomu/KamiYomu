using KamiYomu.Web.Areas.Reader.Repositories.Interfaces;
using KamiYomu.Web.Areas.Reader.ViewComponents;
using KamiYomu.Web.Areas.Reader.ViewModels;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.Areas.Reader.ViewComponents;

public class WeeklyChapterViewComponentTests
{
    [Fact]
    public void Invoke_ReturnsViewModelWithLimitAndChaptersFromRepository()
    {
        List<WeeklyChapterViewModel> chapters =
        [
            new WeeklyChapterViewModel { LibraryId = Guid.NewGuid(), MangaTitle = "Alpha", Items = [] }
        ];
        Mock<IChapterProgressRepository> repository = new();
        _ = repository.Setup(r => r.FetchWeeklyChapters(7)).Returns(chapters);

        WeeklyChapterViewComponent component = new(repository.Object);

        IViewComponentResult result = component.Invoke(7);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        WeeklyChapterViewComponentModel model = Assert.IsType<WeeklyChapterViewComponentModel>(viewResult.ViewData.Model);
        Assert.Equal(7, model.Limit);
        Assert.Same(chapters, model.WeeklyChapters);
        repository.Verify(r => r.FetchWeeklyChapters(7), Times.Once);
    }

    [Fact]
    public void Invoke_WhenRepositoryReturnsEmpty_ReturnsEmptyChapters()
    {
        Mock<IChapterProgressRepository> repository = new();
        _ = repository.Setup(r => r.FetchWeeklyChapters(It.IsAny<int>())).Returns([]);

        WeeklyChapterViewComponent component = new(repository.Object);

        IViewComponentResult result = component.Invoke(3);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        WeeklyChapterViewComponentModel model = Assert.IsType<WeeklyChapterViewComponentModel>(viewResult.ViewData.Model);
        Assert.Empty(model.WeeklyChapters);
    }
}

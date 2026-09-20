using KamiYomu.Web.Areas.Reader.Models;
using KamiYomu.Web.Areas.Reader.ViewModels;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Tests.Infrastructure.Services;

namespace KamiYomu.Web.Tests.Areas.Reader.ViewModels;

public class ChapterViewModelTests
{
    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        ChapterProgress progress = new(Guid.NewGuid(), Guid.NewGuid(), 1);
        Library library = ServiceTestHelpers.CreateLibrary("Alpha");

        ChapterViewModel model = new()
        {
            ChapterProgress = progress,
            Library = library
        };

        Assert.Same(progress, model.ChapterProgress);
        Assert.Same(library, model.Library);
    }
}

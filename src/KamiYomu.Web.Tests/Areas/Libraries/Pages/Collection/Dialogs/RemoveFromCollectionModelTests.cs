using KamiYomu.Web.Areas.Libraries.Pages.Collection.Dialogs;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Tests.Infrastructure.Services;

namespace KamiYomu.Web.Tests.Areas.Libraries.Pages.Collection.Dialogs;

public class RemoveFromCollectionModelTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private RemoveFromCollectionModel CreateModel()
    {
        return new RemoveFromCollectionModel(_dbContext)
        {
            RefreshElementId = string.Empty,
            Library = null!
        };
    }

    [Fact]
    public void OnGet_WithExistingLibrary_PopulatesLibraryAndIds()
    {
        Library library = ServiceTestHelpers.CreateLibrary("Alpha");
        _ = _dbContext.Libraries.Insert(library);

        RemoveFromCollectionModel model = CreateModel();

        model.OnGet(library.Id, "refresh-1");

        Assert.Equal(library.Id, model.LibraryId);
        Assert.Equal("refresh-1", model.RefreshElementId);
        Assert.Equal(library.Id, model.Library.Id);
        Assert.Equal(library.Manga.Id, model.Library.Manga.Id);
    }

    [Fact]
    public void OnGet_WithUnknownLibraryId_LeavesLibraryNull()
    {
        RemoveFromCollectionModel model = CreateModel();

        model.OnGet(Guid.NewGuid(), "refresh-2");

        Assert.Null(model.Library);
    }
}

using KamiYomu.Web.Areas.Reader.Pages.MangaGallery;
using KamiYomu.Web.Areas.Reader.Repositories.Interfaces;
using KamiYomu.Web.Areas.Reader.ViewModels;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Tests.Areas.Reader.Pages.MangaGallery;

public class IndexModelTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");
    private readonly Mock<IChapterProgressRepository> _repository = new();

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private void InsertUserPreference(bool familySafeMode = false)
    {
        UserPreference preference = new(System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        preference.SetFamilySafeMode(familySafeMode);
        _ = _dbContext.UserPreferences.Insert(preference);
    }

    private IndexModel CreateModel(string httpMethod = "GET", bool withHxRequestHeader = false)
    {
        PageContext pageContext = ServiceTestHelpers.CreatePageContext(httpContext =>
        {
            httpContext.Request.Method = httpMethod;
            if (withHxRequestHeader)
            {
                httpContext.Request.Headers["HX-Request"] = "true";
            }
        });

        return new IndexModel(_dbContext, _repository.Object)
        {
            PageContext = pageContext
        };
    }

    [Fact]
    public void OnGet_PopulatesLibrariesRecentlyAddedAndTotalItems()
    {
        InsertUserPreference();
        Library library1 = ServiceTestHelpers.CreateLibrary("Alpha");
        Library library2 = ServiceTestHelpers.CreateLibrary("Beta");
        _ = _dbContext.Libraries.Insert(library1);
        _ = _dbContext.Libraries.Insert(library2);
        _ = _repository.Setup(r => r.FetchHistory(0, 5)).Returns([]);

        IndexModel model = CreateModel();
        model.CurrentPage = 1;
        model.PageSize = 10;

        model.OnGet();

        Assert.Equal(2, model.TotalItems);
        Assert.Equal(2, model.Libraries.Count);
        Assert.Equal(2, model.RecentlyAddedLibraries.Count);
        _repository.Verify(r => r.FetchHistory(0, 5), Times.Once);
    }

    [Fact]
    public void OnGet_RespectsPageSizeAndCurrentPage()
    {
        InsertUserPreference();
        for (int i = 0; i < 5; i++)
        {
            _ = _dbContext.Libraries.Insert(ServiceTestHelpers.CreateLibrary($"Manga {i}"));
        }
        _ = _repository.Setup(r => r.FetchHistory(0, 5)).Returns([]);

        IndexModel model = CreateModel();
        model.CurrentPage = 2;
        model.PageSize = 2;

        model.OnGet();

        Assert.Equal(5, model.TotalItems);
        Assert.Equal(2, model.Libraries.Count);
    }

    [Fact]
    public void OnGetSearch_WhenHxRequestHeaderPresent_ReturnsPartialResult()
    {
        InsertUserPreference();
        Library library = ServiceTestHelpers.CreateLibrary("Naruto Shippuden");
        _ = _dbContext.Libraries.Insert(library);

        IndexModel model = CreateModel(withHxRequestHeader: true);
        model.Search = "Naruto";
        model.CurrentPage = 1;
        model.PageSize = 10;

        IActionResult result = model.OnGetSearch();

        PartialViewResult partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_PagedMangaGrid", partial.ViewName);
        _ = Assert.Single(model.Libraries);
    }

    [Fact]
    public void OnGetSearch_WhenNoHxRequestHeader_ReturnsPageResultAndPopulatesHistory()
    {
        InsertUserPreference();
        Library library = ServiceTestHelpers.CreateLibrary("One Piece");
        _ = _dbContext.Libraries.Insert(library);
        _ = _repository.Setup(r => r.FetchHistory(0, 5)).Returns([]);

        IndexModel model = CreateModel();
        model.Search = "One";
        model.CurrentPage = 1;
        model.PageSize = 10;

        IActionResult result = model.OnGetSearch();

        _ = Assert.IsType<PageResult>(result);
        Assert.NotNull(model.GroupedHistory);
        _repository.Verify(r => r.FetchHistory(0, 5), Times.Once);
    }

    [Fact]
    public void OnGetSearch_FiltersByTitleCaseInsensitive()
    {
        InsertUserPreference();
        Library matching = ServiceTestHelpers.CreateLibrary("Death Note");
        Library other = ServiceTestHelpers.CreateLibrary("Bleach");
        _ = _dbContext.Libraries.Insert(matching);
        _ = _dbContext.Libraries.Insert(other);
        _ = _repository.Setup(r => r.FetchHistory(0, 5)).Returns([]);

        IndexModel model = CreateModel();
        model.Search = "death";
        model.CurrentPage = 1;
        model.PageSize = 10;

        _ = model.OnGetSearch();

        Library item = Assert.Single(model.Libraries);
        Assert.Equal(matching.Id, item.Id);
    }
}

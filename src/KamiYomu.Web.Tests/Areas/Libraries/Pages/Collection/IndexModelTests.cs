using System.Net;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Areas.Libraries.Pages.Collection;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace KamiYomu.Web.Tests.Areas.Libraries.Pages.Collection;

public class IndexModelTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");
    private readonly ImageDbContext _imageDbContext = new(":memory:");

    public IndexModelTests()
    {
        _ = _dbContext.UserPreferences.Insert(new UserPreference(System.Globalization.CultureInfo.GetCultureInfo("en-US")));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _imageDbContext.Dispose();
    }

    private IndexModel CreateModel(IHttpClientFactory? httpClientFactory = null)
    {
        return new IndexModel(
            Mock.Of<ILogger<IndexModel>>(),
            _dbContext,
            httpClientFactory ?? Mock.Of<IHttpClientFactory>(),
            _imageDbContext);
    }

    [Fact]
    public void OnGet_FiltersLibrariesByQueryAndPopulatesViewData()
    {
        Library matching = ServiceTestHelpers.CreateLibrary("One Piece");
        Library other = ServiceTestHelpers.CreateLibrary("Naruto");
        _ = _dbContext.Libraries.Insert(matching);
        _ = _dbContext.Libraries.Insert(other);

        IndexModel model = CreateModel();
        model.PageContext = ServiceTestHelpers.CreatePageContext();
        model.Query = "One";

        model.OnGet();

        Library result = Assert.Single(model.Results);
        Assert.Equal(matching.Id, result.Id);
        Assert.Equal(false, model.ViewData["ShowAddToLibrary"]);
        Assert.Equal("Search", model.ViewData["Handler"]);
    }

    [Fact]
    public void OnGetSearch_WithoutHxRequestHeader_ReturnsPage()
    {
        Library library = ServiceTestHelpers.CreateLibrary("Alpha");
        _ = _dbContext.Libraries.Insert(library);

        IndexModel model = CreateModel();
        model.PageContext = ServiceTestHelpers.CreatePageContext();

        IActionResult result = model.OnGetSearch();

        _ = Assert.IsType<PageResult>(result);
        _ = Assert.Single(model.Results);
    }

    [Fact]
    public void OnGetSearch_WithHxRequestHeader_ReturnsSearchMangaResultViewComponent()
    {
        Library library = ServiceTestHelpers.CreateLibrary("Alpha");
        _ = _dbContext.Libraries.Insert(library);

        IndexModel model = CreateModel();
        model.PageContext = ServiceTestHelpers.CreatePageContext(httpContext => httpContext.Request.Headers["HX-Request"] = "true");

        Mock<IUrlHelper> urlHelper = new();
        ActionContext actionContext = new(model.PageContext.HttpContext, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        _ = urlHelper.Setup(u => u.ActionContext).Returns(actionContext);
        _ = urlHelper.Setup(u => u.RouteUrl(It.IsAny<Microsoft.AspNetCore.Mvc.Routing.UrlRouteContext>())).Returns("/mocked-search-url");
        model.Url = urlHelper.Object;

        IActionResult result = model.OnGetSearch();

        ViewComponentResult viewComponentResult = Assert.IsType<ViewComponentResult>(result);
        Assert.Equal("SearchMangaResult", viewComponentResult.ViewComponentName);
    }

    [Fact]
    public async Task OnGetImageAsync_WhenCancellationRequested_ReturnsNoContent()
    {
        IndexModel model = CreateModel();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        IActionResult result = await model.OnGetImageAsync(new Uri("https://example.com/cover.png"), cts.Token);

        _ = Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task OnGetImageAsync_WithNullUri_ReturnsBadRequest()
    {
        IndexModel model = CreateModel();

        IActionResult result = await model.OnGetImageAsync(null!, CancellationToken.None);

        _ = Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task OnGetImageAsync_WithRelativeUri_ReturnsBadRequest()
    {
        IndexModel model = CreateModel();

        IActionResult result = await model.OnGetImageAsync(new Uri("relative/path.png", UriKind.Relative), CancellationToken.None);

        _ = Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task OnGetImageAsync_WhenAlreadyCached_DoesNotDownloadAndReturnsFile()
    {
        Uri imageUri = new("https://example.com/cached-cover.png");
        using MemoryStream imageBytes = new([1, 2, 3, 4]);
        _ = _imageDbContext.CoverImageFileStorage.Upload(imageUri, "cached-cover.png", imageBytes);

        Mock<IHttpClientFactory> httpClientFactory = new(MockBehavior.Strict);
        IndexModel model = CreateModel(httpClientFactory.Object);
        model.PageContext = ServiceTestHelpers.CreatePageContext();

        IActionResult result = await model.OnGetImageAsync(imageUri, CancellationToken.None);

        _ = Assert.IsType<FileStreamResult>(result);
        httpClientFactory.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OnGetImageAsync_WhenNotCached_DownloadsAndStoresImage()
    {
        using LocalHttpServer server = new(new Dictionary<string, Func<HttpListenerRequest, HttpResponseData>>
        {
            ["/cover.png"] = _ => HttpResponseData.Bytes([9, 8, 7], contentType: "image/png")
        });
        Uri imageUri = new(server.BaseUri, "cover.png");

        using HttpClient httpClient = new();
        StubHttpClientFactory httpClientFactory = new(httpClient);

        IndexModel model = CreateModel(httpClientFactory);
        model.PageContext = ServiceTestHelpers.CreatePageContext();

        IActionResult result = await model.OnGetImageAsync(imageUri, CancellationToken.None);

        _ = Assert.IsType<FileStreamResult>(result);
        Assert.True(_imageDbContext.CoverImageFileStorage.Exists(imageUri));
    }

    [Fact]
    public async Task OnGetImageAsync_WhenDownloadFails_ReturnsServerError()
    {
        using LocalHttpServer server = new(new Dictionary<string, Func<HttpListenerRequest, HttpResponseData>>
        {
            ["/missing.png"] = _ => HttpResponseData.Text("nope", HttpStatusCode.InternalServerError, "text/plain")
        });
        Uri imageUri = new(server.BaseUri, "missing.png");

        using HttpClient httpClient = new();
        StubHttpClientFactory httpClientFactory = new(httpClient);

        IndexModel model = CreateModel(httpClientFactory);

        IActionResult result = await model.OnGetImageAsync(imageUri, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
    }
}

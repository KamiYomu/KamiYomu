using System.IO.Compression;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Areas.Settings.Models;
using KamiYomu.Web.Areas.Settings.Pages.CommunityCrawlers;
using KamiYomu.Web.Areas.Settings.ViewComponents;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.CrawlerAgentRuntime;
using KamiYomu.Web.Entities.CrawlerAgentRuntime.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Resources;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Tests.Areas.Settings.Pages.Add_ons;

public class IndexModelTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");
    private readonly Mock<INugetService> _nugetService = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<ICrawlerAgentAssemblyLoader> _crawlerAgentAssemblyLoader = new();
    private readonly Mock<ILogger<IndexModel>> _logger = new();
    private readonly List<string> _createdDirectories = [];

    public IndexModelTests()
    {
        ServiceTestHelpers.EnsureServiceLocatorConfigured();
    }

    public void Dispose()
    {
        _dbContext.Dispose();

        foreach (string directory in _createdDirectories)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    private IndexModel CreateModel(IOptions<StartupOptions>? startupOptions = null)
    {
        return new IndexModel(
            _logger.Object,
            startupOptions ?? Options.Create(new StartupOptions()),
            _dbContext,
            _nugetService.Object,
            _notificationService.Object,
            _crawlerAgentAssemblyLoader.Object);
    }

    private static NugetPackageInfo CreatePackage(string id, string version, string[]? tags = null, List<string>? dependencies = null)
    {
        return new NugetPackageInfo
        {
            Id = id,
            Version = version,
            Description = $"{id} description",
            Authors = ["Author"],
            Tags = tags ?? [],
            TotalDownloads = 10,
            Dependencies = dependencies ?? []
        };
    }

    // ---------- OnGet ----------

    [Fact]
    public void OnGet_NoSources_SetsIsNugetAddedFalse()
    {
        IndexModel model = CreateModel();

        model.OnGet();

        Assert.False(model.IsNugetAdded);
        Assert.Empty(model.SearchBarViewModel.Sources);
        Assert.Empty(model.PackageListViewModel.PackageItems);
    }

    [Fact]
    public void OnGet_WithNugetOrgSource_SetsIsNugetAddedTrue()
    {
        NugetSource source = new("NuGet.org", new Uri(Defaults.NugetFeeds.NugetFeedUrl), null, null);
        _ = _dbContext.NugetSources.Insert(source);

        IndexModel model = CreateModel();

        model.OnGet();

        Assert.True(model.IsNugetAdded);
        _ = Assert.Single(model.SearchBarViewModel.Sources);
    }

    [Fact]
    public void OnGet_WithNonNugetOrgSource_SetsIsNugetAddedFalse()
    {
        NugetSource source = new("Custom", new Uri("https://example.com/v3/index.json"), null, null);
        _ = _dbContext.NugetSources.Insert(source);

        IndexModel model = CreateModel();

        model.OnGet();

        Assert.False(model.IsNugetAdded);
    }

    // ---------- OnGetPackageItemAsync ----------

    [Fact]
    public async Task OnGetPackageItemAsync_ReturnsViewComponentWithGroupedData()
    {
        Guid sourceId = Guid.NewGuid();
        NugetPackageInfo selectedPackage = CreatePackage("My.Package", "1.0.0", tags: ["tag1"], dependencies: ["My.Package:1.0.0"]);
        selectedPackage = new NugetPackageInfo
        {
            Id = "My.Package",
            Version = "1.0.0",
            Description = "desc",
            Authors = ["Author1"],
            Tags = ["tag1"],
            TotalDownloads = 42,
            IconUrl = new Uri("https://example.com/icon.png"),
            LicenseUrl = new Uri("https://example.com/license"),
            RepositoryUrl = new Uri("https://example.com/repo"),
            Dependencies = []
        };

        List<NugetPackageInfo> allVersions =
        [
            new NugetPackageInfo
            {
                Id = "My.Package",
                Version = "2.0.0",
                Dependencies = ["Dependency.One:2.0.0"]
            },
            new NugetPackageInfo
            {
                Id = "My.Package",
                Version = "1.0.0",
                Dependencies = ["Dependency.Two:1.0.0"]
            }
        ];

        _ = _nugetService
            .Setup(s => s.GetPackageMetadataAsync(sourceId, "My.Package", "1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(selectedPackage);

        _ = _nugetService
            .Setup(s => s.GetAllPackageVersionsAsync(sourceId, "My.Package", It.IsAny<CancellationToken>()))
            .ReturnsAsync(allVersions);

        IndexModel model = CreateModel();

        IActionResult result = await model.OnGetPackageItemAsync(sourceId, "My.Package", "1.0.0", CancellationToken.None);

        ViewComponentResult viewComponentResult = Assert.IsType<ViewComponentResult>(result);
        Assert.Equal("PackageItem", viewComponentResult.ViewComponentName);
        NugetPackageGroupedViewModel viewModel = Assert.IsType<NugetPackageGroupedViewModel>(viewComponentResult.Arguments);

        Assert.Equal("My.Package", viewModel.Id);
        Assert.Equal(sourceId, viewModel.SourceId);
        Assert.Equal(selectedPackage, viewModel.VersionSelected);
        Assert.Equal(["Author1"], viewModel.Authors);
        Assert.Equal(["tag1"], viewModel.Tags);
        Assert.Equal(42L, viewModel.TotalDownloads);
        Assert.Equal(["2.0.0", "1.0.0"], viewModel.Versions);

        // DependenciesByVersion is built as p.Split(":")[1] -> key, p.Split(":")[0] -> value,
        // i.e. keyed by dependency VERSION, valued by dependency PACKAGE ID (looks backwards,
        // but this documents actual pre-existing production behavior).
        Assert.Equal(2, viewModel.DependenciesByVersion.Count);
        Assert.Equal("Dependency.One", viewModel.DependenciesByVersion["2.0.0"]);
        Assert.Equal("Dependency.Two", viewModel.DependenciesByVersion["1.0.0"]);
    }

    [Fact]
    public async Task OnGetPackageItemAsync_NoDependencies_ReturnsEmptyDictionary()
    {
        Guid sourceId = Guid.NewGuid();
        NugetPackageInfo selectedPackage = CreatePackage("My.Package", "1.0.0");

        _ = _nugetService
            .Setup(s => s.GetPackageMetadataAsync(sourceId, "My.Package", "1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(selectedPackage);

        _ = _nugetService
            .Setup(s => s.GetAllPackageVersionsAsync(sourceId, "My.Package", It.IsAny<CancellationToken>()))
            .ReturnsAsync([selectedPackage]);

        IndexModel model = CreateModel();

        IActionResult result = await model.OnGetPackageItemAsync(sourceId, "My.Package", "1.0.0", CancellationToken.None);

        ViewComponentResult viewComponentResult = Assert.IsType<ViewComponentResult>(result);
        NugetPackageGroupedViewModel viewModel = Assert.IsType<NugetPackageGroupedViewModel>(viewComponentResult.Arguments);
        Assert.Empty(viewModel.DependenciesByVersion);
    }

    // ---------- OnGetSearchAsync ----------

    [Fact]
    public async Task OnGetSearchAsync_NoPreferences_DefaultsToFamilySafe_FiltersNsfwAndReturnsPage()
    {
        NugetPackageInfo safePackage = CreatePackage("Safe.Package", "1.0.0", tags: ["safe"]);
        NugetPackageInfo nsfwPackage = CreatePackage("Nsfw.Package", "1.0.0", tags: [Defaults.Package.NotSafeForWorkTag]);

        _ = _nugetService
            .Setup(s => s.SearchPackagesAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([safePackage, nsfwPackage]);

        IndexModel model = CreateModel();
        model.PageContext = ServiceTestHelpers.CreatePageContext();

        IActionResult result = await model.OnGetSearchAsync(CancellationToken.None);

        PageResult pageResult = Assert.IsType<PageResult>(result);
        _ = pageResult;
        NugetPackageGroupedViewModel item = Assert.Single(model.PackageListViewModel.PackageItems);
        Assert.Equal("Safe.Package", item.Id);
    }

    [Fact]
    public async Task OnGetSearchAsync_FamilySafeModeFalse_IncludesNsfwPackages()
    {
        UserPreference preference = new(System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        preference.SetFamilySafeMode(false);
        _ = _dbContext.UserPreferences.Insert(preference);

        NugetPackageInfo safePackage = CreatePackage("Safe.Package", "1.0.0");
        NugetPackageInfo nsfwPackage = CreatePackage("Nsfw.Package", "1.0.0", tags: [Defaults.Package.NotSafeForWorkTag]);

        _ = _nugetService
            .Setup(s => s.SearchPackagesAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([safePackage, nsfwPackage]);

        IndexModel model = CreateModel();
        model.PageContext = ServiceTestHelpers.CreatePageContext();

        _ = await model.OnGetSearchAsync(CancellationToken.None);

        Assert.Equal(2, model.PackageListViewModel.PackageItems.Count());
    }

    [Fact]
    public async Task OnGetSearchAsync_WithHxRequestHeader_ReturnsPackageListViewComponent()
    {
        NugetPackageInfo package = CreatePackage("Safe.Package", "1.0.0");

        _ = _nugetService
            .Setup(s => s.SearchPackagesAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([package]);

        IndexModel model = CreateModel();
        model.PageContext = ServiceTestHelpers.CreatePageContext(httpContext =>
        {
            httpContext.Request.Headers["HX-Request"] = "true";
        });

        IActionResult result = await model.OnGetSearchAsync(CancellationToken.None);

        ViewComponentResult viewComponentResult = Assert.IsType<ViewComponentResult>(result);
        Assert.Equal("PackageList", viewComponentResult.ViewComponentName);
        PackageListViewModel viewModel = Assert.IsType<PackageListViewModel>(viewComponentResult.Arguments);
        _ = Assert.Single(viewModel.PackageItems);
    }

    [Fact]
    public async Task OnGetSearchAsync_EmptySearch_UsesDefaultSearchTermFromOptions()
    {
        _ = _nugetService
            .Setup(s => s.SearchPackagesAsync(It.IsAny<Guid>(), "MyDefaultTerm", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        IndexModel model = CreateModel(Options.Create(new StartupOptions { DefaultSearchTerm = "MyDefaultTerm" }));
        model.PageContext = ServiceTestHelpers.CreatePageContext();
        model.SearchBarViewModel.Search = string.Empty;

        _ = await model.OnGetSearchAsync(CancellationToken.None);

        _nugetService.Verify(
            s => s.SearchPackagesAsync(It.IsAny<Guid>(), "MyDefaultTerm", It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnGetSearchAsync_ServiceThrows_LogsAndEnqueuesErrorAndReturnsPage()
    {
        _ = _nugetService
            .Setup(s => s.SearchPackagesAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        IndexModel model = CreateModel();
        model.PageContext = ServiceTestHelpers.CreatePageContext();

        IActionResult result = await model.OnGetSearchAsync(CancellationToken.None);

        _ = Assert.IsType<PageResult>(result);
        _notificationService.Verify(n => n.EnqueueErrorForNextPage(I18n.FailedToSearch), Times.Once);
        Assert.Empty(model.PackageListViewModel.PackageItems);
    }

    // ---------- OnPostInstallAsync ----------

    private static (byte[] Bytes, string DllEntryName) CreateFakeNupkgBytes(string packageId)
    {
        using MemoryStream memoryStream = new();
        string dllEntryName = $"lib/net8.0/{packageId}.dll";

        using (ZipArchive archive = new(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = archive.CreateEntry(dllEntryName);
            using Stream entryStream = entry.Open();
            byte[] fakeDllBytes = [0x01, 0x02, 0x03, 0x04];
            entryStream.Write(fakeDllBytes, 0, fakeDllBytes.Length);
        }

        // The zip central directory is only flushed to the stream when the archive is disposed,
        // so ToArray() must happen after the using block closes, not inside it.
        return (memoryStream.ToArray(), dllEntryName);
    }

    [Fact]
    public async Task OnPostInstallAsync_Success_InsertsCrawlerAgentAndRedirects()
    {
        Guid sourceId = Guid.NewGuid();
        const string packageId = "My.Crawler.Package";
        const string packageVersion = "1.0.0";

        (byte[] nupkgBytes, _) = CreateFakeNupkgBytes(packageId);

        _ = _nugetService
            .Setup(s => s.OnGetDownloadAsync(sourceId, packageId, packageVersion, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MemoryStream(nupkgBytes)]);

        CrawlerAgentAssembly fakeAssembly = new(typeof(object).Assembly, null!);

        _ = _crawlerAgentAssemblyLoader
            .Setup(l => l.GetIsolatedAssembly(It.Is<string>(p => p.EndsWith($"{packageId}.dll"))))
            .Returns(fakeAssembly);

        _ = _crawlerAgentAssemblyLoader
            .Setup(l => l.GetCrawlerDisplayName(It.IsAny<System.Reflection.Assembly>()))
            .Returns("My Crawler Display Name");

        _ = _crawlerAgentAssemblyLoader
            .Setup(l => l.GetAssemblyMetadata(It.IsAny<CrawlerAgentAssembly>()))
            .Returns(new Dictionary<string, string> { ["Version"] = "1.0.0" });

        IndexModel model = CreateModel();
        model.PageContext = ServiceTestHelpers.CreatePageContext();

        IActionResult result = await model.OnPostInstallAsync(sourceId, packageId, packageVersion, CancellationToken.None);

        RedirectToPageResult redirectResult = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/CrawlerAgents/Edit/Index", redirectResult.PageName);

        CrawlerAgent inserted = Assert.Single(_dbContext.CrawlerAgents.FindAll());
        Assert.Equal("My Crawler Display Name", inserted.DisplayName);
        _createdDirectories.Add(CrawlerAgent.GetCrawlerAgentDir($"{packageId}.{packageVersion}.nupkg"));

        _notificationService.Verify(n => n.EnqueueSuccessForNextPage(I18n.NuGetPackageInstalledSuccessfully), Times.Once);
    }

    [Fact]
    public async Task OnPostInstallAsync_ServiceThrows_EnqueuesErrorAndReturnsPageWithClearedState()
    {
        Guid sourceId = Guid.NewGuid();
        const string packageId = "Broken.Package";
        const string packageVersion = "1.0.0";

        _ = _nugetService
            .Setup(s => s.OnGetDownloadAsync(sourceId, packageId, packageVersion, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("download failed"));

        NugetSource source = new("Custom", new Uri("https://example.com/v3/index.json"), null, null);
        _ = _dbContext.NugetSources.Insert(source);

        IndexModel model = CreateModel();
        model.PageContext = ServiceTestHelpers.CreatePageContext();

        IActionResult result = await model.OnPostInstallAsync(sourceId, packageId, packageVersion, CancellationToken.None);

        _ = Assert.IsType<PageResult>(result);
        _notificationService.Verify(n => n.EnqueueErrorForNextPage(I18n.NuGetPackageIsInvalid), Times.Once);
        Assert.Empty(model.PackageListViewModel.PackageItems);
        _ = Assert.Single(model.SearchBarViewModel.Sources);
        Assert.Empty(_dbContext.CrawlerAgents.FindAll());
    }

    // ---------- OnGetDownloadAsync ----------

    [Theory]
    [InlineData("", "1.0.0")]
    [InlineData("Some.Package", "")]
    public async Task OnGetDownloadAsync_MissingParams_ReturnsBadRequest(string packageId, string packageVersion)
    {
        IndexModel model = CreateModel();

        IActionResult result = await model.OnGetDownloadAsync(Guid.NewGuid(), packageId, packageVersion, CancellationToken.None);

        _ = Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task OnGetDownloadAsync_EmptySourceId_ReturnsBadRequest()
    {
        IndexModel model = CreateModel();

        IActionResult result = await model.OnGetDownloadAsync(Guid.Empty, "Some.Package", "1.0.0", CancellationToken.None);

        _ = Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task OnGetDownloadAsync_NoStreams_ReturnsNotFound()
    {
        Guid sourceId = Guid.NewGuid();
        _ = _nugetService
            .Setup(s => s.OnGetDownloadAsync(sourceId, "Some.Package", "1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        IndexModel model = CreateModel();

        IActionResult result = await model.OnGetDownloadAsync(sourceId, "Some.Package", "1.0.0", CancellationToken.None);

        _ = Assert.IsType<NotFoundResult>(result);
        _notificationService.Verify(n => n.EnqueueErrorForNextPage(I18n.NuGetPackageIsInvalid), Times.Once);
    }

    [Fact]
    public async Task OnGetDownloadAsync_Success_ReturnsFileStreamResult()
    {
        Guid sourceId = Guid.NewGuid();
        byte[] content = [1, 2, 3];
        _ = _nugetService
            .Setup(s => s.OnGetDownloadAsync(sourceId, "Some.Package", "1.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MemoryStream(content)]);

        IndexModel model = CreateModel();

        IActionResult result = await model.OnGetDownloadAsync(sourceId, "Some.Package", "1.0.0", CancellationToken.None);

        FileStreamResult fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("application/octet-stream", fileResult.ContentType);
        Assert.Equal("Some.Package.1.0.0.nupkg", fileResult.FileDownloadName);
    }

    [Fact]
    public async Task OnGetDownloadAsync_OperationCanceled_ReturnsEmptyResult()
    {
        Guid sourceId = Guid.NewGuid();
        _ = _nugetService
            .Setup(s => s.OnGetDownloadAsync(sourceId, "Some.Package", "1.0.0", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        IndexModel model = CreateModel();

        IActionResult result = await model.OnGetDownloadAsync(sourceId, "Some.Package", "1.0.0", CancellationToken.None);

        _ = Assert.IsType<EmptyResult>(result);
    }

    [Fact]
    public async Task OnGetDownloadAsync_GeneralException_Returns500AndPushesError()
    {
        Guid sourceId = Guid.NewGuid();
        _ = _nugetService
            .Setup(s => s.OnGetDownloadAsync(sourceId, "Some.Package", "1.0.0", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        IndexModel model = CreateModel();

        IActionResult result = await model.OnGetDownloadAsync(sourceId, "Some.Package", "1.0.0", CancellationToken.None);

        StatusCodeResult statusCodeResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
        _notificationService.Verify(n => n.PushErrorAsync(I18n.NuGetPackageIsInvalid, It.IsAny<CancellationToken>()), Times.Once);
    }
}

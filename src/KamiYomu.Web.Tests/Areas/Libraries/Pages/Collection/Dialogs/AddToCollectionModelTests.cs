using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.CrawlerAgents.Core.Catalog.Builders;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Areas.Libraries.Pages.Collection.Dialogs;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Resources;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Tests.Areas.Libraries.Pages.Collection.Dialogs;

public class AddToCollectionModelTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");
    private readonly Mock<ICrawlerAgentRepository> _crawlerAgentRepository = new();
    private readonly IOptions<SpecialFolderOptions> _specialFolderOptions = Options.Create(new SpecialFolderOptions
    {
        MangaDir = "manga",
        AgentsDir = "agents",
        DbDir = "db",
        LogDir = "logs",
        FilePathFormat = "{manga_title}/default-path",
        ComicInfoTitleFormat = "default-title-{chapter_number}",
        ComicInfoSeriesFormat = "default-series"
    });
    private readonly IOptions<WorkerOptions> _workerOptions = Options.Create(new WorkerOptions
    {
        ServerAvailableNames = ["server-1"],
        DownloadChapterQueues = ["download"],
        MangaDownloadSchedulerQueues = ["manga"],
        DiscoveryNewChapterQueues = ["discovery"],
        DailyExecutionTime = TimeSpan.FromHours(4)
    });

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private AddToCollectionModel CreateModel()
    {
        return new AddToCollectionModel(_specialFolderOptions, _workerOptions, _dbContext, _crawlerAgentRepository.Object)
        {
            RefreshElementId = "refresh-default",
            FilePathTemplate = "{manga_title}/default-path",
            ComicInfoTitleTemplate = "default-title-{chapter_number}",
            ComicInfoSeriesTemplate = "default-series",
            MakeThisConfigurationDefault = false,
            PageContext = ServiceTestHelpers.CreatePageContext()
        };
    }

    [Fact]
    public async Task OnGetAsync_WithEmptyCrawlerAgentId_DoesNotLoadManga()
    {
        AddToCollectionModel model = CreateModel();

        await model.OnGetAsync(Guid.Empty, "manga-1", "refresh-1", CancellationToken.None);

        Assert.Null(model.Manga);
        _crawlerAgentRepository.Verify(
            r => r.GetMangaAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnGetAsync_WithValidIdsAndNoUserPreferenceTemplates_FallsBackToSpecialFolderDefaults()
    {
        _ = _dbContext.UserPreferences.Insert(new UserPreference(System.Globalization.CultureInfo.GetCultureInfo("en-US")));
        Manga manga = MangaBuilder.Create().WithTitle("Alpha").Build();
        Guid crawlerAgentId = Guid.NewGuid();
        _ = _crawlerAgentRepository
            .Setup(r => r.GetMangaAsync(crawlerAgentId, "manga-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(manga);

        AddToCollectionModel model = CreateModel();

        await model.OnGetAsync(crawlerAgentId, "manga-1", "refresh-1", CancellationToken.None);

        Assert.Same(manga, model.Manga);
        Assert.Equal(crawlerAgentId, model.CrawlerAgentId);
        Assert.Equal("manga-1", model.MangaId);
        Assert.Equal("refresh-1", model.RefreshElementId);
        Assert.Equal("{manga_title}/default-path", model.FilePathTemplate);
        Assert.Equal("default-title-{chapter_number}", model.ComicInfoTitleTemplate);
        Assert.Equal("default-series", model.ComicInfoSeriesTemplate);
        Assert.Equal(4, model.TemplateResults.Length);
        Assert.All(model.TemplateResults, r => Assert.False(string.IsNullOrWhiteSpace(r)));
    }

    [Fact]
    public async Task OnGetAsync_WhenTemplateProducesUniqueFileNames_DoesNotAddModelError()
    {
        UserPreference preference = new(System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        preference.SetFilePathTemplate("{manga_title}/chapter-{chapter_padded_4}");
        _ = _dbContext.UserPreferences.Insert(preference);
        Manga manga = MangaBuilder.Create().WithTitle("Alpha").Build();
        Guid crawlerAgentId = Guid.NewGuid();
        _ = _crawlerAgentRepository
            .Setup(r => r.GetMangaAsync(crawlerAgentId, "manga-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(manga);

        AddToCollectionModel model = CreateModel();

        await model.OnGetAsync(crawlerAgentId, "manga-1", "refresh-1", CancellationToken.None);

        Assert.True(model.ModelState.IsValid);
    }

    [Fact]
    public async Task OnGetAsync_WhenTemplateDoesNotProduceUniqueFileNames_AddsModelError()
    {
        Manga manga = MangaBuilder.Create().WithTitle("Alpha").Build();
        Guid crawlerAgentId = Guid.NewGuid();
        _ = _crawlerAgentRepository
            .Setup(r => r.GetMangaAsync(crawlerAgentId, "manga-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(manga);

        UserPreference preference = new(System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        preference.SetFilePathTemplate("{manga_title}/static-path");
        _ = _dbContext.UserPreferences.Insert(preference);

        AddToCollectionModel model = CreateModel();

        await model.OnGetAsync(crawlerAgentId, "manga-1", "refresh-1", CancellationToken.None);

        Assert.False(model.ModelState.IsValid);
        Assert.Contains(
            model.ModelState[nameof(AddToCollectionModel.FilePathTemplate)]!.Errors,
            e => e.ErrorMessage == I18n.TheTemplateMustProduceUniqueFileNames);
    }

    [Fact]
    public async Task OnPostPreviewAsync_ReturnsPathTemplatePreviewPartial()
    {
        Manga manga = MangaBuilder.Create().WithTitle("Alpha").Build();
        Guid crawlerAgentId = Guid.NewGuid();
        _ = _crawlerAgentRepository
            .Setup(r => r.GetMangaAsync(crawlerAgentId, "manga-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(manga);

        AddToCollectionModel model = CreateModel();
        model.CrawlerAgentId = crawlerAgentId;
        model.MangaId = "manga-1";
        model.FilePathTemplate = "{manga_title}/preview-path";

        IActionResult result = await model.OnPostPreviewAsync(CancellationToken.None);

        PartialViewResult partialResult = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_PathTemplatePreview", partialResult.ViewName);
        _ = Assert.IsAssignableFrom<string[]>(partialResult.Model);
    }

    [Fact]
    public async Task OnPostComicInfoPreviewAsync_ReturnsComicInfoTemplatePreviewPartial()
    {
        Manga manga = MangaBuilder.Create().WithTitle("Alpha").Build();
        Guid crawlerAgentId = Guid.NewGuid();
        _ = _crawlerAgentRepository
            .Setup(r => r.GetMangaAsync(crawlerAgentId, "manga-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(manga);

        AddToCollectionModel model = CreateModel();
        model.CrawlerAgentId = crawlerAgentId;
        model.MangaId = "manga-1";
        model.ComicInfoTitleTemplate = "{manga_title} Title";
        model.ComicInfoSeriesTemplate = "{manga_title} Series";

        IActionResult result = await model.OnPostComicInfoPreviewAsync(CancellationToken.None);

        PartialViewResult partialResult = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_ComicInfoTemplatePreview", partialResult.ViewName);
        ComicInfoTemplateViewModel resultModel = Assert.IsType<ComicInfoTemplateViewModel>(partialResult.Model);
        Assert.Equal("Alpha Title", resultModel.TitleTemplatePreview);
        Assert.Equal("Alpha Series", resultModel.SeriesTemplatePreview);
    }
}

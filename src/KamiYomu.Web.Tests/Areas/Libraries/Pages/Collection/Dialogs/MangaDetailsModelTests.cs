using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.CrawlerAgents.Core.Catalog.Builders;
using KamiYomu.Web.Areas.Libraries.Pages.Collection.Dialogs;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;

namespace KamiYomu.Web.Tests.Areas.Libraries.Pages.Collection.Dialogs;

public class MangaDetailsModelTests
{
    private readonly Mock<ICrawlerAgentRepository> _crawlerAgentRepository = new();

    private MangaDetailsModel CreateModel()
    {
        return new MangaDetailsModel(_crawlerAgentRepository.Object);
    }

    [Fact]
    public async Task OnGetAsync_WithValidIds_PopulatesManga()
    {
        Manga manga = MangaBuilder.Create().WithTitle("Alpha").Build();
        Guid crawlerAgentId = Guid.NewGuid();
        _ = _crawlerAgentRepository
            .Setup(r => r.GetMangaAsync(crawlerAgentId, "manga-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(manga);

        MangaDetailsModel model = CreateModel();

        await model.OnGetAsync(crawlerAgentId, "manga-1", CancellationToken.None);

        Assert.Same(manga, model.Manga);
    }

    [Fact]
    public async Task OnGetAsync_WithEmptyCrawlerAgentId_DoesNotCallRepository()
    {
        MangaDetailsModel model = CreateModel();

        await model.OnGetAsync(Guid.Empty, "manga-1", CancellationToken.None);

        Assert.Null(model.Manga);
        _crawlerAgentRepository.Verify(
            r => r.GetMangaAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task OnGetAsync_WithBlankMangaId_DoesNotCallRepository(string? mangaId)
    {
        MangaDetailsModel model = CreateModel();

        await model.OnGetAsync(Guid.NewGuid(), mangaId!, CancellationToken.None);

        Assert.Null(model.Manga);
        _crawlerAgentRepository.Verify(
            r => r.GetMangaAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

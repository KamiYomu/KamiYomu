using System.Reflection;

using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.CrawlerAgents.Core.Catalog.Builders;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.CrawlerAgentRuntime.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories;

namespace KamiYomu.Web.Tests.Infrastructure.Repositories;

[Collection(KamiYomu.Web.Tests.Infrastructure.AppServices.SharedInfrastructureStateCollection.Name)]
public class CrawlerAgentRepositoryTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");
    private readonly CacheContext _cacheContext = new();
    private readonly Mock<ICrawlerAgentFactory> _crawlerAgentFactory = new();
    private readonly CrawlerAgentRepository _repository;

    public CrawlerAgentRepositoryTests()
    {
        _cacheContext.EmptyAll();
        _repository = new CrawlerAgentRepository(_dbContext, _cacheContext, _crawlerAgentFactory.Object);
    }

    [Fact]
    public async Task GetMangaAsync_ReturnsCrawlerMangaAndCachesByAgentAndMangaId()
    {
        CrawlerAgent crawlerAgent = InsertCrawlerAgent(CreateCrawlerAgent("manga-agent.dll"));
        string mangaId = $"manga-{Guid.NewGuid():N}";
        Manga expected = CreateManga(mangaId, "Cached Manga");

        Mock<ICrawlerAgentDecorator> crawler = CreateCrawlerDecorator();
        _ = crawler.Setup(agent => agent.GetByIdAsync(mangaId, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        _ = _crawlerAgentFactory.Setup(factory => factory.Create(It.Is<CrawlerAgent>(agent => agent.Id == crawlerAgent.Id))).Returns(crawler.Object);

        Manga first = await _repository.GetMangaAsync(crawlerAgent.Id, mangaId, CancellationToken.None);
        Manga second = await _repository.GetMangaAsync(crawlerAgent.Id, mangaId, CancellationToken.None);

        Assert.Equal(expected.Id, first.Id);
        Assert.Equal(expected.Title, second.Title);

        _crawlerAgentFactory.Verify(factory => factory.Create(It.Is<CrawlerAgent>(agent => agent.Id == crawlerAgent.Id)), Times.Once);
        crawler.Verify(agent => agent.GetByIdAsync(mangaId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMangaChaptersAsync_UsesStoredLibraryMangaAndCachesResult()
    {
        CrawlerAgent crawlerAgent = InsertCrawlerAgent(CreateCrawlerAgent("chapters-agent.dll"));
        Manga manga = CreateManga($"manga-{Guid.NewGuid():N}", "Chapters Manga");
        Library library = InsertLibrary(CreateLibrary(crawlerAgent, manga));
        PaginationOptions paginationOptions = new(0, 10, null);

        Chapter chapter = CreateChapter(manga, $"chapter-{Guid.NewGuid():N}", 1, "Chapter 1");
        PagedResult<Chapter> expected = PagedResultBuilder<Chapter>.Create()
            .WithData([chapter])
            .WithPaginationOptions(paginationOptions)
            .Build();

        Mock<ICrawlerAgentDecorator> crawler = CreateCrawlerDecorator();
        _ = crawler
            .Setup(agent => agent.GetChaptersAsync(It.Is<Manga>(value => value.Id == library.Manga!.Id), paginationOptions, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        _ = _crawlerAgentFactory.Setup(factory => factory.Create(It.Is<CrawlerAgent>(agent => agent.Id == crawlerAgent.Id))).Returns(crawler.Object);

        PagedResult<Chapter> first = await _repository.GetMangaChaptersAsync(crawlerAgent.Id, manga.Id, paginationOptions, CancellationToken.None);
        PagedResult<Chapter> second = await _repository.GetMangaChaptersAsync(crawlerAgent.Id, manga.Id, paginationOptions, CancellationToken.None);

        _ = Assert.Single(first.Data);
        Assert.Equal(chapter.Id, first.Data.Single().Id);
        _ = Assert.Single(second.Data);

        _crawlerAgentFactory.Verify(factory => factory.Create(It.Is<CrawlerAgent>(agent => agent.Id == crawlerAgent.Id)), Times.Once);
        crawler.Verify(agent => agent.GetChaptersAsync(It.Is<Manga>(value => value.Id == library.Manga!.Id), paginationOptions, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetChapterPagesAsync_ReturnsPagesAndCachesResult()
    {
        CrawlerAgent crawlerAgent = InsertCrawlerAgent(CreateCrawlerAgent("pages-agent.dll"));
        Manga manga = CreateManga($"manga-{Guid.NewGuid():N}", "Pages Manga");
        Chapter chapter = CreateChapter(manga, $"chapter-{Guid.NewGuid():N}", 2, "Pages Chapter");

        Page page = PageBuilder.Create()
            .WithId($"page-{Guid.NewGuid():N}")
            .WithChapterId(chapter.Id)
            .WithPageNumber(1)
            .WithParentChapter(chapter)
            .WithImageUrl(new Uri("https://example.test/page-1.jpg"))
            .Build();

        Mock<ICrawlerAgentDecorator> crawler = CreateCrawlerDecorator();
        _ = crawler.Setup(agent => agent.GetChapterPagesAsync(chapter, It.IsAny<CancellationToken>())).ReturnsAsync([page]);

        _ = _crawlerAgentFactory.Setup(factory => factory.Create(It.Is<CrawlerAgent>(agent => agent.Id == crawlerAgent.Id))).Returns(crawler.Object);

        IEnumerable<Page> first = await _repository.GetChapterPagesAsync(crawlerAgent.Id, chapter, CancellationToken.None);
        IEnumerable<Page> second = await _repository.GetChapterPagesAsync(crawlerAgent.Id, chapter, CancellationToken.None);

        _ = Assert.Single(first);
        Assert.Equal(page.Id, first.Single().Id);
        _ = Assert.Single(second);

        _crawlerAgentFactory.Verify(factory => factory.Create(It.Is<CrawlerAgent>(agent => agent.Id == crawlerAgent.Id)), Times.Once);
        crawler.Verify(agent => agent.GetChapterPagesAsync(chapter, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ReturnsMatchesAndCachesSanitizedQueryKey()
    {
        CrawlerAgent crawlerAgent = InsertCrawlerAgent(CreateCrawlerAgent("search-agent.dll"));
        string query = $"one piece #{Guid.NewGuid():N}";
        PaginationOptions paginationOptions = new(0, 5, null);
        Manga manga = CreateManga($"manga-{Guid.NewGuid():N}", "One Piece");

        PagedResult<Manga> expected = PagedResultBuilder<Manga>.Create()
            .WithData([manga])
            .WithPaginationOptions(paginationOptions)
            .Build();

        Mock<ICrawlerAgentDecorator> crawler = CreateCrawlerDecorator();
        _ = crawler.Setup(agent => agent.SearchAsync(query, paginationOptions, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        _ = _crawlerAgentFactory.Setup(factory => factory.Create(It.Is<CrawlerAgent>(agent => agent.Id == crawlerAgent.Id))).Returns(crawler.Object);

        PagedResult<Manga> first = await _repository.SearchAsync(crawlerAgent.Id, query, paginationOptions, CancellationToken.None);
        PagedResult<Manga> second = await _repository.SearchAsync(crawlerAgent.Id, query, paginationOptions, CancellationToken.None);

        _ = Assert.Single(first.Data);
        Assert.Equal(manga.Id, first.Data.Single().Id);
        _ = Assert.Single(second.Data);

        _crawlerAgentFactory.Verify(factory => factory.Create(It.Is<CrawlerAgent>(agent => agent.Id == crawlerAgent.Id)), Times.Once);
        crawler.Verify(agent => agent.SearchAsync(query, paginationOptions, It.IsAny<CancellationToken>()), Times.Once);
    }

    public void Dispose()
    {
        _cacheContext.EmptyAll();
        _dbContext.Dispose();
    }

    private static Mock<ICrawlerAgentDecorator> CreateCrawlerDecorator()
    {
        Mock<ICrawlerAgentDecorator> crawler = new();
        _ = crawler.Setup(agent => agent.Dispose());
        return crawler;
    }

    private static CrawlerAgent CreateCrawlerAgent(string assemblyName)
    {
        return WithId(new CrawlerAgent(Path.Combine("C:\\agents", assemblyName), Path.GetFileNameWithoutExtension(assemblyName), []), Guid.NewGuid());
    }

    private static Manga CreateManga(string id, string title)
    {
        Manga manga = MangaBuilder.Create()
            .WithTitle(title)
            .WithIsFamilySafe(true)
            .Build();

        return WithId(manga, id);
    }

    private static Chapter CreateChapter(Manga parentManga, string id, decimal number, string title)
    {
        Chapter chapter = ChapterBuilder.Create()
            .WithNumber(number)
            .WithTitle(title)
            .WithParentManga(parentManga)
            .Build();

        return WithId(chapter, id);
    }

    private static Library CreateLibrary(CrawlerAgent crawlerAgent, Manga manga)
    {
        return WithId(new Library(crawlerAgent, manga, "{manga_title}\\{manga_title} ch.{chapter_padded_4}", "{manga_title} ch.{chapter_padded_4}", "{manga_title}"), Guid.NewGuid());
    }

    private static T WithId<T>(T entity, Guid id)
    {
        PropertyInfo property = typeof(T).GetProperty(nameof(Library.Id))!;
        _ = property.GetSetMethod(true)!.Invoke(entity, [id]);
        return entity;
    }

    private static T WithId<T>(T entity, string id)
    {
        PropertyInfo property = typeof(T).GetProperty(nameof(Manga.Id))!;
        _ = property.GetSetMethod(true)!.Invoke(entity, [id]);
        return entity;
    }

    private CrawlerAgent InsertCrawlerAgent(CrawlerAgent crawlerAgent)
    {
        _ = _dbContext.CrawlerAgents.Insert(crawlerAgent);
        return crawlerAgent;
    }

    private Library InsertLibrary(Library library)
    {
        _ = _dbContext.Libraries.Insert(library);
        return library;
    }
}

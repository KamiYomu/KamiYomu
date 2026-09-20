using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.CrawlerAgents.Core.Catalog.Builders;
using KamiYomu.Web.Areas.Reader.Data;
using KamiYomu.Web.Areas.Reader.Models;
using KamiYomu.Web.Areas.Reader.Repositories;
using KamiYomu.Web.Areas.Reader.ViewModels;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Tests.Infrastructure.Services;

namespace KamiYomu.Web.Tests.Areas.Reader.Repositories;

public class ChapterProgressRepositoryTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "KamiYomu.Tests.ChapterProgressRepository", Guid.NewGuid().ToString("N"));
    private readonly DbContext _dbContext;
    private readonly ReadingDbContext _readingDbContext;
    private readonly CacheContext _cacheContext = new();
    private readonly Mock<IUserClockManager> _userClockManager = new();
    private readonly ChapterProgressRepository _repository;

    public ChapterProgressRepositoryTests()
    {
        _ = Directory.CreateDirectory(_rootPath);
        _dbContext = new DbContext(":memory:");
        _readingDbContext = new ReadingDbContext(Path.Combine(_rootPath, "reading.db"), false);
        LibraryDbContext.DatabaseFilePathResolver = libraryId => Path.Combine(_rootPath, $"lib{libraryId}.db");

        _ = _userClockManager.Setup(c => c.ConvertToUserTime(It.IsAny<DateTimeOffset>()))
            .Returns((DateTimeOffset dt) => dt);

        _repository = new ChapterProgressRepository(_dbContext, _readingDbContext, _cacheContext, _userClockManager.Object);
    }

    public void Dispose()
    {
        LibraryDbContext.DatabaseFilePathResolver = ServiceTestHelpers.DefaultLibraryDbContextResolver;
        _dbContext.Dispose();
        _readingDbContext.Dispose();

        try
        {
            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(_rootPath, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    private void InsertUserPreference(bool familySafeMode = false)
    {
        UserPreference preference = new(System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        preference.SetFamilySafeMode(familySafeMode);
        _ = _dbContext.UserPreferences.Insert(preference);
    }

    [Fact]
    public void FetchHistory_WhenNoProgressExists_ReturnsEmpty()
    {
        InsertUserPreference();

        IEnumerable<IGrouping<DateTime, ChapterViewModel>> result = _repository.FetchHistory(0, 20);

        Assert.Empty(result);
    }

    [Fact]
    public void FetchHistory_ReturnsProgressGroupedByDateDescending()
    {
        InsertUserPreference();
        Library library = ServiceTestHelpers.CreateLibrary("Alpha");
        _ = _dbContext.Libraries.Insert(library);

        ChapterProgress older = new(library.Id, Guid.NewGuid(), 1);
        older.SetLastPageRead(2, 10);
        older.LastReadAt = DateTimeOffset.UtcNow.AddDays(-2);
        _ = _readingDbContext.ChapterProgress.Insert(older);

        ChapterProgress newer = new(library.Id, Guid.NewGuid(), 2);
        newer.SetLastPageRead(3, 10);
        newer.LastReadAt = DateTimeOffset.UtcNow;
        _ = _readingDbContext.ChapterProgress.Insert(newer);

        List<IGrouping<DateTime, ChapterViewModel>> result = _repository.FetchHistory(0, 20).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(newer.LastReadAt.Date, result[0].Key);
        Assert.Equal(older.LastReadAt.Date, result[1].Key);
        Assert.All(result.SelectMany(g => g), cm => Assert.Equal(library.Id, cm.Library.Id));
    }

    [Fact]
    public void FetchHistory_ExcludesProgressWhenLibraryNoLongerExists()
    {
        InsertUserPreference();

        ChapterProgress orphaned = new(Guid.NewGuid(), Guid.NewGuid(), 1);
        orphaned.SetLastPageRead(1, 10);
        _ = _readingDbContext.ChapterProgress.Insert(orphaned);

        IEnumerable<IGrouping<DateTime, ChapterViewModel>> result = _repository.FetchHistory(0, 20);

        Assert.Empty(result);
    }

    [Fact]
    public void FetchHistory_WhenFamilySafeModeOnAndLibraryNotFamilySafe_ExcludesLibrary()
    {
        InsertUserPreference(familySafeMode: true);
        Library library = CreateNonFamilySafeLibrary("Adult Manga");
        _ = _dbContext.Libraries.Insert(library);

        ChapterProgress progress = new(library.Id, Guid.NewGuid(), 1);
        progress.SetLastPageRead(1, 10);
        _ = _readingDbContext.ChapterProgress.Insert(progress);

        IEnumerable<IGrouping<DateTime, ChapterViewModel>> result = _repository.FetchHistory(0, 20);

        Assert.Empty(result);
    }

    [Fact]
    public void FetchHistory_RespectsOffsetAndLimit()
    {
        InsertUserPreference();
        Library library = ServiceTestHelpers.CreateLibrary("Alpha");
        _ = _dbContext.Libraries.Insert(library);

        for (int i = 0; i < 5; i++)
        {
            ChapterProgress progress = new(library.Id, Guid.NewGuid(), i + 1);
            progress.SetLastPageRead(1, 10);
            progress.LastReadAt = DateTimeOffset.UtcNow.AddMinutes(-i);
            _ = _readingDbContext.ChapterProgress.Insert(progress);
        }

        List<ChapterViewModel> result = _repository.FetchHistory(1, 2).SelectMany(g => g).ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FetchWeeklyChapters_WhenNoLibraries_ReturnsEmpty()
    {
        ServiceTestHelpers.InitializeCache(nameof(ChapterProgressRepositoryTests) + "-empty");
        InsertUserPreference();

        IEnumerable<WeeklyChapterViewModel> result = _repository.FetchWeeklyChapters(7);

        Assert.Empty(result);
    }

    [Fact]
    public void FetchWeeklyChapters_WhenCompletedChapterRecentAndNotRead_IncludesLibrary()
    {
        ServiceTestHelpers.InitializeCache(nameof(ChapterProgressRepositoryTests) + "-included");
        InsertUserPreference();
        StoredLibraryRecord stored = ServiceTestHelpers.CreateCompletedStoredChapter(_dbContext, 1, "Chapter 1");

        List<WeeklyChapterViewModel> result = _repository.FetchWeeklyChapters(7).ToList();

        WeeklyChapterViewModel weekly = Assert.Single(result);
        Assert.Equal(stored.Library.Id, weekly.LibraryId);
        WeeklyChapterItemViewModel item = Assert.Single(weekly.Items);
        Assert.Equal(stored.ChapterDownload.Id, item.ChapterDownloadId);
    }

    [Fact]
    public void FetchWeeklyChapters_WhenChapterAlreadyCompletedInReadingProgress_ExcludesChapterButKeepsLibraryEntry()
    {
        // NOTE: production behavior - FetchWeeklyChapters adds a WeeklyChapterViewModel for the
        // library whenever it has *any* recently-completed download (chapterRecords.Any()), even
        // if every one of those chapters has already been fully read (so the filtered
        // "chapterDownloads"/Items ends up empty). This looks like it may be an oversight (the
        // outer guard probably intended to check the filtered list), but it is pre-existing
        // behavior and out of scope for a testability-only refactor, so this test documents the
        // current behavior rather than the possibly-intended one.
        ServiceTestHelpers.InitializeCache(nameof(ChapterProgressRepositoryTests) + "-alreadyread");
        InsertUserPreference();
        StoredLibraryRecord stored = ServiceTestHelpers.CreateCompletedStoredChapter(_dbContext, 1, "Chapter 1");

        ChapterProgress progress = new(stored.Library.Id, stored.ChapterDownload.Id, 1);
        progress.SetAsCompleted(10);
        _ = _readingDbContext.ChapterProgress.Insert(progress);

        List<WeeklyChapterViewModel> result = _repository.FetchWeeklyChapters(7).ToList();

        WeeklyChapterViewModel weekly = Assert.Single(result);
        Assert.Empty(weekly.Items);
    }

    [Fact]
    public void FetchWeeklyChapters_WhenFamilySafeModeOnAndLibraryNotFamilySafe_ExcludesLibrary()
    {
        ServiceTestHelpers.InitializeCache(nameof(ChapterProgressRepositoryTests) + "-familysafe");
        InsertUserPreference(familySafeMode: true);
        Library library = CreateNonFamilySafeLibrary("Adult Manga");
        _ = _dbContext.Libraries.Insert(library);

        List<WeeklyChapterViewModel> result = _repository.FetchWeeklyChapters(7).ToList();

        Assert.Empty(result);
    }

    private static Library CreateNonFamilySafeLibrary(string title)
    {
        ServiceTestHelpers.EnsureServiceLocatorConfigured();

        Manga manga = MangaBuilder.Create()
            .WithTitle(title)
            .WithOriginalLanguage("ja")
            .WithTags(["mature"])
            .WithCoverUrl(new Uri("https://example.com/cover.png"))
            .WithWebsiteUrl("https://example.com/manga")
            .WithIsFamilySafe(false)
            .Build();

        Library library = new(
            new CrawlerAgent("Test.Agent.dll", "Test Agent", new Dictionary<string, object>()),
            manga,
            "{manga_title}/chapter-{chapter_padded_4}",
            "{manga_title} ch.{chapter_padded_4}",
            "{manga_title}");

        ServiceTestHelpers.AssignId(library);
        ServiceTestHelpers.AssignId(library.CrawlerAgent);

        return library;
    }
}

using System.Globalization;
using System.Reflection;

using Hangfire;
using Hangfire.States;
using Hangfire.Storage.SQLite;

using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.CrawlerAgents.Core.Catalog.Builders;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Infrastructure.AppServices;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Models;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Tests.Infrastructure.AppServices;

[Collection(SharedInfrastructureStateCollection.Name)]
public class DownloadAppServiceTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");
    private readonly Mock<ICrawlerAgentRepository> _crawlerAgentRepository = new();
    private readonly Mock<IWorkerService> _workerService = new();
    private readonly Mock<IHangfireRepository> _hangfireRepository = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly DownloadAppService _service;

    public DownloadAppServiceTests(SharedInfrastructureStateFixture fixture)
    {
        Fixture = fixture;

        _ = _notificationService
            .Setup(service => service.PushSuccessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _service = new DownloadAppService(
            NullLogger<DownloadAppService>.Instance,
            Options.Create(fixture.SpecialFolderOptions),
            Options.Create(fixture.WorkerOptions),
            _dbContext,
            _crawlerAgentRepository.Object,
            _workerService.Object,
            _hangfireRepository.Object,
            _notificationService.Object);
    }

    private SharedInfrastructureStateFixture Fixture { get; }

    [Fact]
    public async Task AddToCollectionAsync_UsesDefaultTemplatesAndUpdatesUserPreferencesWhenRequested()
    {
        CrawlerAgent crawlerAgent = InsertCrawlerAgent(_dbContext, CreateCrawlerAgent("add-defaults-agent.dll"));
        UserPreference userPreference = new(CultureInfo.GetCultureInfo("en-US"));
        userPreference.SetFilePathTemplate("old-path");
        userPreference.SetComicInfoTitleTemplate("old-title");
        userPreference.SetComicInfoSeriesTemplate("old-series");
        userPreference.SetDailyExecutionTime(TimeSpan.FromHours(1));
        _ = _dbContext.UserPreferences.Insert(userPreference);

        Manga manga = CreateManga($"manga-{Guid.NewGuid():N}", "Defaulted Manga");

        AddItemCollection request = new()
        {
            CrawlerAgentId = crawlerAgent.Id,
            MangaId = manga.Id,
            FilePathTemplate = null,
            ComicInfoTitleTemplate = null,
            ComicInfoSeriesTemplate = null,
            MakeThisConfigurationDefault = true,
            DailyExecutionSchedule = TimeSpan.FromHours(4)
        };

        _ = _crawlerAgentRepository
            .Setup(repository => repository.GetMangaAsync(crawlerAgent.Id, manga.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(manga);
        _ = _workerService
            .Setup(service => service.ScheduleMangaDownload(It.IsAny<MangaDownloadRecord>(), request.DailyExecutionSchedule))
            .Returns("add-defaults-job");

        Library library = await _service.AddToCollectionAsync(request, CancellationToken.None);

        Assert.Equal(Fixture.SpecialFolderOptions.FilePathFormat, library.FilePathTemplate);
        Assert.Equal(Fixture.SpecialFolderOptions.ComicInfoTitleFormat, library.ComicInfoTitleTemplateFormat);
        Assert.Equal(Fixture.SpecialFolderOptions.ComicInfoSeriesFormat, library.ComicInfoSeriesTemplate);

        Library storedLibrary = _dbContext.Libraries.FindById(library.Id);
        Assert.NotNull(storedLibrary);
        Assert.Equal(manga.Id, storedLibrary.Manga?.Id);

        UserPreference storedPreference = _dbContext.UserPreferences.Query().First();
        Assert.Equal(Fixture.SpecialFolderOptions.FilePathFormat, storedPreference.FilePathTemplate);
        Assert.Equal(Fixture.SpecialFolderOptions.ComicInfoTitleFormat, storedPreference.ComicInfoTitleTemplate);
        Assert.Equal(Fixture.SpecialFolderOptions.ComicInfoSeriesFormat, storedPreference.ComicInfoSeriesTemplate);
        Assert.Equal(TimeSpan.FromHours(4), storedPreference.DailyExecutionTime);

        using LibraryDbContext libraryDbContext = new(library.Id);
        MangaDownloadRecord downloadRecord = libraryDbContext.MangaDownloadRecords.FindOne(x => x.Library.Id == library.Id);

        Assert.NotNull(downloadRecord);
        Assert.Equal(DownloadStatus.Scheduled, downloadRecord.DownloadStatus);
        Assert.Equal("add-defaults-job", downloadRecord.BackgroundJobId);

        _workerService.Verify(service => service.ScheduleMangaDownload(It.IsAny<MangaDownloadRecord>(), TimeSpan.FromHours(4)), Times.Once);
        _notificationService.Verify(service => service.PushSuccessAsync(It.Is<string>(message => message.Contains(manga.Title, StringComparison.Ordinal)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddToCollectionAsync_UsesCustomTemplatesAndWorkerDefaultScheduleWithoutChangingPreferences()
    {
        CrawlerAgent crawlerAgent = InsertCrawlerAgent(_dbContext, CreateCrawlerAgent("add-custom-agent.dll"));
        UserPreference userPreference = new(CultureInfo.GetCultureInfo("en-US"));
        userPreference.SetFilePathTemplate("keep-path");
        userPreference.SetComicInfoTitleTemplate("keep-title");
        userPreference.SetComicInfoSeriesTemplate("keep-series");
        userPreference.SetDailyExecutionTime(TimeSpan.FromHours(2));
        _ = _dbContext.UserPreferences.Insert(userPreference);

        Manga manga = CreateManga($"manga-{Guid.NewGuid():N}", "Custom Manga");

        AddItemCollection request = new()
        {
            CrawlerAgentId = crawlerAgent.Id,
            MangaId = manga.Id,
            FilePathTemplate = "custom\\path\\{manga_title}",
            ComicInfoTitleTemplate = "custom title {chapter}",
            ComicInfoSeriesTemplate = "custom series {manga_title}",
            MakeThisConfigurationDefault = false,
            DailyExecutionSchedule = null
        };

        _ = _crawlerAgentRepository
            .Setup(repository => repository.GetMangaAsync(crawlerAgent.Id, manga.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(manga);
        _ = _workerService
            .Setup(service => service.ScheduleMangaDownload(It.IsAny<MangaDownloadRecord>(), Fixture.WorkerOptions.DailyExecutionTime))
            .Returns("add-custom-job");

        Library library = await _service.AddToCollectionAsync(request, CancellationToken.None);

        Assert.Equal(request.FilePathTemplate, library.FilePathTemplate);
        Assert.Equal(request.ComicInfoTitleTemplate, library.ComicInfoTitleTemplateFormat);
        Assert.Equal(request.ComicInfoSeriesTemplate, library.ComicInfoSeriesTemplate);

        UserPreference storedPreference = _dbContext.UserPreferences.Query().First();
        Assert.Equal("keep-path", storedPreference.FilePathTemplate);
        Assert.Equal("keep-title", storedPreference.ComicInfoTitleTemplate);
        Assert.Equal("keep-series", storedPreference.ComicInfoSeriesTemplate);
        Assert.Equal(TimeSpan.FromHours(2), storedPreference.DailyExecutionTime);

        _workerService.Verify(service => service.ScheduleMangaDownload(It.IsAny<MangaDownloadRecord>(), Fixture.WorkerOptions.DailyExecutionTime), Times.Once);
    }

    [Fact]
    public async Task RemoveFromCollectionAsync_RemovesLibraryCancelsDownloadAndDeletesLibraryDatabase()
    {
        CrawlerAgent crawlerAgent = InsertCrawlerAgent(_dbContext, CreateCrawlerAgent("remove-agent.dll"));
        Library library = InsertLibrary(_dbContext, CreateLibrary(crawlerAgent, CreateManga($"manga-{Guid.NewGuid():N}", "Remove Manga")));

        MangaDownloadRecord mangaDownloadRecord = WithId(new MangaDownloadRecord(library, "remove-job"), Guid.NewGuid());
        mangaDownloadRecord.Schedule("remove-job");

        using (LibraryDbContext libraryDbContext = new(library.Id))
        {
            _ = libraryDbContext.MangaDownloadRecords.Insert(mangaDownloadRecord);
        }

        string databaseFile = new LibraryDbContext(library.Id).DatabaseFilePath();
        Assert.True(File.Exists(databaseFile));

        RemoveItemCollection request = new()
        {
            MangaId = library.Manga!.Id,
            CrawlerAgentId = crawlerAgent.Id
        };

        Library removedLibrary = await _service.RemoveFromCollectionAsync(request, CancellationToken.None);

        Assert.Equal(library.Id, removedLibrary.Id);
        Assert.Null(_dbContext.Libraries.FindById(library.Id));
        Assert.False(File.Exists(databaseFile));

        _workerService.Verify(service => service.CancelMangaDownload(It.Is<MangaDownloadRecord>(record => record.Id == mangaDownloadRecord.Id)), Times.Once);
        _notificationService.Verify(service => service.PushSuccessAsync(It.Is<string>(message => message.Contains("Remove Manga", StringComparison.Ordinal)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_ReturnsNullWhenLibraryDoesNotExist()
    {
        ChapterDownloadRecord? result = await _service.CancelAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task CancelAsync_ReturnsNullWhenChapterCannotBeCancelled()
    {
        ConfigureHangfireStorage();

        CrawlerAgent crawlerAgent = InsertCrawlerAgent(_dbContext, CreateCrawlerAgent("cancel-non-cancellable-agent.dll"));
        Library library = InsertLibrary(_dbContext, CreateLibrary(crawlerAgent, CreateManga($"manga-{Guid.NewGuid():N}", "Completed Manga")));
        MangaDownloadRecord mangaDownloadRecord = WithId(new MangaDownloadRecord(library, "manga-job"), Guid.NewGuid());

        Chapter chapter = CreateChapter(library.Manga!, $"chapter-{Guid.NewGuid():N}", 1, "Completed Chapter");
        ChapterDownloadRecord chapterDownloadRecord = WithId(new ChapterDownloadRecord(crawlerAgent, mangaDownloadRecord, chapter), Guid.NewGuid());
        chapterDownloadRecord.Scheduled("job-to-complete");
        chapterDownloadRecord.Complete();

        using (LibraryDbContext libraryDbContext = new(library.Id))
        {
            _ = libraryDbContext.MangaDownloadRecords.Insert(mangaDownloadRecord);
            _ = libraryDbContext.ChapterDownloadRecords.Insert(chapterDownloadRecord);
        }

        ChapterDownloadRecord? result = await _service.CancelAsync(library.Id, chapterDownloadRecord.Id, CancellationToken.None);

        Assert.Null(result);
        _notificationService.Verify(service => service.PushSuccessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelAsync_CancelsChapterDownloadAndPersistsChanges()
    {
        ConfigureHangfireStorage();

        CrawlerAgent crawlerAgent = InsertCrawlerAgent(_dbContext, CreateCrawlerAgent("cancel-agent.dll"));
        Library library = InsertLibrary(_dbContext, CreateLibrary(crawlerAgent, CreateManga($"manga-{Guid.NewGuid():N}", "Cancelable Manga")));
        MangaDownloadRecord mangaDownloadRecord = WithId(new MangaDownloadRecord(library, "manga-job"), Guid.NewGuid());

        Chapter chapter = CreateChapter(library.Manga!, $"chapter-{Guid.NewGuid():N}", 5, "Cancelable Chapter");
        ChapterDownloadRecord chapterDownloadRecord = WithId(new ChapterDownloadRecord(crawlerAgent, mangaDownloadRecord, chapter), Guid.NewGuid());

        // BackgroundJob.Delete goes through the real (SQLite-backed) JobStorage configured by
        // ConfigureHangfireStorage, so the job id must correspond to an actual stored job rather
        // than an arbitrary string.
        string realJobId = BackgroundJob.Enqueue(() => NoOpJob());
        chapterDownloadRecord.Scheduled(realJobId);

        using (LibraryDbContext insertDbContext = new(library.Id))
        {
            _ = insertDbContext.MangaDownloadRecords.Insert(mangaDownloadRecord);
            _ = insertDbContext.ChapterDownloadRecords.Insert(chapterDownloadRecord);
        }

        ChapterDownloadRecord? result = await _service.CancelAsync(library.Id, chapterDownloadRecord.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DownloadStatus.Cancelled, result.DownloadStatus);
        Assert.True(string.IsNullOrEmpty(result.BackgroundJobId));

        using LibraryDbContext libraryDbContext = new(library.Id);
        ChapterDownloadRecord storedRecord = libraryDbContext.ChapterDownloadRecords.FindById(chapterDownloadRecord.Id);
        Assert.Equal(DownloadStatus.Cancelled, storedRecord.DownloadStatus);
        // LiteDB round-trips an empty string BackgroundJobId as null; both mean "no job id".
        Assert.True(string.IsNullOrEmpty(storedRecord.BackgroundJobId));

        _notificationService.Verify(service => service.PushSuccessAsync(It.Is<string>(message => message.Contains("Cancelable Manga ch.0005.cbz", StringComparison.Ordinal)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RescheduleAsync_ReturnsNullWhenLibraryDoesNotExist()
    {
        ChapterDownloadRecord? result = await _service.RescheduleAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RescheduleAsync_ReturnsNullWhenChapterIsNotReschedulable()
    {
        ConfigureHangfireStorage();

        CrawlerAgent crawlerAgent = InsertCrawlerAgent(_dbContext, CreateCrawlerAgent("not-reschedulable-agent.dll"));
        Library library = InsertLibrary(_dbContext, CreateLibrary(crawlerAgent, CreateManga($"manga-{Guid.NewGuid():N}", "Scheduled Manga")));
        MangaDownloadRecord mangaDownloadRecord = WithId(new MangaDownloadRecord(library, "manga-job"), Guid.NewGuid());

        Chapter chapter = CreateChapter(library.Manga!, $"chapter-{Guid.NewGuid():N}", 3, "Scheduled Chapter");
        ChapterDownloadRecord chapterDownloadRecord = WithId(new ChapterDownloadRecord(crawlerAgent, mangaDownloadRecord, chapter), Guid.NewGuid());
        chapterDownloadRecord.Scheduled("already-scheduled");

        using (LibraryDbContext libraryDbContext = new(library.Id))
        {
            _ = libraryDbContext.MangaDownloadRecords.Insert(mangaDownloadRecord);
            _ = libraryDbContext.ChapterDownloadRecords.Insert(chapterDownloadRecord);
        }

        ChapterDownloadRecord? result = await _service.RescheduleAsync(library.Id, chapterDownloadRecord.Id, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RescheduleAsync_DeletesExistingFileEnqueuesJobAndUpdatesRecord()
    {
        ConfigureHangfireStorage();

        CrawlerAgent crawlerAgent = InsertCrawlerAgent(_dbContext, CreateCrawlerAgent("reschedule-agent.dll"));
        Library library = InsertLibrary(_dbContext, CreateLibrary(crawlerAgent, CreateManga($"manga-{Guid.NewGuid():N}", "Reschedule Manga")));
        MangaDownloadRecord mangaDownloadRecord = WithId(new MangaDownloadRecord(library, "manga-job"), Guid.NewGuid());

        Chapter chapter = CreateChapter(library.Manga!, $"chapter-{Guid.NewGuid():N}", 7, "Reschedule Chapter");
        ChapterDownloadRecord chapterDownloadRecord = WithId(new ChapterDownloadRecord(crawlerAgent, mangaDownloadRecord, chapter), Guid.NewGuid());
        chapterDownloadRecord.Scheduled("completed-job");
        chapterDownloadRecord.Complete();

        using (LibraryDbContext insertDbContext = new(library.Id))
        {
            _ = insertDbContext.MangaDownloadRecords.Insert(mangaDownloadRecord);
            _ = insertDbContext.ChapterDownloadRecords.Insert(chapterDownloadRecord);
        }

        string cbzPath = library.GetCbzFilePath(chapter);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(cbzPath)!);
        await File.WriteAllTextAsync(cbzPath, "test payload");

        _ = _hangfireRepository
            .Setup(repository => repository.GetLeastLoadedDownloadChapterQueue())
            .Returns(new EnqueuedState("chapter-queue-a"));

        ChapterDownloadRecord? result = await _service.RescheduleAsync(library.Id, chapterDownloadRecord.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DownloadStatus.Scheduled, result.DownloadStatus);
        Assert.False(string.IsNullOrWhiteSpace(result.BackgroundJobId));
        Assert.False(File.Exists(cbzPath));

        using LibraryDbContext libraryDbContext = new(library.Id);
        ChapterDownloadRecord storedRecord = libraryDbContext.ChapterDownloadRecords.FindById(chapterDownloadRecord.Id);
        Assert.Equal(DownloadStatus.Scheduled, storedRecord.DownloadStatus);
        Assert.False(string.IsNullOrWhiteSpace(storedRecord.BackgroundJobId));

        _hangfireRepository.Verify(repository => repository.GetLeastLoadedDownloadChapterQueue(), Times.Once);
        _notificationService.Verify(service => service.PushSuccessAsync(It.Is<string>(message => message.Contains("Reschedule Manga ch.0007.cbz", StringComparison.Ordinal)), It.IsAny<CancellationToken>()), Times.Once);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    public static void NoOpJob()
    {
    }

    private void ConfigureHangfireStorage()
    {
        string hangfireDbPath = Path.Combine(Fixture.RootPath, "hangfire", $"hangfire-{Guid.NewGuid():N}.db");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(hangfireDbPath)!);
        JobStorage.Current = new SQLiteStorage(hangfireDbPath);
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

    private static CrawlerAgent InsertCrawlerAgent(DbContext dbContext, CrawlerAgent crawlerAgent)
    {
        _ = dbContext.CrawlerAgents.Insert(crawlerAgent);
        return crawlerAgent;
    }

    private static Library InsertLibrary(DbContext dbContext, Library library)
    {
        _ = dbContext.Libraries.Insert(library);
        return library;
    }
}

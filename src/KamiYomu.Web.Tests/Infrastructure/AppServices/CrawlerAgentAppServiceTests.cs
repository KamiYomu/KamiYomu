using System.Reflection;

using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.CrawlerAgents.Core.Catalog.Builders;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Infrastructure.AppServices;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Resources;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MonkeyCache;
using MonkeyCache.LiteDB;

namespace KamiYomu.Web.Tests.Infrastructure.AppServices;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SharedInfrastructureStateCollection : ICollectionFixture<SharedInfrastructureStateFixture>
{
    public const string Name = "shared-infrastructure-state";
}

public sealed class SharedInfrastructureStateFixture : IDisposable
{
    public SharedInfrastructureStateFixture()
    {
        Defaults.LiteDbConfig.Configure();
        Barrel.ApplicationId = $"KamiYomu.Web.Tests.{Guid.NewGuid():N}";

        RootPath = Path.Combine(Path.GetTempPath(), "KamiYomu.Tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(RootPath);
        _ = Directory.CreateDirectory(Path.Combine(RootPath, "library-db"));
        _ = Directory.CreateDirectory(Path.Combine(RootPath, "manga"));

        SpecialFolderOptions = new SpecialFolderOptions
        {
            AgentsDir = Path.Combine(RootPath, "agents"),
            DbDir = Path.Combine(RootPath, "db"),
            LogDir = Path.Combine(RootPath, "logs"),
            MangaDir = Path.Combine(RootPath, "manga"),
            FilePathFormat = "{manga_title}\\{manga_title} ch.{chapter_padded_4}",
            ComicInfoTitleFormat = "{manga_title} ch.{chapter_padded_4}",
            ComicInfoSeriesFormat = "{manga_title}"
        };

        WorkerOptions = new WorkerOptions
        {
            ServerAvailableNames = ["server-1"],
            DownloadChapterQueues = ["chapter-queue-a", "chapter-queue-b"],
            MangaDownloadSchedulerQueues = ["manga-queue-a", "manga-queue-b"],
            DiscoveryNewChapterQueues = ["discovery-queue-a"],
            DailyExecutionTime = TimeSpan.FromHours(19)
        };

        _serviceProvider = new ServiceCollection()
            .AddSingleton<IOptions<SpecialFolderOptions>>(Options.Create(SpecialFolderOptions))
            .AddLogging()
            .AddSingleton<ILockManager, NoOpLockManager>()
            .BuildServiceProvider();

        Defaults.ServiceLocator.Configure(() => _serviceProvider);
        LibraryDbContext.DatabaseFilePathResolver = libraryId => Path.Combine(RootPath, "library-db", $"lib{libraryId}.db");
    }

    public string RootPath { get; }

    public SpecialFolderOptions SpecialFolderOptions { get; }

    public WorkerOptions WorkerOptions { get; }

    public void Dispose()
    {
        LibraryDbContext.DatabaseFilePathResolver = ServiceTestHelpers.DefaultLibraryDbContextResolver;
        TryDeleteDirectoryWithRetries(RootPath);

        _serviceProvider.Dispose();
    }

    private static void TryDeleteDirectoryWithRetries(string path)
    {
        // Hangfire's SQLite storage keeps background threads with open file handles to its
        // database files even after the tests using them finish. Best-effort clean up of the
        // temp artifacts: delete whatever can be deleted and silently skip anything still
        // locked by a lingering background thread rather than failing collection teardown.
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        try
        {
            Directory.Delete(path, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private readonly ServiceProvider _serviceProvider;
}

internal sealed class NoOpLockManager : ILockManager
{
    public IDisposable? TryAcquireAsync(string crawlerId) => new NoOpHandle();

    private sealed class NoOpHandle : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

[Collection(SharedInfrastructureStateCollection.Name)]
public class CrawlerAgentAppServiceTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");
    private readonly Mock<ICrawlerAgentRepository> _crawlerAgentRepository = new();
    private readonly Mock<IWorkerService> _workerService = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly CrawlerAgentAppService _service;

    public CrawlerAgentAppServiceTests(SharedInfrastructureStateFixture fixture)
    {
        Fixture = fixture;

        _ = _notificationService
            .Setup(service => service.PushInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _ = _notificationService
            .Setup(service => service.PushSuccessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _service = new CrawlerAgentAppService(
            NullLogger<CrawlerAgentAppService>.Instance,
            _dbContext,
            _crawlerAgentRepository.Object,
            _workerService.Object,
            _notificationService.Object);
    }

    private SharedInfrastructureStateFixture Fixture { get; }

    [Fact]
    public async Task RefreshCollectionAsync_CreatesScheduledMangaDownloadRecord()
    {
        CrawlerAgent crawlerAgent = InsertCrawlerAgent(_dbContext, CreateCrawlerAgent("refresh-agent.dll"));
        Library library = InsertLibrary(_dbContext, CreateLibrary(crawlerAgent, CreateManga("refresh-manga", "Refresh Manga")));

        _ = _workerService
            .Setup(service => service.ScheduleMangaDownload(It.IsAny<MangaDownloadRecord>(), null))
            .Returns("refresh-job");

        Library result = await _service.RefreshCollectionAsync(library, CancellationToken.None);

        Assert.Equal(library.Id, result.Id);

        using LibraryDbContext libraryDbContext = new(library.Id);
        MangaDownloadRecord record = libraryDbContext.MangaDownloadRecords.FindOne(x => x.Library.Id == library.Id);

        Assert.NotNull(record);
        Assert.Equal("refresh-job", record.BackgroundJobId);
        Assert.Equal(DownloadStatus.Scheduled, record.DownloadStatus);

        _workerService.Verify(service => service.ScheduleMangaDownload(It.IsAny<MangaDownloadRecord>(), null), Times.Once);
    }

    [Fact]
    public async Task UpgradeCrawlerAgentAsync_WhenCrawlerAgentIdIsEmpty_ReturnsLibraryWithoutScheduling()
    {
        CrawlerAgent crawlerAgent = InsertCrawlerAgent(_dbContext, CreateCrawlerAgent("empty-upgrade-agent.dll"));
        Library library = InsertLibrary(_dbContext, CreateLibrary(crawlerAgent, CreateManga("empty-upgrade-manga", "Empty Upgrade Manga")));

        Library result = await _service.UpgradeCrawlerAgentAsync(library.Id, Guid.Empty, CancellationToken.None);

        Assert.Equal(library.Id, result.Id);
        Assert.Equal(crawlerAgent.Id, result.CrawlerAgent.Id);
        _workerService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpgradeCrawlerAgentAsync_WhenMangaDownloadExists_UpdatesRecordsAndReschedulesJobs()
    {
        CrawlerAgent oldCrawlerAgent = InsertCrawlerAgent(_dbContext, CreateCrawlerAgent("agent-upgrade.dll", id: Guid.NewGuid()));
        CrawlerAgent newCrawlerAgent = InsertCrawlerAgent(_dbContext, CreateCrawlerAgent("agent-upgrade.dll", id: Guid.NewGuid(), displayName: "Updated Agent"));
        Library library = InsertLibrary(_dbContext, CreateLibrary(oldCrawlerAgent, CreateManga("upgrade-manga", "Upgrade Manga"), id: Guid.NewGuid()));

        MangaDownloadRecord mangaDownloadRecord = WithId(new MangaDownloadRecord(library, "old-manga-job"), Guid.NewGuid());
        mangaDownloadRecord.Schedule("old-manga-job");

        Chapter pendingChapter = CreateChapter(library.Manga!, "pending-chapter", 1, "Pending Chapter");
        Chapter completedChapter = CreateChapter(library.Manga!, "completed-chapter", 2, "Completed Chapter");

        ChapterDownloadRecord pendingRecord = WithId(new ChapterDownloadRecord(oldCrawlerAgent, mangaDownloadRecord, pendingChapter), Guid.NewGuid());
        pendingRecord.Scheduled("pending-job");

        ChapterDownloadRecord completedRecord = WithId(new ChapterDownloadRecord(oldCrawlerAgent, mangaDownloadRecord, completedChapter), Guid.NewGuid());
        completedRecord.Scheduled("completed-job");
        completedRecord.Complete();

        using (LibraryDbContext libraryDbContext = new(library.Id))
        {
            _ = libraryDbContext.MangaDownloadRecords.Insert(mangaDownloadRecord);
            _ = libraryDbContext.ChapterDownloadRecords.Insert(pendingRecord);
            _ = libraryDbContext.ChapterDownloadRecords.Insert(completedRecord);
        }

        TimeSpan expectedSchedule = TimeSpan.FromHours(7);
        _ = _workerService.Setup(service => service.GetDiscoverySchedule(It.IsAny<Library>())).Returns(expectedSchedule);
        _ = _workerService.Setup(service => service.ScheduleMangaDownload(It.IsAny<MangaDownloadRecord>(), expectedSchedule)).Returns("new-manga-job");

        Library result = await _service.UpgradeCrawlerAgentAsync(library.Id, newCrawlerAgent.Id, CancellationToken.None);

        Assert.Equal(newCrawlerAgent.Id, result.CrawlerAgent.Id);

        Library storedLibrary = _dbContext.Libraries.FindById(library.Id);
        Assert.Equal(newCrawlerAgent.Id, storedLibrary.CrawlerAgent.Id);

        using LibraryDbContext verificationDb = new(library.Id);
        MangaDownloadRecord updatedMangaDownload = verificationDb.MangaDownloadRecords.FindById(mangaDownloadRecord.Id);
        ChapterDownloadRecord updatedPending = verificationDb.ChapterDownloadRecords.FindById(pendingRecord.Id);
        ChapterDownloadRecord updatedCompleted = verificationDb.ChapterDownloadRecords.FindById(completedRecord.Id);

        Assert.Equal("new-manga-job", updatedMangaDownload.BackgroundJobId);
        Assert.Equal(DownloadStatus.Scheduled, updatedMangaDownload.DownloadStatus);
        Assert.Equal(I18n.CrawlerAgentHasBeenUpgraded, updatedMangaDownload.StatusReason);
        Assert.Equal(newCrawlerAgent.Id, updatedMangaDownload.Library.CrawlerAgent.Id);

        Assert.Equal(DownloadStatus.ToBeRescheduled, updatedPending.DownloadStatus);
        Assert.Equal(I18n.CrawlerAgentHasBeenUpgraded, updatedPending.StatusReason);
        Assert.Equal(newCrawlerAgent.Id, updatedPending.CrawlerAgent.Id);

        Assert.Equal(DownloadStatus.Completed, updatedCompleted.DownloadStatus);
        Assert.Equal(newCrawlerAgent.Id, updatedCompleted.CrawlerAgent.Id);

        _workerService.Verify(service => service.CancelMangaDownload(It.Is<MangaDownloadRecord>(record => record.Id == mangaDownloadRecord.Id)), Times.Once);
        _workerService.Verify(service => service.ScheduleMangaDownload(It.Is<MangaDownloadRecord>(record => record.Id == mangaDownloadRecord.Id), expectedSchedule), Times.Once);
        _workerService.Verify(service => service.CancelChapterDownload(It.Is<ChapterDownloadRecord>(record => record.Id == pendingRecord.Id)), Times.Once);
        _workerService.Verify(service => service.CancelChapterDownload(It.Is<ChapterDownloadRecord>(record => record.Id == completedRecord.Id)), Times.Never);
    }

    [Fact]
    public async Task UpgradeCrawlerAgentAsync_WhenMangaDownloadDoesNotExist_RefreshesCollection()
    {
        CrawlerAgent oldCrawlerAgent = InsertCrawlerAgent(_dbContext, CreateCrawlerAgent("agent-refresh-upgrade.dll", id: Guid.NewGuid()));
        CrawlerAgent newCrawlerAgent = InsertCrawlerAgent(_dbContext, CreateCrawlerAgent("agent-refresh-upgrade.dll", id: Guid.NewGuid(), displayName: "New Version"));
        Library library = InsertLibrary(_dbContext, CreateLibrary(oldCrawlerAgent, CreateManga("refresh-upgrade-manga", "Refresh Upgrade Manga"), id: Guid.NewGuid()));

        _ = _workerService.Setup(service => service.GetDiscoverySchedule(It.IsAny<Library>())).Returns(TimeSpan.FromHours(10));
        _ = _workerService.Setup(service => service.ScheduleMangaDownload(It.IsAny<MangaDownloadRecord>(), null)).Returns("refresh-upgrade-job");

        Library result = await _service.UpgradeCrawlerAgentAsync(library.Id, newCrawlerAgent.Id, CancellationToken.None);

        Assert.Equal(newCrawlerAgent.Id, result.CrawlerAgent.Id);

        using LibraryDbContext libraryDbContext = new(library.Id);
        MangaDownloadRecord mangaDownloadRecord = libraryDbContext.MangaDownloadRecords.FindOne(x => x.Library.Id == library.Id);

        Assert.NotNull(mangaDownloadRecord);
        Assert.Equal("refresh-upgrade-job", mangaDownloadRecord.BackgroundJobId);
        Assert.Equal(DownloadStatus.Scheduled, mangaDownloadRecord.DownloadStatus);

        _workerService.Verify(service => service.CancelMangaDownload(It.IsAny<MangaDownloadRecord>()), Times.Never);
        _workerService.Verify(service => service.ScheduleMangaDownload(It.IsAny<MangaDownloadRecord>(), null), Times.Once);
    }

    [Fact]
    public async Task ConsolidateCollectionByCrawlerAgentAsync_RefreshesMatchingLibrariesAndSendsNotifications()
    {
        CrawlerAgent crawlerAgent = InsertCrawlerAgent(_dbContext, CreateCrawlerAgent("consolidate-agent.dll", id: Guid.NewGuid()));
        CrawlerAgent otherCrawlerAgent = InsertCrawlerAgent(_dbContext, CreateCrawlerAgent("other-consolidate-agent.dll", id: Guid.NewGuid()));

        Library firstMatch = InsertLibrary(_dbContext, CreateLibrary(crawlerAgent, CreateManga($"manga-{Guid.NewGuid():N}", "First Match"), id: Guid.NewGuid()));
        Library secondMatch = InsertLibrary(_dbContext, CreateLibrary(crawlerAgent, CreateManga($"manga-{Guid.NewGuid():N}", "Second Match"), id: Guid.NewGuid()));
        Library nonMatch = InsertLibrary(_dbContext, CreateLibrary(otherCrawlerAgent, CreateManga($"manga-{Guid.NewGuid():N}", "Non Match"), id: Guid.NewGuid()));

        List<string> infoMessages = [];
        List<string> successMessages = [];

        _ = _notificationService
            .Setup(service => service.PushInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((message, _) => infoMessages.Add(message))
            .Returns(Task.CompletedTask);
        _ = _notificationService
            .Setup(service => service.PushSuccessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((message, _) => successMessages.Add(message))
            .Returns(Task.CompletedTask);

        _ = _workerService.Setup(service => service.ScheduleMangaDownload(It.IsAny<MangaDownloadRecord>(), null)).Returns("consolidated-job");

        List<Library> result = [.. await _service.ConsolidateCollectionByCrawlerAgentAsync(crawlerAgent, CancellationToken.None)];

        Assert.Equal(2, result.Count);
        Assert.Contains(result, library => library.Id == firstMatch.Id);
        Assert.Contains(result, library => library.Id == secondMatch.Id);

        using LibraryDbContext firstLibraryDbContext = new(firstMatch.Id);
        using LibraryDbContext secondLibraryDbContext = new(secondMatch.Id);
        using LibraryDbContext thirdLibraryDbContext = new(nonMatch.Id);

        Assert.NotNull(firstLibraryDbContext.MangaDownloadRecords.FindOne(x => x.Library.Id == firstMatch.Id));
        Assert.NotNull(secondLibraryDbContext.MangaDownloadRecords.FindOne(x => x.Library.Id == secondMatch.Id));
        Assert.Null(thirdLibraryDbContext.MangaDownloadRecords.FindOne(x => x.Library.Id == nonMatch.Id));

        Assert.Equal(2, infoMessages.Count);
        Assert.Equal(2, successMessages.Count);
        Assert.Contains(infoMessages, message => message.Contains("First Match", StringComparison.Ordinal));
        Assert.Contains(infoMessages, message => message.Contains("Second Match", StringComparison.Ordinal));
        Assert.Contains(successMessages, message => message.Contains("First Match", StringComparison.Ordinal));
        Assert.Contains(successMessages, message => message.Contains("Second Match", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConsolidateCollectionByAssemblyNameAsync_UpgradesMatchingLibrariesToTargetCrawlerAgent()
    {
        CrawlerAgent oldCrawlerAgent = InsertCrawlerAgent(_dbContext, CreateCrawlerAgent("same-assembly-agent.dll", id: Guid.NewGuid(), displayName: "Old Version"));
        CrawlerAgent newCrawlerAgent = InsertCrawlerAgent(_dbContext, CreateCrawlerAgent("same-assembly-agent.dll", id: Guid.NewGuid(), displayName: "New Version"));
        CrawlerAgent unrelatedCrawlerAgent = InsertCrawlerAgent(_dbContext, CreateCrawlerAgent("different-assembly-agent.dll", id: Guid.NewGuid()));

        Library firstMatch = InsertLibrary(_dbContext, CreateLibrary(oldCrawlerAgent, CreateManga($"assembly-match-{Guid.NewGuid():N}", "Assembly Match 1"), id: Guid.NewGuid()));
        Library secondMatch = InsertLibrary(_dbContext, CreateLibrary(oldCrawlerAgent, CreateManga($"assembly-match-{Guid.NewGuid():N}", "Assembly Match 2"), id: Guid.NewGuid()));
        Library nonMatch = InsertLibrary(_dbContext, CreateLibrary(unrelatedCrawlerAgent, CreateManga($"assembly-other-{Guid.NewGuid():N}", "Assembly Other"), id: Guid.NewGuid()));

        _ = _workerService.Setup(service => service.GetDiscoverySchedule(It.IsAny<Library>())).Returns(TimeSpan.FromHours(3));
        _ = _workerService.Setup(service => service.ScheduleMangaDownload(It.IsAny<MangaDownloadRecord>(), null)).Returns("assembly-upgrade-job");

        List<Library> result = [.. await _service.ConsolidateCollectionByAssemblyNameAsync(newCrawlerAgent, CancellationToken.None)];

        Assert.Equal(2, result.Count);

        Library storedFirst = _dbContext.Libraries.FindById(firstMatch.Id);
        Library storedSecond = _dbContext.Libraries.FindById(secondMatch.Id);
        Library storedNonMatch = _dbContext.Libraries.FindById(nonMatch.Id);

        Assert.Equal(newCrawlerAgent.Id, storedFirst.CrawlerAgent.Id);
        Assert.Equal(newCrawlerAgent.Id, storedSecond.CrawlerAgent.Id);
        Assert.Equal(unrelatedCrawlerAgent.Id, storedNonMatch.CrawlerAgent.Id);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private static CrawlerAgent CreateCrawlerAgent(string assemblyName, Guid? id = null, string? displayName = null)
    {
        return WithId(
            new CrawlerAgent(Path.Combine("C:\\agents", assemblyName), displayName ?? Path.GetFileNameWithoutExtension(assemblyName), []),
            id ?? Guid.NewGuid());
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

    private static T WithId<T>(T entity, string id)
    {
        PropertyInfo property = typeof(T).GetProperty(nameof(Manga.Id))!;
        _ = property.GetSetMethod(true)!.Invoke(entity, [id]);
        return entity;
    }

    private static Library CreateLibrary(CrawlerAgent crawlerAgent, Manga manga, Guid? id = null)
    {
        return WithId(new Library(crawlerAgent, manga, "{manga_title}\\{manga_title} ch.{chapter_padded_4}", "{manga_title} ch.{chapter_padded_4}", "{manga_title}"), id ?? Guid.NewGuid());
    }

    private static T WithId<T>(T entity, Guid id)
    {
        PropertyInfo property = typeof(T).GetProperty(nameof(Library.Id))!;
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

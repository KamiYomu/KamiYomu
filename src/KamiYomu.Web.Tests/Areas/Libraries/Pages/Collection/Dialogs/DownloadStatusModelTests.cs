using Hangfire;
using Hangfire.Storage.SQLite;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Areas.Libraries.Pages.Collection.Dialogs;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Resources;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Tests.Areas.Libraries.Pages.Collection.Dialogs;

public class DownloadStatusModelTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "KamiYomu.Tests.DownloadStatusModel", Guid.NewGuid().ToString("N"));
    private readonly DbContext _dbContext = new(":memory:");
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<IWorkerService> _workerService = new();
    private readonly IOptions<WorkerOptions> _workerOptions = Options.Create(new WorkerOptions
    {
        ServerAvailableNames = ["server-1"],
        DownloadChapterQueues = ["download"],
        MangaDownloadSchedulerQueues = ["manga"],
        DiscoveryNewChapterQueues = ["discovery"],
        DailyExecutionTime = TimeSpan.FromHours(2)
    });
    private readonly string _storagePath;

    public DownloadStatusModelTests()
    {
        _ = Directory.CreateDirectory(_rootPath);
        LibraryDbContext.DatabaseFilePathResolver = libraryId => Path.Combine(_rootPath, $"lib{libraryId}.db");

        _ = Directory.CreateDirectory(ServiceTestHelpers.ArtifactsRoot);
        _storagePath = Path.Combine(ServiceTestHelpers.ArtifactsRoot, $"hangfire-{Guid.NewGuid():N}.sqlite");
        JobStorage.Current = new SQLiteStorage(_storagePath);
    }

    public void Dispose()
    {
        LibraryDbContext.DatabaseFilePathResolver = ServiceTestHelpers.DefaultLibraryDbContextResolver;
        _dbContext.Dispose();

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

        foreach (string path in new[] { _storagePath, $"{_storagePath}-shm", $"{_storagePath}-wal" })
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }

    private DownloadStatusModel CreateModel()
    {
        return new DownloadStatusModel(_workerOptions, _dbContext, _notificationService.Object, _workerService.Object)
        {
            FollowButtonViewModel = new FollowButtonViewModel(),
            ScanNowButtonViewModel = new ScanNowButtonViewModel(),
            Library = null!
        };
    }

    [Fact]
    public void OnGet_WhenNoMangaDownloadRecordExists_LeavesRecordNull()
    {
        Library library = ServiceTestHelpers.CreateLibrary("Alpha");
        _ = _dbContext.Libraries.Insert(library);

        DownloadStatusModel model = CreateModel();

        model.OnGet(library.Id);

        Assert.Null(model.Record);
        Assert.Equal(_workerOptions.Value.DailyExecutionTime, model.FollowButtonViewModel.DailyExecutionSchedule);
        Assert.False(model.FollowButtonViewModel.IsFollowing);
    }

    [Fact]
    public void OnGet_WhenMangaDownloadRecordExists_PopulatesRecordAndButtonStates()
    {
        StoredLibraryRecord stored = ServiceTestHelpers.CreateCompletedStoredChapter(_dbContext);
        _ = _workerService.Setup(w => w.IsDiscoverRecurringJobRunning(It.IsAny<Library>())).Returns(true);
        _ = _workerService.Setup(w => w.IsDiscoverRecurringJobScheduled(It.IsAny<Library>())).Returns(true);

        DownloadStatusModel model = CreateModel();

        model.OnGet(stored.Library.Id);

        Assert.NotNull(model.Record);
        Assert.Equal(stored.MangaDownload.Id, model.Record.Id);
        Assert.True(model.ScanNowButtonViewModel.IsScanning);
        Assert.True(model.FollowButtonViewModel.IsFollowing);
    }

    [Fact]
    public async Task OnPostToggleFollowingAsync_WhenCurrentlyFollowing_UnfollowsAndPushesSuccess()
    {
        Library library = ServiceTestHelpers.CreateLibrary("Alpha");
        _ = _dbContext.Libraries.Insert(library);

        DownloadStatusModel model = CreateModel();
        model.FollowButtonViewModel.LibraryId = library.Id;
        model.FollowButtonViewModel.IsFollowing = true;

        IActionResult result = await model.OnPostToggleFollowingAsync(CancellationToken.None);

        ViewComponentResult viewComponentResult = Assert.IsType<ViewComponentResult>(result);
        Assert.Equal("FollowButton", viewComponentResult.ViewComponentName);
        Assert.False(model.FollowButtonViewModel.IsFollowing);
        _notificationService.Verify(n => n.PushSuccessAsync(I18n.YouAreNoLongerFollowingThisTitle, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnPostToggleFollowingAsync_WhenNotFollowing_SchedulesJobAndPushesSuccess()
    {
        Library library = ServiceTestHelpers.CreateLibrary("Beta");
        _ = _dbContext.Libraries.Insert(library);

        DownloadStatusModel model = CreateModel();
        model.FollowButtonViewModel.LibraryId = library.Id;
        model.FollowButtonViewModel.IsFollowing = false;
        model.FollowButtonViewModel.DailyExecutionSchedule = TimeSpan.FromHours(5);

        IActionResult result = await model.OnPostToggleFollowingAsync(CancellationToken.None);

        _ = Assert.IsType<ViewComponentResult>(result);
        Assert.True(model.FollowButtonViewModel.IsFollowing);
        _workerService.Verify(w => w.ScheduleDiscoverRecurringJob(It.IsAny<Library>(), TimeSpan.FromHours(5)), Times.Once);
        _notificationService.Verify(n => n.PushSuccessAsync(I18n.YouStartedFollowingThisTitle, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OnPostStartDiscoverChaptersJob_WhenJobTriggered_SetsScanningAndPushesNotification()
    {
        Library library = ServiceTestHelpers.CreateLibrary("Gamma");
        _ = _dbContext.Libraries.Insert(library);
        _ = _workerService.Setup(w => w.TriggerDiscoverRecurringJob(It.IsAny<Library>())).Returns("job-123");

        DownloadStatusModel model = CreateModel();
        model.ScanNowButtonViewModel.LibraryId = library.Id;

        IActionResult result = model.OnPostStartDiscoverChaptersJob();

        ViewComponentResult viewComponentResult = Assert.IsType<ViewComponentResult>(result);
        Assert.Equal("ScanNowButton", viewComponentResult.ViewComponentName);
        Assert.True(model.ScanNowButtonViewModel.IsScanning);
        _notificationService.Verify(n => n.PushSuccessAsync(I18n.StartSearchingForChapters, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OnPostStartDiscoverChaptersJob_WhenJobIdIsEmpty_DoesNotPushNotification()
    {
        Library library = ServiceTestHelpers.CreateLibrary("Delta");
        _ = _dbContext.Libraries.Insert(library);
        _ = _workerService.Setup(w => w.TriggerDiscoverRecurringJob(It.IsAny<Library>())).Returns(string.Empty);

        DownloadStatusModel model = CreateModel();
        model.ScanNowButtonViewModel.LibraryId = library.Id;

        IActionResult result = model.OnPostStartDiscoverChaptersJob();

        _ = Assert.IsType<ViewComponentResult>(result);
        Assert.False(model.ScanNowButtonViewModel.IsScanning);
        _notificationService.Verify(n => n.PushSuccessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

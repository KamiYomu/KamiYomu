using Hangfire;
using Hangfire.Storage.SQLite;

using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Areas.Settings.Pages.CrawlerAgents.Dialogs;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Resources;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Tests.Areas.Settings.Pages.CrawlerAgents.Dialogs;

public class DeleteConfirmModelTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "KamiYomu.Tests.DeleteConfirmModel", Guid.NewGuid().ToString("N"));
    private readonly DbContext _dbContext = new(":memory:");
    private readonly Mock<IBackgroundJobClient> _jobClient = new();
    private readonly Mock<IWorkerService> _workerService = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly string _storagePath;

    public DeleteConfirmModelTests()
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

    private DeleteConfirmModel CreateModel()
    {
        return new DeleteConfirmModel(_dbContext, _jobClient.Object, _workerService.Object, _notificationService.Object)
        {
            PageContext = ServiceTestHelpers.CreatePageContext()
        };
    }

    [Fact]
    public void OnGet_WithExistingAgent_PopulatesAgentAndReturnsPage()
    {
        CrawlerAgent agent = new("Agent.dll", "Agent", []);
        _ = _dbContext.CrawlerAgents.Insert(agent);

        DeleteConfirmModel model = CreateModel();

        IActionResult result = model.OnGet(agent.Id);

        _ = Assert.IsType<PageResult>(result);
        Assert.NotNull(model.Agent);
        Assert.Equal(agent.Id, model.Agent.Id);
    }

    [Fact]
    public void OnGet_WithUnknownAgent_ReturnsNotFound()
    {
        DeleteConfirmModel model = CreateModel();

        IActionResult result = model.OnGet(Guid.NewGuid());

        _ = Assert.IsType<NotFoundResult>(result);
        Assert.Null(model.Agent);
    }

    [Fact]
    public void OnPostAsync_WithNoLibrariesUsingAgent_DeletesAgentAndReturnsPartial()
    {
        CrawlerAgent agent = new("Agent.dll", "Agent", []);
        _ = _dbContext.CrawlerAgents.Insert(agent);

        DeleteConfirmModel model = CreateModel();
        model.Id = agent.Id;

        IActionResult result = model.OnPostAsync(CancellationToken.None);

        PartialViewResult partialViewResult = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_CrawlerAgentList", partialViewResult.ViewName);
        List<CrawlerAgent> resultAgents = Assert.IsType<List<CrawlerAgent>>(partialViewResult.Model);
        Assert.Empty(resultAgents);

        Assert.Null(_dbContext.CrawlerAgents.FindById(agent.Id));
        _workerService.Verify(w => w.CancelJobsForCrawlerAgent(It.Is<CrawlerAgent>(a => a.Id == agent.Id)), Times.Once);
        _notificationService.Verify(n => n.PushSuccessAsync(I18n.CrawlerAgentRemovedSuccessfully, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OnPostAsync_WithLibraryHavingDownloadRecords_CleansUpJobsAndRecords()
    {
        CrawlerAgent agent = new("Agent.dll", "Agent", []);
        _ = _dbContext.CrawlerAgents.Insert(agent);

        Library library = ServiceTestHelpers.CreateLibrary(agent, "Alpha");
        _ = _dbContext.Libraries.Insert(library);

        MangaDownloadRecord mangaDownload = new(library, "manga-job-1");
        ServiceTestHelpers.AssignId(mangaDownload);

        Chapter chapter = ServiceTestHelpers.CreateChapter();
        ChapterDownloadRecord chapterDownload = new(agent, mangaDownload, chapter);
        ServiceTestHelpers.AssignId(chapterDownload);

        using (LibraryDbContext libraryDbContext = library.GetReadWriteDbContext())
        {
            _ = libraryDbContext.MangaDownloadRecords.Insert(mangaDownload);
            _ = libraryDbContext.ChapterDownloadRecords.Insert(chapterDownload);
        }

        DeleteConfirmModel model = CreateModel();
        model.Id = agent.Id;

        IActionResult result = model.OnPostAsync(CancellationToken.None);

        _ = Assert.IsType<PartialViewResult>(result);

        // IBackgroundJobClient.Delete(jobId) is a Hangfire extension method (not an interface
        // member), so it can't be verified directly via Moq; verify the underlying ChangeState
        // call it delegates to instead.
        _jobClient.Verify(
            j => j.ChangeState("manga-job-1", It.IsAny<Hangfire.States.DeletedState>(), null),
            Times.Once);

        using LibraryDbContext verifyContext = new(library.Id);
        Assert.Empty(verifyContext.MangaDownloadRecords.FindAll());
        Assert.Empty(verifyContext.ChapterDownloadRecords.FindAll());

        Assert.Null(_dbContext.CrawlerAgents.FindById(agent.Id));
        _workerService.Verify(w => w.CancelJobsForCrawlerAgent(It.Is<CrawlerAgent>(a => a.Id == agent.Id)), Times.Once);
    }
}

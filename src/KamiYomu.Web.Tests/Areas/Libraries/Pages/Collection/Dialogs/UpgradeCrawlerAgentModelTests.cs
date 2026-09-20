using KamiYomu.Web.Areas.Libraries.Pages.Collection.Dialogs;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.AppServices.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Resources;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Tests.Areas.Libraries.Pages.Collection.Dialogs;

public class UpgradeCrawlerAgentModelTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<ICrawlerAgentAppService> _crawlerAgentAppService = new();

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private UpgradeCrawlerAgentModel CreateModel()
    {
        return new UpgradeCrawlerAgentModel(_dbContext, _notificationService.Object, _crawlerAgentAppService.Object)
        {
            RefreshElementId = string.Empty,
            Library = null!
        };
    }

    [Fact]
    public void OnGet_PopulatesLibraryAndAvailableVersionsForSameAssembly()
    {
        Library library = ServiceTestHelpers.CreateLibrary("Alpha");
        _ = _dbContext.Libraries.Insert(library);
        _ = _dbContext.CrawlerAgents.Insert(library.CrawlerAgent);

        CrawlerAgent otherVersion = new(library.CrawlerAgent.AssemblyName, "Newer Version", []);
        typeof(CrawlerAgent).GetProperty(nameof(CrawlerAgent.Id))!.SetValue(otherVersion, Guid.NewGuid());
        _ = _dbContext.CrawlerAgents.Insert(otherVersion);

        UpgradeCrawlerAgentModel model = CreateModel();

        model.OnGet(library.Id, "refresh-1");

        Assert.Equal(library.Id, model.LibraryId);
        Assert.Equal("refresh-1", model.RefreshElementId);
        Assert.Equal(library.Id, model.Library.Id);
        Assert.Equal(2, model.AvailableVersions.Count());
    }

    [Fact]
    public async Task OnPostUpgradeCrawlerAgentAsync_WhenUpgradeSucceeds_PushesSuccessNotification()
    {
        Library library = ServiceTestHelpers.CreateLibrary("Alpha");
        Guid newCrawlerAgentId = Guid.NewGuid();
        typeof(CrawlerAgent).GetProperty(nameof(CrawlerAgent.Id))!.SetValue(library.CrawlerAgent, newCrawlerAgentId);
        _ = _crawlerAgentAppService
            .Setup(s => s.UpgradeCrawlerAgentAsync(library.Id, newCrawlerAgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(library);

        UpgradeCrawlerAgentModel model = CreateModel();

        IActionResult result = await model.OnPostUpgradeCrawlerAgentAsync(library.Id, newCrawlerAgentId, CancellationToken.None);

        ViewComponentResult viewComponentResult = Assert.IsType<ViewComponentResult>(result);
        Assert.Equal("LibraryCard", viewComponentResult.ViewComponentName);
        _notificationService.Verify(
            n => n.PushSuccessAsync(I18n.CrawlerAgentHasBeenUpgraded, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnPostUpgradeCrawlerAgentAsync_WhenUpgradeReturnsNull_DoesNotPushNotification()
    {
        Guid libraryId = Guid.NewGuid();
        Guid crawlerAgentId = Guid.NewGuid();
        _ = _crawlerAgentAppService
            .Setup(s => s.UpgradeCrawlerAgentAsync(libraryId, crawlerAgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Library)null!);

        UpgradeCrawlerAgentModel model = CreateModel();

        IActionResult result = await model.OnPostUpgradeCrawlerAgentAsync(libraryId, crawlerAgentId, CancellationToken.None);

        _ = Assert.IsType<ViewComponentResult>(result);
        _notificationService.Verify(
            n => n.PushSuccessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

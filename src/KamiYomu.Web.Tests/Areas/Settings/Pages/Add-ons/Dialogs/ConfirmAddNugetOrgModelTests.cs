using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Areas.Settings.Models;
using KamiYomu.Web.Areas.Settings.Pages.Add_ons.Dialogs;
using KamiYomu.Web.Areas.Settings.ViewComponents;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Tests.Areas.Settings.Pages.Add_ons.Dialogs;

public class ConfirmAddNugetOrgModelTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");
    private readonly Mock<INotificationService> _notificationService = new();

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private ConfirmAddNugetOrgModel CreateModel()
    {
        return new ConfirmAddNugetOrgModel(_dbContext, _notificationService.Object);
    }

    [Fact]
    public void OnGet_DoesNothing()
    {
        ConfirmAddNugetOrgModel model = CreateModel();

        model.OnGet();

        Assert.Empty(_dbContext.NugetSources.FindAll());
    }

    [Fact]
    public void OnPostAsync_InsertsNugetOrgSourceAndPushesNotification()
    {
        ConfirmAddNugetOrgModel model = CreateModel();

        IActionResult result = model.OnPostAsync(CancellationToken.None);

        List<NugetSource> sources = [.. _dbContext.NugetSources.FindAll()];
        NugetSource inserted = Assert.Single(sources);
        Assert.Equal("NuGet.org", inserted.DisplayName);
        Assert.Equal(new Uri(Defaults.NugetFeeds.NugetFeedUrl), inserted.Url);

        _notificationService.Verify(
            n => n.PushSuccessAsync("Source added successfully", It.IsAny<CancellationToken>()),
            Times.Once);

        ViewComponentResult viewComponentResult = Assert.IsType<ViewComponentResult>(result);
        Assert.Equal("SearchBar", viewComponentResult.ViewComponentName);
        SearchBarViewModel viewModel = Assert.IsType<SearchBarViewModel>(viewComponentResult.Arguments);
        Assert.Equal(inserted.Id, viewModel.SourceId);
        Assert.False(viewModel.IncludePrerelease);
        _ = Assert.Single(viewModel.Sources);
        Assert.Equal(inserted.Id, viewModel.Sources.Single().Id);
    }
}

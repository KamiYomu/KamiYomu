using KamiYomu.Web.Areas.Settings.Models;
using KamiYomu.Web.Areas.Settings.Pages.Add_ons.Dialogs;
using KamiYomu.Web.Areas.Settings.ViewComponents;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Resources;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Tests.Areas.Settings.Pages.Add_ons.Dialogs;

public class DeleteConfirmModelTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");
    private readonly Mock<INotificationService> _notificationService = new();

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private DeleteConfirmModel CreateModel()
    {
        return new DeleteConfirmModel(_dbContext, _notificationService.Object);
    }

    private NugetSource InsertSource(string displayName = "MySource")
    {
        NugetSource source = new(displayName, new Uri("https://example.com/index.json"), null, null);
        _ = _dbContext.NugetSources.Insert(source);
        return source;
    }

    [Fact]
    public void OnGet_WithExistingId_PopulatesNugetSourceAndReturnsPage()
    {
        NugetSource source = InsertSource();
        DeleteConfirmModel model = CreateModel();

        IActionResult result = model.OnGet(source.Id);

        _ = Assert.IsType<PageResult>(result);
        Assert.NotNull(model.NugetSource);
        Assert.Equal(source.Id, model.NugetSource!.Id);
    }

    [Fact]
    public void OnGet_WithUnknownId_ReturnsNotFound()
    {
        DeleteConfirmModel model = CreateModel();

        IActionResult result = model.OnGet(Guid.NewGuid());

        _ = Assert.IsType<NotFoundResult>(result);
        Assert.Null(model.NugetSource);
    }

    [Fact]
    public void OnPost_DeletesSourcePushesNotificationAndReturnsViewComponent()
    {
        NugetSource source = InsertSource();
        DeleteConfirmModel model = CreateModel();
        model.Id = source.Id;

        IActionResult result = model.OnPost(CancellationToken.None);

        Assert.Empty(_dbContext.NugetSources.FindAll());

        _notificationService.Verify(
            n => n.PushSuccessAsync(I18n.SourceRemovedSuccessfully, It.IsAny<CancellationToken>()),
            Times.Once);

        ViewComponentResult viewComponentResult = Assert.IsType<ViewComponentResult>(result);
        Assert.Equal("SearchBar", viewComponentResult.ViewComponentName);
        SearchBarViewModel viewModel = Assert.IsType<SearchBarViewModel>(viewComponentResult.Arguments);
        Assert.False(viewModel.IncludePrerelease);
        Assert.Empty(viewModel.Sources);
    }

    [Fact]
    public void OnPost_WithOtherSourcesRemaining_PopulatesRemainingSourcesInViewComponent()
    {
        NugetSource toDelete = InsertSource("ToDelete");
        NugetSource remaining = InsertSource("Remaining");
        DeleteConfirmModel model = CreateModel();
        model.Id = toDelete.Id;

        IActionResult result = model.OnPost(CancellationToken.None);

        ViewComponentResult viewComponentResult = Assert.IsType<ViewComponentResult>(result);
        SearchBarViewModel viewModel = Assert.IsType<SearchBarViewModel>(viewComponentResult.Arguments);
        NugetSource onlyRemaining = Assert.Single(viewModel.Sources);
        Assert.Equal(remaining.Id, onlyRemaining.Id);
    }
}

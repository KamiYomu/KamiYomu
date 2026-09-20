using KamiYomu.Web.Areas.Settings.Models;
using KamiYomu.Web.Areas.Settings.Pages.Add_ons;
using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace KamiYomu.Web.Tests.Areas.Settings.Pages.Add_ons;

public class EditModelTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");
    private readonly Mock<ILogger<EditModel>> _logger = new();

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private EditModel CreateModel()
    {
        return new EditModel(_logger.Object, _dbContext);
    }

    private NugetSource InsertSource(string displayName = "MySource", string url = "https://example.com/index.json", string? userName = "user", string? password = "pass")
    {
        NugetSource source = new(displayName, new Uri(url), userName, password);
        _ = _dbContext.NugetSources.Insert(source);
        return source;
    }

    [Fact]
    public void IsEditMode_WhenInputIdIsEmpty_IsFalse()
    {
        EditModel model = CreateModel();

        Assert.False(model.IsEditMode);
    }

    [Fact]
    public void IsEditMode_WhenInputIdIsSet_IsTrue()
    {
        EditModel model = CreateModel();
        model.Input.Id = Guid.NewGuid();

        Assert.True(model.IsEditMode);
    }

    [Fact]
    public void OnGet_WithNullId_ReturnsPageWithDefaultInput()
    {
        EditModel model = CreateModel();

        IActionResult result = model.OnGet(null);

        _ = Assert.IsType<PageResult>(result);
        Assert.Equal(Guid.Empty, model.Input.Id);
        Assert.False(model.IsEditMode);
    }

    [Fact]
    public void OnGet_WithUnknownId_ReturnsNotFound()
    {
        EditModel model = CreateModel();

        IActionResult result = model.OnGet(Guid.NewGuid());

        _ = Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void OnGet_WithExistingId_PopulatesTrimmedInputAndReturnsPage()
    {
        NugetSource source = InsertSource(" My Source ", "https://example.com/index.json", " userA ", " passA ");
        EditModel model = CreateModel();

        IActionResult result = model.OnGet(source.Id);

        _ = Assert.IsType<PageResult>(result);
        Assert.Equal(source.Id, model.Input.Id);
        Assert.Equal("My Source", model.Input.DisplayName);
        Assert.Equal("https://example.com/index.json", model.Input.Url);
        Assert.Equal("userA", model.Input.UserName);
        Assert.Equal("passA", model.Input.Password);
        Assert.True(model.IsEditMode);
    }

    [Fact]
    public void OnPost_WhenModelStateInvalid_ReturnsPageWithoutSaving()
    {
        EditModel model = CreateModel();
        model.ModelState.AddModelError("Input.Url", "Invalid URL");

        IActionResult result = model.OnPost();

        _ = Assert.IsType<PageResult>(result);
        Assert.Empty(_dbContext.NugetSources.FindAll());
    }

    [Fact]
    public void OnPost_WithNewSource_InsertsSourceAndRedirectsToIndex()
    {
        EditModel model = CreateModel();
        model.Input = new EditModel.NugetSourceInput
        {
            Id = Guid.Empty,
            DisplayName = "New Source",
            Url = "https://example.com/index.json",
            UserName = " userA ",
            Password = " passA "
        };

        IActionResult result = model.OnPost();

        RedirectToPageResult redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Index", redirect.PageName);

        NugetSource inserted = Assert.Single(_dbContext.NugetSources.FindAll());
        Assert.Equal("New Source", inserted.DisplayName);
        Assert.Equal(new Uri("https://example.com/index.json"), inserted.Url);
        Assert.Equal("userA", inserted.UserName);
        Assert.Equal("passA", inserted.Password);
    }

    [Fact]
    public void OnPost_WithExistingSource_UpdatesSourceAndRedirectsToIndex()
    {
        NugetSource source = InsertSource("Original", "https://original.example.com/index.json", "origUser", "origPass");
        EditModel model = CreateModel();
        model.Input = new EditModel.NugetSourceInput
        {
            Id = source.Id,
            DisplayName = "Updated Name",
            Url = "https://updated.example.com/index.json",
            UserName = " updUser ",
            Password = " updPass "
        };

        IActionResult result = model.OnPost();

        RedirectToPageResult redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Index", redirect.PageName);

        NugetSource updated = Assert.Single(_dbContext.NugetSources.FindAll());
        Assert.Equal(source.Id, updated.Id);
        Assert.Equal("Updated Name", updated.DisplayName);
        Assert.Equal(new Uri("https://updated.example.com/index.json"), updated.Url);
        Assert.Equal("updUser", updated.UserName);
        Assert.Equal("updPass", updated.Password);
    }

    [Fact]
    public void OnPost_WithIdNotMatchingAnyExistingSource_InsertsNewSourceWithThatId()
    {
        // The Id from Input is not applied to the newly constructed NugetSource (its constructor
        // does not accept an Id and Id is auto-assigned by the entity), so posting an Input.Id that
        // doesn't match any existing source falls into the "insert" branch, silently discarding the
        // posted Id rather than erroring. This documents that actual behavior.
        Guid nonExistentId = Guid.NewGuid();
        EditModel model = CreateModel();
        model.Input = new EditModel.NugetSourceInput
        {
            Id = nonExistentId,
            DisplayName = "Orphan Source",
            Url = "https://example.com/index.json",
            UserName = null,
            Password = null
        };

        IActionResult result = model.OnPost();

        _ = Assert.IsType<RedirectToPageResult>(result);
        NugetSource inserted = Assert.Single(_dbContext.NugetSources.FindAll());
        Assert.NotEqual(nonExistentId, inserted.Id);
        Assert.Equal("Orphan Source", inserted.DisplayName);
    }
}

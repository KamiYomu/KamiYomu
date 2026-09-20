using KamiYomu.Web.Areas.Settings.Pages.Integrations;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Integrations;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Resources;
using KamiYomu.Web.Tests.Infrastructure.Services;

using LiteDB;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace KamiYomu.Web.Tests.Areas.Settings.Pages.Integrations;

public class GotifyIntegrationModelTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");
    private readonly Mock<ILogger<IndexModel>> _logger = new();
    private readonly Mock<IGotifyService> _gotifyService = new();

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private GotifyIntegrationModel CreateModel(GotifyIntegrationInput? input = null)
    {
        return new GotifyIntegrationModel(_logger.Object, _dbContext, _gotifyService.Object)
        {
            Input = input ?? new GotifyIntegrationInput
            {
                ServiceUri = new Uri("https://gotify.example.com"),
                ApiKey = "api-key",
                Enabled = true
            },
            PageContext = ServiceTestHelpers.CreatePageContext()
        };
    }

    [Fact]
    public void OnGet_DoesNotThrow()
    {
        GotifyIntegrationModel model = CreateModel();

        model.OnGet();
    }

    [Fact]
    public async Task OnPostTestGotifyConnectionAsync_WhenModelStateInvalid_ReturnsPartialWithInput()
    {
        GotifyIntegrationModel model = CreateModel();
        model.ModelState.AddModelError("ServiceUri", "required");

        IActionResult result = await model.OnPostTestGotifyConnectionAsync(CancellationToken.None);

        PartialViewResult partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_GotifyIntegration", partial.ViewName);
        Assert.Same(model.Input, partial.Model);
        _gotifyService.Verify(s => s.TestConnection(It.IsAny<GotifySettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnPostTestGotifyConnectionAsync_WhenConnectionSucceeds_SetsSuccessMessageAndReturnsPartial()
    {
        _ = _gotifyService.Setup(s => s.TestConnection(It.IsAny<GotifySettings>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        GotifyIntegrationModel model = CreateModel();

        IActionResult result = await model.OnPostTestGotifyConnectionAsync(CancellationToken.None);

        PartialViewResult partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal(I18n.ConnectionSuccessfully, model.Input.SucessMessage);
        Assert.True(model.ModelState.IsValid);
    }

    [Fact]
    public async Task OnPostTestGotifyConnectionAsync_WhenConnectionFails_AddsModelErrorAndReturnsPartial()
    {
        _ = _gotifyService.Setup(s => s.TestConnection(It.IsAny<GotifySettings>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        GotifyIntegrationModel model = CreateModel();

        IActionResult result = await model.OnPostTestGotifyConnectionAsync(CancellationToken.None);

        _ = Assert.IsType<PartialViewResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.Contains(model.ModelState[""]!.Errors, e => e.ErrorMessage == I18n.TheConnectionHasFailed);
    }

    [Fact]
    public async Task OnPostTestGotifyConnectionAsync_WhenServiceThrows_LogsAndReturnsEmptyResult()
    {
        // The try block's normal paths (success/failure) both `return Partial(...)` before reaching
        // the end of the method. Only the catch block falls through past `return Partial` to the
        // trailing `return new EmptyResult()` - so an exception yields EmptyResult, not a partial.
        _ = _gotifyService
            .Setup(s => s.TestConnection(It.IsAny<GotifySettings>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        GotifyIntegrationModel model = CreateModel();

        IActionResult result = await model.OnPostTestGotifyConnectionAsync(CancellationToken.None);

        _ = Assert.IsType<EmptyResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.Contains(model.ModelState[""]!.Errors, e => e.ErrorMessage == I18n.SomethingWentWrong);
    }

    [Fact]
    public async Task OnPostSaveGotifyAsync_WhenDisabled_RemovesApiKeyModelStateBeforeValidating()
    {
        GotifyIntegrationModel model = CreateModel(new GotifyIntegrationInput
        {
            ServiceUri = new Uri("https://gotify.example.com"),
            ApiKey = null,
            Enabled = false
        });
        model.ModelState.AddModelError(nameof(GotifyIntegrationInput.ApiKey), "required");

        IActionResult result = await model.OnPostSaveGotifyAsync(CancellationToken.None);

        _ = Assert.IsType<PartialViewResult>(result);
        // ApiKey error was stripped by RemoveWithSuffix, so save proceeds and succeeds.
        Assert.Equal(I18n.SettingsSavedSuccessfully, model.Input.SucessMessage);
    }

    [Fact]
    public async Task OnPostSaveGotifyAsync_WhenModelStateInvalidAfterRemoval_ReturnsPartialWithoutSaving()
    {
        GotifyIntegrationModel model = CreateModel();
        model.ModelState.AddModelError(nameof(GotifyIntegrationInput.ServiceUri), "required");

        IActionResult result = await model.OnPostSaveGotifyAsync(CancellationToken.None);

        _ = Assert.IsType<PartialViewResult>(result);
        Assert.Empty(_dbContext.UserPreferences.FindAll());
    }

    [Fact]
    public async Task OnPostSaveGotifyAsync_WhenEnabledTrue_ActuallyDisablesSettings_LikelyInversionBug()
    {
        // BUG (documented, not fixed - out of scope for testability-only refactor):
        // GotifyIntegrationModel.OnPostSaveGotifyAsync has its Enable/Disable calls inverted
        // (lines ~94-101): when Input.Enabled is TRUE it calls settings.Disable(), and when
        // Input.Enabled is FALSE it calls settings.Enable(). This is the opposite of
        // KavitaIntegrationModel.OnPostSaveKavitaAsync, which correctly calls Enable() when
        // Input.Enabled is true. This test asserts the ACTUAL (buggy) observed behavior.
        _ = _gotifyService.Setup(s => s.TestConnection(It.IsAny<GotifySettings>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        GotifyIntegrationModel model = CreateModel(new GotifyIntegrationInput
        {
            ServiceUri = new Uri("https://gotify.example.com"),
            ApiKey = "new-key",
            Enabled = true
        });

        _ = await model.OnPostSaveGotifyAsync(CancellationToken.None);

        UserPreference saved = _dbContext.UserPreferences.FindAll().Single();
        Assert.NotNull(saved.GotifySettings);
        Assert.False(saved.GotifySettings!.Enabled);
    }

    [Fact]
    public async Task OnPostSaveGotifyAsync_WhenEnabledFalse_ActuallyEnablesSettings_LikelyInversionBug()
    {
        // Mirror of the inversion above: Input.Enabled == false results in settings.Enable() being
        // called, so the persisted settings end up Enabled == true.
        GotifyIntegrationModel model = CreateModel(new GotifyIntegrationInput
        {
            ServiceUri = new Uri("https://gotify.example.com"),
            ApiKey = null,
            Enabled = false
        });

        _ = await model.OnPostSaveGotifyAsync(CancellationToken.None);

        UserPreference saved = _dbContext.UserPreferences.FindAll().Single();
        Assert.NotNull(saved.GotifySettings);
        Assert.True(saved.GotifySettings!.Enabled);
        _gotifyService.Verify(s => s.TestConnection(It.IsAny<GotifySettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnPostSaveGotifyAsync_WithNonBlankApiKey_UpdatesApiKey()
    {
        GotifyIntegrationModel model = CreateModel(new GotifyIntegrationInput
        {
            ServiceUri = new Uri("https://gotify.example.com"),
            ApiKey = "brand-new-key",
            Enabled = false
        });

        _ = await model.OnPostSaveGotifyAsync(CancellationToken.None);

        UserPreference saved = _dbContext.UserPreferences.FindAll().Single();
        Assert.Equal("brand-new-key", saved.GotifySettings!.ApiKey);
    }

    [Fact]
    public async Task OnPostSaveGotifyAsync_WithBlankApiKey_LeavesExistingApiKeyUnchanged()
    {
        GotifySettings existingSettings = new(true, new Uri("https://old.example.com"), "old-key");
        UserPreference preference = ServiceTestHelpers.CreateUserPreferenceWithGotify(existingSettings);
        _ = _dbContext.UserPreferences.Insert(preference);

        GotifyIntegrationModel model = CreateModel(new GotifyIntegrationInput
        {
            ServiceUri = new Uri("https://old.example.com"),
            ApiKey = "   ",
            Enabled = false
        });

        _ = await model.OnPostSaveGotifyAsync(CancellationToken.None);

        UserPreference saved = _dbContext.UserPreferences.FindAll().Single();
        Assert.Equal("old-key", saved.GotifySettings!.ApiKey);
    }

    [Fact]
    public async Task OnPostSaveGotifyAsync_WhenConnectionTestFails_AddsModelErrorButStillSaves()
    {
        _ = _gotifyService.Setup(s => s.TestConnection(It.IsAny<GotifySettings>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        GotifyIntegrationModel model = CreateModel(new GotifyIntegrationInput
        {
            ServiceUri = new Uri("https://gotify.example.com"),
            ApiKey = "api-key",
            Enabled = true
        });

        IActionResult result = await model.OnPostSaveGotifyAsync(CancellationToken.None);

        _ = Assert.IsType<PartialViewResult>(result);
        Assert.False(model.ModelState.IsValid);
        // No early return on failed connection test, so the settings are persisted regardless.
        _ = Assert.Single(_dbContext.UserPreferences.FindAll());
    }

    [Fact]
    public async Task OnPostSaveGotifyAsync_WhenServiceThrows_LogsAddsModelErrorAndReturnsPartial()
    {
        _ = _gotifyService
            .Setup(s => s.TestConnection(It.IsAny<GotifySettings>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        GotifyIntegrationModel model = CreateModel(new GotifyIntegrationInput
        {
            ServiceUri = new Uri("https://gotify.example.com"),
            ApiKey = "api-key",
            Enabled = true
        });

        IActionResult result = await model.OnPostSaveGotifyAsync(CancellationToken.None);

        // Unlike OnPostTestGotifyConnectionAsync, this method's catch is followed by an
        // unconditional `return Partial(...)`, so it always returns the partial view.
        PartialViewResult partial = Assert.IsType<PartialViewResult>(result);
        Assert.Same(model.Input, partial.Model);
        Assert.False(model.ModelState.IsValid);
        Assert.Contains(model.ModelState[""]!.Errors, e => e.ErrorMessage == I18n.SomethingWentWrong);
    }

    [Fact]
    public void OnPostDeleteGotify_WhenNoPreferences_ReturnsPartialWithRemovedMessage()
    {
        GotifyIntegrationModel model = CreateModel();

        IActionResult result = model.OnPostDeleteGotify();

        PartialViewResult partial = Assert.IsType<PartialViewResult>(result);
        GotifyIntegrationInput resultInput = Assert.IsType<GotifyIntegrationInput>(partial.Model);
        Assert.Equal(I18n.SettingsRemovedSuccessfully, resultInput.SucessMessage);
    }

    [Fact]
    public void OnPostDeleteGotify_WhenPreferencesHaveNoGotifySettings_ReturnsPartialWithRemovedMessage()
    {
        UserPreference preference = new(System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        _ = _dbContext.UserPreferences.Insert(preference);
        GotifyIntegrationModel model = CreateModel();

        IActionResult result = model.OnPostDeleteGotify();

        PartialViewResult partial = Assert.IsType<PartialViewResult>(result);
        GotifyIntegrationInput resultInput = Assert.IsType<GotifyIntegrationInput>(partial.Model);
        Assert.Equal(I18n.SettingsRemovedSuccessfully, resultInput.SucessMessage);
    }

    [Fact]
    public void OnPostDeleteGotify_WhenGotifySettingsExist_ClearsSettingsAndReturnsPartial()
    {
        GotifySettings existingSettings = new(true, new Uri("https://old.example.com"), "old-key");
        UserPreference preference = ServiceTestHelpers.CreateUserPreferenceWithGotify(existingSettings);
        _ = _dbContext.UserPreferences.Insert(preference);
        GotifyIntegrationModel model = CreateModel();

        IActionResult result = model.OnPostDeleteGotify();

        _ = Assert.IsType<PartialViewResult>(result);
        UserPreference saved = _dbContext.UserPreferences.FindAll().Single();
        Assert.Null(saved.GotifySettings);
    }
}

using KamiYomu.Web.Areas.Settings.Pages.Integrations;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Integrations;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Resources;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace KamiYomu.Web.Tests.Areas.Settings.Pages.Integrations;

public class KavitaIntegrationModelTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");
    private readonly Mock<ILogger<IndexModel>> _logger = new();
    private readonly Mock<IKavitaService> _kavitaService = new();

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private KavitaIntegrationModel CreateModel(KavitaIntegrationInput? input = null)
    {
        return new KavitaIntegrationModel(_logger.Object, _dbContext, _kavitaService.Object)
        {
            Input = input ?? new KavitaIntegrationInput
            {
                ServiceUri = new Uri("https://kavita.example.com"),
                Username = "user",
                Password = "pass",
                ApiKey = "api-key",
                Enabled = true
            },
            PageContext = ServiceTestHelpers.CreatePageContext()
        };
    }

    [Fact]
    public void OnGet_DoesNotThrow()
    {
        KavitaIntegrationModel model = CreateModel();

        model.OnGet();
    }

    [Fact]
    public async Task OnPostTestKavitaConnectionAsync_WhenModelStateInvalid_ReturnsPartialWithInput()
    {
        KavitaIntegrationModel model = CreateModel();
        model.ModelState.AddModelError("ServiceUri", "required");

        IActionResult result = await model.OnPostTestKavitaConnectionAsync(CancellationToken.None);

        PartialViewResult partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_KavitaIntegration", partial.ViewName);
        Assert.Same(model.Input, partial.Model);
        _kavitaService.Verify(s => s.TestConnection(It.IsAny<KavitaSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnPostTestKavitaConnectionAsync_WhenConnectionSucceeds_SetsSuccessMessageAndReturnsPartial()
    {
        _ = _kavitaService.Setup(s => s.TestConnection(It.IsAny<KavitaSettings>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        KavitaIntegrationModel model = CreateModel();

        IActionResult result = await model.OnPostTestKavitaConnectionAsync(CancellationToken.None);

        _ = Assert.IsType<PartialViewResult>(result);
        Assert.Equal(I18n.ConnectionSuccessfully, model.Input.SucessMessage);
        Assert.True(model.ModelState.IsValid);
    }

    [Fact]
    public async Task OnPostTestKavitaConnectionAsync_WhenConnectionFails_AddsModelErrorAndReturnsPartial()
    {
        _ = _kavitaService.Setup(s => s.TestConnection(It.IsAny<KavitaSettings>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        KavitaIntegrationModel model = CreateModel();

        IActionResult result = await model.OnPostTestKavitaConnectionAsync(CancellationToken.None);

        _ = Assert.IsType<PartialViewResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.Contains(model.ModelState[""]!.Errors, e => e.ErrorMessage == I18n.TheConnectionHasFailed);
    }

    [Fact]
    public async Task OnPostTestKavitaConnectionAsync_WhenServiceThrows_LogsAndStillReturnsPartial()
    {
        // Unlike Gotify's OnPostTestGotifyConnectionAsync, here the `return Partial(...)` is
        // outside/after the try-catch, so both the happy path and the exception path fall
        // through to the same return - this always returns the partial view, never EmptyResult.
        _ = _kavitaService
            .Setup(s => s.TestConnection(It.IsAny<KavitaSettings>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        KavitaIntegrationModel model = CreateModel();

        IActionResult result = await model.OnPostTestKavitaConnectionAsync(CancellationToken.None);

        PartialViewResult partial = Assert.IsType<PartialViewResult>(result);
        Assert.Same(model.Input, partial.Model);
        Assert.False(model.ModelState.IsValid);
        Assert.Contains(model.ModelState[""]!.Errors, e => e.ErrorMessage == I18n.SomethingWentWrong);
    }

    [Fact]
    public async Task OnPostSaveKavitaAsync_WhenModelStateInvalid_ReturnsPartialWithoutSaving()
    {
        KavitaIntegrationModel model = CreateModel();
        model.ModelState.AddModelError(nameof(KavitaIntegrationInput.Username), "required");

        IActionResult result = await model.OnPostSaveKavitaAsync(CancellationToken.None);

        _ = Assert.IsType<PartialViewResult>(result);
        Assert.Empty(_dbContext.UserPreferences.FindAll());
    }

    [Fact]
    public async Task OnPostSaveKavitaAsync_WhenEnabledTrue_EnablesSettings_CorrectBehavior()
    {
        // Confirms KavitaIntegrationModel does NOT have the Enable/Disable inversion present in
        // GotifyIntegrationModel.OnPostSaveGotifyAsync: Input.Enabled == true correctly results in
        // settings.Enable() being called (Enabled stays true after save).
        _ = _kavitaService.Setup(s => s.TestConnection(It.IsAny<KavitaSettings>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        KavitaIntegrationModel model = CreateModel(new KavitaIntegrationInput
        {
            ServiceUri = new Uri("https://kavita.example.com"),
            Username = "user",
            Password = "pass",
            ApiKey = "api-key",
            Enabled = true
        });

        _ = await model.OnPostSaveKavitaAsync(CancellationToken.None);

        UserPreference saved = _dbContext.UserPreferences.FindAll().Single();
        Assert.NotNull(saved.KavitaSettings);
        Assert.True(saved.KavitaSettings!.Enabled);
    }

    [Fact]
    public async Task OnPostSaveKavitaAsync_WhenEnabledFalse_DisablesSettings_CorrectBehavior()
    {
        KavitaIntegrationModel model = CreateModel(new KavitaIntegrationInput
        {
            ServiceUri = new Uri("https://kavita.example.com"),
            Username = "user",
            Password = null,
            ApiKey = null,
            Enabled = false
        });

        _ = await model.OnPostSaveKavitaAsync(CancellationToken.None);

        UserPreference saved = _dbContext.UserPreferences.FindAll().Single();
        Assert.NotNull(saved.KavitaSettings);
        Assert.False(saved.KavitaSettings!.Enabled);
        _kavitaService.Verify(s => s.TestConnection(It.IsAny<KavitaSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnPostSaveKavitaAsync_WithNonBlankPasswordAndApiKey_UpdatesBoth()
    {
        KavitaIntegrationModel model = CreateModel(new KavitaIntegrationInput
        {
            ServiceUri = new Uri("https://kavita.example.com"),
            Username = "user",
            Password = "new-pass",
            ApiKey = "new-key",
            Enabled = false
        });

        _ = await model.OnPostSaveKavitaAsync(CancellationToken.None);

        UserPreference saved = _dbContext.UserPreferences.FindAll().Single();
        Assert.Equal("new-pass", saved.KavitaSettings!.Password);
        Assert.Equal("new-key", saved.KavitaSettings!.ApiKey);
    }

    [Fact]
    public async Task OnPostSaveKavitaAsync_WithBlankPasswordAndApiKey_LeavesExistingValuesUnchanged()
    {
        KavitaSettings existingSettings = new(new Uri("https://old.example.com"), "olduser", "old-pass", "old-key", true);
        UserPreference preference = ServiceTestHelpers.CreateUserPreferenceWithKavita(existingSettings);
        _ = _dbContext.UserPreferences.Insert(preference);

        _ = _kavitaService.Setup(s => s.TestConnection(It.IsAny<KavitaSettings>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        KavitaIntegrationModel model = CreateModel(new KavitaIntegrationInput
        {
            ServiceUri = new Uri("https://old.example.com"),
            Username = "olduser",
            Password = "   ",
            ApiKey = string.Empty,
            Enabled = true
        });

        _ = await model.OnPostSaveKavitaAsync(CancellationToken.None);

        UserPreference saved = _dbContext.UserPreferences.FindAll().Single();
        Assert.Equal("old-pass", saved.KavitaSettings!.Password);
        Assert.Equal("old-key", saved.KavitaSettings!.ApiKey);
    }

    [Fact]
    public async Task OnPostSaveKavitaAsync_WhenConnectionTestFails_AddsModelErrorButStillSaves()
    {
        _ = _kavitaService.Setup(s => s.TestConnection(It.IsAny<KavitaSettings>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        KavitaIntegrationModel model = CreateModel(new KavitaIntegrationInput
        {
            ServiceUri = new Uri("https://kavita.example.com"),
            Username = "user",
            Password = "pass",
            ApiKey = "api-key",
            Enabled = true
        });

        IActionResult result = await model.OnPostSaveKavitaAsync(CancellationToken.None);

        _ = Assert.IsType<PartialViewResult>(result);
        Assert.False(model.ModelState.IsValid);
        _ = Assert.Single(_dbContext.UserPreferences.FindAll());
    }

    [Fact]
    public async Task OnPostSaveKavitaAsync_WhenServiceThrows_LogsAddsModelErrorAndReturnsPartial()
    {
        _ = _kavitaService
            .Setup(s => s.TestConnection(It.IsAny<KavitaSettings>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        KavitaIntegrationModel model = CreateModel(new KavitaIntegrationInput
        {
            ServiceUri = new Uri("https://kavita.example.com"),
            Username = "user",
            Password = "pass",
            ApiKey = "api-key",
            Enabled = true
        });

        IActionResult result = await model.OnPostSaveKavitaAsync(CancellationToken.None);

        PartialViewResult partial = Assert.IsType<PartialViewResult>(result);
        Assert.Same(model.Input, partial.Model);
        Assert.False(model.ModelState.IsValid);
        Assert.Contains(model.ModelState[""]!.Errors, e => e.ErrorMessage == I18n.SomethingWentWrong);
    }

    [Fact]
    public void OnPostDeleteKavita_WhenNoPreferences_ReturnsPartialWithFreshInput()
    {
        KavitaIntegrationModel model = CreateModel();

        IActionResult result = model.OnPostDeleteKavita();

        PartialViewResult partial = Assert.IsType<PartialViewResult>(result);
        KavitaIntegrationInput resultInput = Assert.IsType<KavitaIntegrationInput>(partial.Model);
        Assert.NotEqual(I18n.SettingsRemovedSuccessfully, resultInput.SucessMessage);
    }

    [Fact]
    public void OnPostDeleteKavita_WhenPreferencesHaveNoKavitaSettings_ReturnsPartialWithFreshInput()
    {
        UserPreference preference = new(System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        _ = _dbContext.UserPreferences.Insert(preference);
        KavitaIntegrationModel model = CreateModel();

        IActionResult result = model.OnPostDeleteKavita();

        PartialViewResult partial = Assert.IsType<PartialViewResult>(result);
        _ = Assert.IsType<KavitaIntegrationInput>(partial.Model);
    }

    [Fact]
    public void OnPostDeleteKavita_WhenKavitaSettingsExist_ClearsSettingsAndReturnsPartial()
    {
        KavitaSettings existingSettings = new(new Uri("https://old.example.com"), "olduser", "old-pass", "old-key", true);
        UserPreference preference = ServiceTestHelpers.CreateUserPreferenceWithKavita(existingSettings);
        _ = _dbContext.UserPreferences.Insert(preference);
        KavitaIntegrationModel model = CreateModel();

        IActionResult result = model.OnPostDeleteKavita();

        PartialViewResult partial = Assert.IsType<PartialViewResult>(result);
        KavitaIntegrationInput resultInput = Assert.IsType<KavitaIntegrationInput>(partial.Model);
        Assert.Equal(I18n.SettingsRemovedSuccessfully, resultInput.SucessMessage);

        UserPreference saved = _dbContext.UserPreferences.FindAll().Single();
        Assert.Null(saved.KavitaSettings);
    }
}

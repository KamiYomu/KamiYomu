using System.Globalization;

using KamiYomu.Web.Areas.Settings.Pages.Integrations;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Integrations;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.Extensions.Logging;

namespace KamiYomu.Web.Tests.Areas.Settings.Pages.Integrations;

public class IndexModelTests : IDisposable
{
    private const string MaskedValue = "***";

    private readonly DbContext _dbContext = new(":memory:");
    private readonly Mock<ILogger<IndexModel>> _logger = new();
    private readonly Mock<INotificationService> _notificationService = new();

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private IndexModel CreateModel()
    {
        return new IndexModel(_logger.Object, _dbContext, _notificationService.Object);
    }

    [Fact]
    public void OnGet_WithNoPreferences_LeavesBothInputsNull()
    {
        IndexModel model = CreateModel();

        model.OnGet();

        Assert.Null(model.KavitaIntegrationInput);
        Assert.Null(model.GotifyIntegrationInput);
    }

    [Fact]
    public void OnGet_WithKavitaSettingsOnly_PopulatesMaskedKavitaInput()
    {
        KavitaSettings kavitaSettings = new(new Uri("https://kavita.example.com"), "user", "pass", "key", true);
        UserPreference preference = ServiceTestHelpers.CreateUserPreferenceWithKavita(kavitaSettings);
        _ = _dbContext.UserPreferences.Insert(preference);

        IndexModel model = CreateModel();

        model.OnGet();

        Assert.NotNull(model.KavitaIntegrationInput);
        Assert.True(model.KavitaIntegrationInput.Enabled);
        Assert.Equal("user", model.KavitaIntegrationInput.Username);
        Assert.Equal(new Uri("https://kavita.example.com"), model.KavitaIntegrationInput.ServiceUri);
        Assert.Equal(MaskedValue, model.KavitaIntegrationInput.Password);
        Assert.Equal(MaskedValue, model.KavitaIntegrationInput.ApiKey);
        Assert.Null(model.GotifyIntegrationInput);
    }

    [Fact]
    public void OnGet_WithGotifySettingsOnly_PopulatesMaskedGotifyInput()
    {
        GotifySettings gotifySettings = new(true, new Uri("https://gotify.example.com"), "key");
        UserPreference preference = ServiceTestHelpers.CreateUserPreferenceWithGotify(gotifySettings);
        _ = _dbContext.UserPreferences.Insert(preference);

        IndexModel model = CreateModel();

        model.OnGet();

        Assert.NotNull(model.GotifyIntegrationInput);
        Assert.True(model.GotifyIntegrationInput.Enabled);
        Assert.Equal(new Uri("https://gotify.example.com"), model.GotifyIntegrationInput.ServiceUri);
        Assert.Equal(MaskedValue, model.GotifyIntegrationInput.ApiKey);
        Assert.Null(model.KavitaIntegrationInput);
    }

    [Fact]
    public void OnGet_WithBothSettings_PopulatesBothInputs()
    {
        UserPreference preference = new(CultureInfo.GetCultureInfo("en-US"));
        preference.SetKavitaSettings(new KavitaSettings(new Uri("https://kavita.example.com"), "user", "pass", "key", false));
        preference.SetGotifySettings(new GotifySettings(false, new Uri("https://gotify.example.com"), "key"));
        _ = _dbContext.UserPreferences.Insert(preference);

        IndexModel model = CreateModel();

        model.OnGet();

        Assert.NotNull(model.KavitaIntegrationInput);
        Assert.NotNull(model.GotifyIntegrationInput);
        Assert.False(model.KavitaIntegrationInput.Enabled);
        Assert.False(model.GotifyIntegrationInput.Enabled);
    }
}

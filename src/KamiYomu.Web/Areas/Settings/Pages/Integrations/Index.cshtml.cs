using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Settings.Pages.Integrations;

public class IndexModel(
    ILogger<IndexModel> logger,
    DbContext dbContext,
    INotificationService notificationService) : PageModel
{
    public KavitaIntegrationInput KavitaIntegrationInput { get; set; }
    public GotifyIntegrationInput GotifyIntegrationInput { get; set; }

    private const string PasswordEmptyValue = "***";
    public void OnGet()
    {
        UserPreference? preferences = dbContext.UserPreferences
                                               .Include(p => p.KavitaSettings)
                                               .Include(p => p.GotifySettings)
                                               .Query().FirstOrDefault();

        if (preferences?.KavitaSettings != null)
        {
            KavitaIntegrationInput = new KavitaIntegrationInput
            {
                Enabled = preferences.KavitaSettings.Enabled,
                Username = preferences.KavitaSettings.Username,
                ServiceUri = preferences.KavitaSettings.ServiceUri,
                Password = PasswordEmptyValue,
                ApiKey = PasswordEmptyValue
            };
        }

        if (preferences?.GotifySettings != null)
        {
            GotifyIntegrationInput = new GotifyIntegrationInput
            {
                Enabled = preferences.GotifySettings.Enabled,
                ServiceUri = preferences.GotifySettings.ServiceUri,
                ApiKey = PasswordEmptyValue
            };
        }
    }
}

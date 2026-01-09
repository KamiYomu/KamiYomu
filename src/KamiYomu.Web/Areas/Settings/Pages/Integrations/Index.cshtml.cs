using System.ComponentModel.DataAnnotations;
using System.Globalization;

using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Validators;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Settings.Pages.Integrations;

public class IndexModel(DbContext dbContext, IKavitaService kavitaService) : PageModel
{
    [BindProperty]
    public KavitaIntegrationInput KavitaIntegrationInput { get; set; }

    private const string PasswordEmptyValue = "***";
    public void OnGet()
    {
        UserPreference? preferences = dbContext.UserPreferences.Query().FirstOrDefault();

        if (preferences?.KavitaSettings != null)
        {
            KavitaIntegrationInput = new KavitaIntegrationInput
            {
                Enabled = preferences.KavitaSettings.Enabled,
                Username = preferences.KavitaSettings.Username,
                ServiceUri = preferences.KavitaSettings.ServiceUri,
                Password = PasswordEmptyValue
            };
        }
    }

    public async Task<IActionResult> OnPostTestConnectionAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Partial("_MessageError", I18n.PleaseFixValidationErrors);
        }

        try
        {
            bool success = await kavitaService.TryConnectToKavita(new KavitaSettings(
                KavitaIntegrationInput.ServiceUri,
                KavitaIntegrationInput.Username,
                KavitaIntegrationInput.Password,
                KavitaIntegrationInput.Enabled), cancellationToken);

            return success
                ? Partial("_MessageSuccess", I18n.ConnectionSuccessfully)
                : Partial("_MessageError", I18n.FailedConnectKavita);
        }
        catch (Exception ex)
        {
            return Partial("_MessageError", ex.Message);
        }
    }

    public IActionResult OnPostSave()
    {
        if (!ModelState.IsValid)
        {
            return Partial("_MessageError", I18n.PleaseFixValidationErrors);
        }

        try
        {
            // Save preferences to DB
            UserPreference preferences = dbContext.UserPreferences.Query().FirstOrDefault();
            preferences ??= new UserPreference(CultureInfo.CurrentCulture);

            KavitaSettings kavitaSettings = preferences.KavitaSettings ?? new KavitaSettings(
                                                        KavitaIntegrationInput.ServiceUri,
                                                        KavitaIntegrationInput.Username,
                                                        KavitaIntegrationInput.Password,
                                                        KavitaIntegrationInput.Enabled);

            if (KavitaIntegrationInput.Password != PasswordEmptyValue)
            {
                kavitaSettings.UpdatePassword(KavitaIntegrationInput.Password);

                preferences.SetKavitaSettings(kavitaSettings);

                _ = dbContext.UserPreferences.Upsert(preferences);
            }

            return Partial("_MessageSuccess", I18n.SettingsSavedSuccessfully);
        }
        catch (Exception ex)
        {
            return Partial("_MessageError", ex.Message);
        }
    }

}

public class KavitaIntegrationInput
{
    [Required(ErrorMessageResourceType = typeof(I18n), ErrorMessageResourceName = nameof(I18n.ServiceUriRequired))]
    [UriValidator(ErrorMessageResourceType = typeof(I18n), ErrorMessageResourceName = nameof(I18n.ServiceUriInvalid))]
    public required Uri ServiceUri { get; set; }

    [Required(ErrorMessageResourceType = typeof(I18n), ErrorMessageResourceName = nameof(I18n.UsernameRequired))]
    public required string Username { get; set; }

    [Required(ErrorMessageResourceType = typeof(I18n), ErrorMessageResourceName = nameof(I18n.PasswordRequired))]
    public required string Password { get; set; }

    public bool Enabled { get; set; } = true;
}

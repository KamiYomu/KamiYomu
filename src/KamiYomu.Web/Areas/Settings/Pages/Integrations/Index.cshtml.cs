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
                Password = PasswordEmptyValue,
                ApiKey = PasswordEmptyValue
            };
        }
    }

    public async Task<IActionResult> OnPostTestKavitaConnectionAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Partial("_MessageError", I18n.PleaseFixValidationErrors);
        }

        try
        {
            KavitaSettings settings = new(
                KavitaIntegrationInput.ServiceUri,
                KavitaIntegrationInput.Username,
                KavitaIntegrationInput.Password,
                KavitaIntegrationInput.ApiKey,
                KavitaIntegrationInput.Enabled);

            bool success = await kavitaService.TryConnectToKavita(settings, cancellationToken);

            return success
                ? Partial("_MessageSuccess", I18n.ConnectionSuccessfully)
                : Partial("_MessageError", I18n.FailedConnectKavita);
        }
        catch (Exception ex)
        {
            return Partial("_MessageError", ex.Message);
        }
    }

    public async Task<IActionResult> OnPostSaveKavitaAsync(CancellationToken cancellationToken)
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

            KavitaSettings settings = new(
                KavitaIntegrationInput.ServiceUri,
                KavitaIntegrationInput.Username,
                KavitaIntegrationInput.Password,
                KavitaIntegrationInput.ApiKey,
                KavitaIntegrationInput.Enabled);

            bool success = await kavitaService.TryConnectToKavita(settings, cancellationToken);


            if (!success)
            {
                return Partial("_MessageError", I18n.FailedConnectKavita);
            }

            if (KavitaIntegrationInput.Password != PasswordEmptyValue)
            {
                settings.UpdatePassword(KavitaIntegrationInput.Password);
            }

            if (KavitaIntegrationInput.ApiKey != PasswordEmptyValue)
            {
                settings.UpdateApiKey(KavitaIntegrationInput.ApiKey);
            }

            preferences.SetKavitaSettings(settings);

            _ = dbContext.UserPreferences.Upsert(preferences);

            return Partial("_MessageSuccess", I18n.SettingsSavedSuccessfully);
        }
        catch (Exception ex)
        {
            return Partial("_MessageError", ex.Message);
        }
    }


    public IActionResult OnPostDeleteKavita()
    {
        try
        {
            // Save preferences to DB
            UserPreference preferences = dbContext.UserPreferences.Query().FirstOrDefault();

            if (preferences?.KavitaSettings == null)
            {
                return Partial("_MessageSuccess", I18n.SettingsRemovedSuccessfully);
            }

            preferences.SetKavitaSettings(null!);

            _ = dbContext.UserPreferences.Update(preferences);

            return Partial("_MessageSuccess", I18n.SettingsRemovedSuccessfully);
        }
        catch (Exception ex)
        {
            return Partial("_MessageError", ex.Message);
        }
    }

}

[RequireFields(nameof(ApiKey), nameof(Password), ErrorMessageResourceType = typeof(I18n), ErrorMessageResourceName = nameof(I18n.ApiKeyOrPasswordIsRequired))]
public class KavitaIntegrationInput
{
    [Required(ErrorMessageResourceType = typeof(I18n), ErrorMessageResourceName = nameof(I18n.ServiceUriRequired))]
    [UriValidator(ErrorMessageResourceType = typeof(I18n), ErrorMessageResourceName = nameof(I18n.ServiceUriInvalid))]
    [Display(ResourceType = typeof(I18n), Name = nameof(I18n.ServiceUri))]
    public required Uri ServiceUri { get; set; }

    [Required(ErrorMessageResourceType = typeof(I18n), ErrorMessageResourceName = nameof(I18n.UsernameRequired))]
    [Display(ResourceType = typeof(I18n), Name = nameof(I18n.UserName))]
    public required string Username { get; set; }

    [Display(ResourceType = typeof(I18n), Name = nameof(I18n.Password))]
    public string? Password { get; set; }

    [Display(ResourceType = typeof(I18n), Name = nameof(I18n.APIKey))]
    public string? ApiKey { get; set; }

    [Display(ResourceType = typeof(I18n), Name = nameof(I18n.Enable))]
    public bool Enabled { get; set; } = true;
}

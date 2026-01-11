using System.ComponentModel.DataAnnotations;
using System.Globalization;

using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Integrations;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Validators;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Settings.Pages.Integrations;

public class KavitaIntegrationModel(ILogger<IndexModel> logger, DbContext dbContext, IKavitaService kavitaService) : PageModel
{
    [BindProperty]
    public KavitaIntegrationInput Input { get; set; }


    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostTestKavitaConnectionAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Partial("_KavitaIntegration", Input);
        }

        try
        {
            KavitaSettings settings = new(
                Input.ServiceUri,
                Input.Username,
                Input.Password,
                Input.ApiKey,
                Input.Enabled);

            bool success = await kavitaService.TestConnection(settings, cancellationToken);


            if (success)
            {
                Input.SucessMessage = I18n.ConnectionSuccessfully;
            }
            else
            {
                ModelState.AddModelError("", I18n.TheConnectionHasFailed);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, I18n.SomethingWentWrong);
            ModelState.AddModelError("", I18n.SomethingWentWrong);
        }
        return Partial("_KavitaIntegration", Input);
    }

    public async Task<IActionResult> OnPostSaveKavitaAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Partial("_KavitaIntegration", Input);
        }

        try
        {
            UserPreference preferences = dbContext.UserPreferences.Query().FirstOrDefault();
            preferences ??= new UserPreference(CultureInfo.CurrentCulture);

            KavitaSettings settings = preferences?.KavitaSettings ?? new(
                Input.ServiceUri,
                Input.Username,
                Input.Password,
                Input.ApiKey,
                Input.Enabled);

            if (!string.IsNullOrWhiteSpace(Input.Password))
            {
                settings.UpdatePassword(Input.Password);
            }

            if (!string.IsNullOrWhiteSpace(Input.ApiKey))
            {
                settings.UpdateApiKey(Input.ApiKey);
            }

            if (Input.Enabled)
            {
                bool success = await kavitaService.TestConnection(settings, cancellationToken);

                if (!success)
                {
                    ModelState.AddModelError("", I18n.TheConnectionHasFailed);
                }
            }

            if (Input.Enabled)
            {
                settings.Enable();
            }
            else
            {
                settings.Disable();
            }

            preferences.SetKavitaSettings(settings);

            _ = dbContext.UserPreferences.Upsert(preferences);

            Input.SucessMessage = I18n.SettingsSavedSuccessfully;

        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            ModelState.AddModelError("", I18n.SomethingWentWrong);
        }

        return Partial("_KavitaIntegration", Input);
    }

    public IActionResult OnPostDeleteKavita()
    {
        try
        {
            // Save preferences to DB
            UserPreference preferences = dbContext.UserPreferences.Include(p => p.KavitaSettings).Query().FirstOrDefault();

            if (preferences?.KavitaSettings == null)
            {
                Input = new KavitaIntegrationInput();
                return Partial("_KavitaIntegration", Input);
            }

            preferences.SetKavitaSettings(null!);

            _ = dbContext.UserPreferences.Update(preferences);

            Input = new KavitaIntegrationInput
            {
                SucessMessage = I18n.SettingsRemovedSuccessfully
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            ModelState.AddModelError("", I18n.SomethingWentWrong);
        }

        return Partial("_KavitaIntegration", Input);
    }
}


public class KavitaIntegrationInput
{
    [Required(ErrorMessageResourceType = typeof(I18n), ErrorMessageResourceName = nameof(I18n.ServiceUriRequired))]
    [UriValidator(ErrorMessageResourceType = typeof(I18n), ErrorMessageResourceName = nameof(I18n.ServiceUriInvalid))]
    [Display(ResourceType = typeof(I18n), Name = nameof(I18n.ServiceUri))]
    public Uri ServiceUri { get; set; }

    [Required(ErrorMessageResourceType = typeof(I18n), ErrorMessageResourceName = nameof(I18n.UsernameRequired))]
    [Display(ResourceType = typeof(I18n), Name = nameof(I18n.UserName))]
    public string Username { get; set; }

    [Display(ResourceType = typeof(I18n), Name = nameof(I18n.Password))]
    public string? Password { get; set; }

    [Display(ResourceType = typeof(I18n), Name = nameof(I18n.APIKey))]
    public string? ApiKey { get; set; }

    [Display(ResourceType = typeof(I18n), Name = nameof(I18n.Enable))]
    public bool Enabled { get; set; }

    public string? SucessMessage { get; set; } = string.Empty;
}



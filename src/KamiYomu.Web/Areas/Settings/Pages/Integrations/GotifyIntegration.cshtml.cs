using System.ComponentModel.DataAnnotations;
using System.Globalization;

using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Integrations;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Validators;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Settings.Pages.Integrations;

public class GotifyIntegrationModel(ILogger<IndexModel> logger, DbContext dbContext, IGotifyService gotifyService) : PageModel
{
    [BindProperty]
    public GotifyIntegrationInput Input { get; set; }

    public void OnGet()
    {
    }


    public async Task<IActionResult> OnPostTestGotifyConnectionAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Partial("_GotifyIntegration", Input);
        }

        try
        {
            GotifySettings settings = new(Input.Enabled, Input.ServiceUri, Input.ApiKey);

            bool success = await gotifyService.TestConnection(settings, cancellationToken);


            if (success)
            {
                Input.SucessMessage = I18n.ConnectionSuccessfully;
            }
            else
            {
                ModelState.AddModelError("", I18n.TheConnectionHasFailed);
            }

            return Partial("_GotifyIntegration", Input);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, I18n.SomethingWentWrong);
            ModelState.AddModelError("", I18n.SomethingWentWrong);
        }

        return new EmptyResult();
    }

    public async Task<IActionResult> OnPostSaveGotifyAsync(CancellationToken cancellationToken)
    {
        if (!Input.Enabled)
        {
            ModelState.RemoveWithSuffix(nameof(Input.ApiKey));
        }

        if (!ModelState.IsValid)
        {
            return Partial("_GotifyIntegration", Input);
        }

        try
        {
            UserPreference preferences = dbContext.UserPreferences.Include(p => p.GotifySettings).Query().FirstOrDefault();
            preferences ??= new UserPreference(CultureInfo.CurrentCulture);

            GotifySettings settings = preferences?.GotifySettings ?? new(Input.Enabled, Input.ServiceUri, Input.ApiKey);

            if (Input.Enabled)
            {
                bool success = await gotifyService.TestConnection(settings, cancellationToken);

                if (!success)
                {
                    ModelState.AddModelError("", I18n.TheConnectionHasFailed);
                }
            }

            if (!string.IsNullOrWhiteSpace(Input.ApiKey))
            {
                settings.UpdateApiKey(Input.ApiKey);
            }

            if (Input.Enabled)
            {
                settings.Disable();
            }
            else
            {
                settings.Enable();
            }

            preferences.SetGotifySettings(settings);

            _ = dbContext.UserPreferences.Upsert(preferences);

            Input.SucessMessage = I18n.SettingsSavedSuccessfully;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            ModelState.AddModelError("", I18n.SomethingWentWrong);
        }
        return Partial("_GotifyIntegration", Input);
    }

    public IActionResult OnPostDeleteGotify()
    {
        try
        {
            // Save preferences to DB
            UserPreference preferences = dbContext.UserPreferences.Include(p => p.GotifySettings).Query().FirstOrDefault();

            if (preferences?.GotifySettings == null)
            {
                Input = new GotifyIntegrationInput
                {
                    SucessMessage = I18n.SettingsRemovedSuccessfully
                };
                return Partial("_GotifyIntegration", Input);
            }

            preferences.SetGotifySettings(null!);

            _ = dbContext.UserPreferences.Update(preferences);
            Input = new GotifyIntegrationInput
            {
                SucessMessage = I18n.SettingsRemovedSuccessfully
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            ModelState.AddModelError("", I18n.SomethingWentWrong);
        }
        return Partial("_GotifyIntegration", Input);
    }
}

public class GotifyIntegrationInput
{
    [Required(ErrorMessageResourceType = typeof(I18n), ErrorMessageResourceName = nameof(I18n.ServiceUriRequired))]
    [UriValidator(ErrorMessageResourceType = typeof(I18n), ErrorMessageResourceName = nameof(I18n.ServiceUriInvalid))]
    [Display(ResourceType = typeof(I18n), Name = nameof(I18n.ServiceUri))]
    public Uri ServiceUri { get; set; }

    [Display(ResourceType = typeof(I18n), Name = nameof(I18n.APIKey))]
    public string? ApiKey { get; set; }

    [Display(ResourceType = typeof(I18n), Name = nameof(I18n.Enable))]
    public bool Enabled { get; set; }

    public string? SucessMessage { get; set; } = string.Empty;
}

using System.Globalization;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Pages;

public class IndexModel(ILogger<IndexModel> logger,
        IOptions<WorkerOptions> workerOptions,
                        DbContext dbContext,
                        INotificationService notificationService) : PageModel
{
    [BindProperty]
    public string? Culture { get; set; }

    public void OnGet()
    {
    }
    public IActionResult OnPostSetTimeZone(string tz)
    {
        if (!string.IsNullOrWhiteSpace(tz))
        {
            Response.Cookies.Append("UserTimeZone", tz, new CookieOptions
            {
                Expires = DateTime.UtcNow.AddYears(1),
                Secure = true,
                HttpOnly = false,
                IsEssential = true
            });
        }

        return new EmptyResult();
    }

    public IActionResult OnPostSetLanguage(string? returnUrl = null)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(Culture);

        UserPreference userPreference = dbContext.UserPreferences.FindOne(x => true);

        if (userPreference != null)
        {
            userPreference.SetCulture(culture);
        }
        else
        {
            userPreference = new UserPreference(culture);
        }

        _ = dbContext.UserPreferences.Upsert(userPreference);

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(Culture))
        );

        Thread.CurrentThread.CurrentCulture =
        Thread.CurrentThread.CurrentUICulture =
        CultureInfo.CurrentCulture =
        CultureInfo.CurrentUICulture = culture;

        notificationService.EnqueueSuccessForNextPage(I18n.UserInterfaceLanguageChanged);

        return Redirect(returnUrl ?? Url.Page("/Index", new { area = "" }));
    }

    public IActionResult OnPostFamilySafe(string? returnUrl = null)
    {
        UserPreference userPreference = dbContext.UserPreferences.FindOne(x => true);
        userPreference.SetFamilySafeMode(!userPreference.FamilySafeMode);
        _ = dbContext.UserPreferences.Upsert(userPreference);

        notificationService.EnqueueSuccessForNextPage(userPreference.FamilySafeMode ? I18n.FamilySafeModeEnabled : I18n.FamilySafeModeDisabled);
        return Redirect(returnUrl ?? Url.Page("/Index", new { area = "" }));
    }
}

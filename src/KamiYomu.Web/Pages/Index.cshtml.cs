using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;

namespace KamiYomu.Web.Pages
{
    public class IndexModel(ILogger<IndexModel> logger, 
                            DbContext dbContext,
                            INotificationService notificationService) : PageModel
    {
        [BindProperty]
        public string Culture { get; set; }

        public void OnGet()
        {

        }


        public IActionResult OnPostLanguageSetAsync(string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            var culture = CultureInfo.GetCultureInfo(Culture);

            var userPreference = dbContext.UserPreferences.FindOne(x => true);

            if (userPreference != null)
            {
                userPreference.SetCulture(culture);
            }
            else
            {
                userPreference = new UserPreference(culture);
            }

            dbContext.UserPreferences.Upsert(userPreference);

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(Culture))
            );

            Thread.CurrentThread.CurrentCulture =
            Thread.CurrentThread.CurrentUICulture =
            CultureInfo.CurrentCulture =
            CultureInfo.CurrentUICulture = culture;


            notificationService.EnqueueSuccess(I18n.UserInterfaceLanguageChanged);
            return Redirect(returnUrl ?? Url.Page("/Index", new { area = "" }));
        }


        public IActionResult OnPostFamilySafeAsync(string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            var userPreference = dbContext.UserPreferences.FindOne(x => true);
            userPreference.SetFamilySafeMode(!userPreference.FamilySafeMode);
            dbContext.UserPreferences.Upsert(userPreference);

            notificationService.EnqueueSuccess(userPreference.FamilySafeMode ? I18n.FamilySafeModeEnabled : I18n.FamilySafeModeDisabled);
            return Redirect(returnUrl ?? Url.Page("/Index", new { area = "" }));
        }
    }
}

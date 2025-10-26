using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;

namespace KamiYomu.Web.Pages
{
    public class IndexModel(ILogger<IndexModel> logger, DbContext dbContext) : PageModel
    {
        [BindProperty]
        public string Culture { get; set; }

        public void OnGet()
        {

        }


        public IActionResult OnPostLanguageSet()
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

            return new EmptyResult();
        }
    }
}

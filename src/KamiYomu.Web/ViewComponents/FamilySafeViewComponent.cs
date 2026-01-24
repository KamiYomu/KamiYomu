using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.ViewComponents;

public class FamilySafeViewComponent(DbContext dbContext, IOptions<StartupOptions> startupOptions) : ViewComponent
{


    public async Task<IViewComponentResult> InvokeAsync(bool hasMobileMode = false)
    {
        UserPreference userPreference = dbContext.UserPreferences.FindOne(p => true);
        bool isFamilySafe = userPreference?.FamilySafeMode ?? startupOptions.Value.FamilyMode;

        string familySafeModeText = isFamilySafe
            ? I18n.FamilySafeModeEnableMessage
            : I18n.FamilySafeModeDisableMessage;

        string buttonClass = isFamilySafe ? "btn btn-outline-success no-hover" : "btn btn-outline-secondary no-hover";
        string iconClass = isFamilySafe ? "bi-shield-check text-success" : "bi-shield-exclamation text-danger";

        return View(new FamilySafeViewComponentModel(isFamilySafe, familySafeModeText, buttonClass, iconClass, hasMobileMode));
    }
}


public record FamilySafeViewComponentModel(
    bool IsFamilySafe,
    string FamilySafeModeText,
    string ButtonClass,
    string IconClass,
    bool HasMobileMode);


using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.ViewComponents;

public class FamilySafeViewComponent(DbContext dbContext, IOptions<StartupOptions> startupOptions) : ViewComponent
{
    public IViewComponentResult Invoke(bool hasMobileMode = false)
    {
        UserPreference userPreference = dbContext.UserPreferences.Query().FirstOrDefault();
        bool isFamilySafe = userPreference?.FamilySafeMode ?? startupOptions.Value.FamilyMode;

        string legend = isFamilySafe
            ? I18n.FamilySafeModeEnableMessage
            : I18n.FamilySafeModeDisableMessage;

        string buttonClass = isFamilySafe ? "btn btn-outline-success no-hover" : "btn btn-outline-secondary no-hover";
        string iconClass = isFamilySafe ? "bi-shield-check text-success" : "bi-shield-exclamation text-danger";

        return View(new FamilySafeViewComponentModel(isFamilySafe, legend, buttonClass, iconClass, hasMobileMode));
    }
}


public record FamilySafeViewComponentModel(
    bool IsFamilySafe,
    string Legend,
    string ButtonClass,
    string IconClass,
    bool HasMobileMode);


using System.Globalization;

namespace KamiYomu.Web.Extensions;

public static class ResourceManagerExtensions
{
    public static string? GetStringSafe(this System.Resources.ResourceManager resourceManager, string name, System.Globalization.CultureInfo? culture = null)
    {
        try
        {
            culture ??= CultureInfo.CurrentCulture;

            string? result = resourceManager.GetString(name, culture);

            return string.IsNullOrWhiteSpace(result) ? name : result;
        }
        catch
        {
            return name;
        }
    }
}

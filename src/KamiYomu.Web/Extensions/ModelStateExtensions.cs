using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace KamiYomu.Web.Extensions;

public static class ModelStateExtensions
{
    public static void RemoveWithPrefix(this ModelStateDictionary modelState, string prefix)
    {
        // Remove all keys starting with the prefix
        foreach (string? key in modelState.Keys
                                   .Where(k => k.Equals(prefix, StringComparison.Ordinal)
                                            || k.StartsWith(prefix + ".", StringComparison.Ordinal))
                                   .ToList())
        {
            _ = modelState.Remove(key);
        }
    }

    public static void RemoveWithSuffix(this ModelStateDictionary modelState, string suffix)
    {
        // Remove all keys starting with the prefix
        foreach (string? key in modelState.Keys
                                   .Where(k => k.Equals(suffix, StringComparison.Ordinal)
                                            || k.EndsWith("." + suffix, StringComparison.Ordinal))
                                   .ToList())
        {
            _ = modelState.Remove(key);
        }
    }
}


using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace KamiYomu.Web.Extensions;

public static class ModelStateExtensions
{
    /// <summary>
    /// Temporarily removes all ModelState entries for a complex object
    /// so that validation for it is skipped in this request.
    /// </summary>
    /// <param name="modelState">ModelStateDictionary</param>
    /// <param name="prefix">The property name of the complex object</param>
    public static void RemoveComplex(this ModelStateDictionary modelState, string prefix)
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
}


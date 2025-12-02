using System.Text;
using System.Text.RegularExpressions;

namespace KamiYomu.Web.Infrastructure.Storage;

public static class FileNameHelper
{
    public static string SanitizeFileName(string input, string replacement = "_")
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var normalized = input.Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        var noAccents = sb.ToString().Normalize(NormalizationForm.FormC);

        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            noAccents = noAccents.Replace(c.ToString(), replacement);
        }

        noAccents = Regex.Replace(noAccents, $"{Regex.Escape(replacement)}+", replacement);

        noAccents = noAccents.Trim().Trim(replacement.ToCharArray());

        return noAccents;
    }
}


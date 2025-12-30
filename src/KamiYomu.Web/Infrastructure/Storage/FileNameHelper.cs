using System.Text;
using System.Text.RegularExpressions;

namespace KamiYomu.Web.Infrastructure.Storage;

public static class FileNameHelper
{
    public static string SanitizeFileName(string input, string replacement = "_")
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        string normalized = input.Normalize(NormalizationForm.FormD);

        StringBuilder sb = new();
        foreach (char c in normalized)
        {
            System.Globalization.UnicodeCategory uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                _ = sb.Append(c);
            }
        }
        string noAccents = sb.ToString().Normalize(NormalizationForm.FormC);

        char[] invalidChars = Path.GetInvalidFileNameChars();
        foreach (char c in invalidChars)
        {
            noAccents = noAccents.Replace(c.ToString(), replacement);
        }

        noAccents = Regex.Replace(noAccents, $"{Regex.Escape(replacement)}+", replacement);

        noAccents = noAccents.Trim().Trim(replacement.ToCharArray());

        return noAccents;
    }
}


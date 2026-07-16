using System.Text;
using System.Text.RegularExpressions;

namespace KamiYomu.Web.Infrastructure.Storage;


public static class FileNameHelper
{
    /// <summary>
    /// Sanitizes a file name by removing invalid characters and replacing them with a specified replacement string.
    /// </summary>
    /// <param name="input">The input file name to sanitize.</param>
    /// <param name="replacement">The string to replace invalid characters with.</param>
    /// <returns>The sanitized file name.</returns>
    public static string SanitizeFileName(string input, string replacement = "_")
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        if (string.IsNullOrEmpty(replacement))
        {
            replacement = "_";
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
        // Ensure both single and double quotes are considered invalid for filenames.
        HashSet<char> invalidSet = [.. invalidChars];

        foreach (char c in invalidSet)
        {
            noAccents = noAccents.Replace(c.ToString(), replacement);
        }

        noAccents = Regex.Replace(noAccents, $"{Regex.Escape(replacement)}+", replacement);

        noAccents = noAccents.Trim().Trim(replacement.ToCharArray());

        return noAccents;
    }
    /// <summary>
    /// Normalizes a system path based on the operating system.
    /// </summary>
    /// <param name="raw">The raw path to normalize.</param>
    /// <returns>The normalized system path.</returns>
    public static string NormalizeSystemPath(string raw)
    {
        bool isDocker = IsRunningInDocker();

        string baseDir = !isDocker
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KamiYomu")
            : "/"; // Docker keeps absolute paths

        if (string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }

        // Docker: convert "/db" → "/db"
        // Linux: convert "/db" → "/home/<User>/.local/share/db"
        // Windows: convert "/db" → "C:\Users\<User>\AppData\Local\KamiYomu\db"
        if (raw.StartsWith("/"))
        {
            raw = raw.TrimStart('/');
        }

        return Path.Combine(baseDir, raw);
    }

    /// <summary>
    /// Determines whether the application is running inside a Docker container.
    /// </summary>
    /// <returns><c>true</c> if running in Docker; otherwise, <c>false</c>.</returns>
    public static bool IsRunningInDocker()
    {
        return File.Exists("/.dockerenv");
    }


}


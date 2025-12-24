using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Infrastructure.Storage;

namespace KamiYomu.Web.Infrastructure.Services;

public static class TemplateResolver
{
    public static string Resolve(string template, Manga? manga, Chapter? chapter, DateTime? date = null)
    {
        if (string.IsNullOrWhiteSpace(template))
            return string.Empty;

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Merge(map, GetMangaVariables(manga));
        Merge(map, GetChapterVariables(chapter));
        Merge(map, GetDateTimeVariables(date));

        foreach (var kv in map)
        {
            template = template.Replace("{" + kv.Key + "}", kv.Value ?? string.Empty);
        }

        return template.Trim('/').Trim();
    }

    private static void Merge(Dictionary<string, string> target, Dictionary<string, string> source)
    {
        foreach (var kv in source)
            target[kv.Key] = kv.Value;
    }

    public static Dictionary<string, string> GetMangaVariables(Manga? manga)
    {
        if(manga == null)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["manga_title"] = "",
                ["manga_title_slug"] = "",
                ["manga_familysafe"] = "",
            };
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["manga_title"] = FileNameHelper.SanitizeFileName(manga.Title) ?? "",
            ["manga_title_slug"] = Slugify(FileNameHelper.SanitizeFileName(manga.Title) ?? ""),
            ["manga_familysafe"] = manga.IsFamilySafe.ToString(),
        };
    }


    public static Dictionary<string, string> GetChapterVariables(Chapter? chapter)
    {
        if(chapter == null)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["chapter"] = "",
                ["chapter_raw"] = "",
                ["chapter_title"] = "",
                ["chapter_title_slug"] = "",
                ["volume"] = ""
            };
        }

        string chapterPadded = chapter.Number.ToString("0000");

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["chapter"] = chapterPadded,
            ["chapter_raw"] = chapter.Number.ToString(),
            ["chapter_title"] = FileNameHelper.SanitizeFileName(chapter.Title) ?? "",
            ["chapter_title_slug"] = Slugify(FileNameHelper.SanitizeFileName(chapter.Title) ?? ""),
            ["volume"] = chapter.Volume.ToString()
        };
    }


    public static Dictionary<string, string> GetDateTimeVariables(DateTime? date = null)
    {
        date ??= DateTime.Now;

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["date"] = date.Value.ToString("yyyy-MM-dd"),
            ["date_short"] = date.Value.ToString("yyyy-MM-dd"),
            ["date_compact"] = date.Value.ToString("yyyyMMdd"),

            ["time"] = date.Value.ToString("HH-mm-ss"),
            ["time_compact"] = date.Value.ToString("HHmmss"),

            ["datetime"] = date.Value.ToString("yyyy-MM-dd HH-mm-ss"),
            ["datetime_compact"] = date.Value.ToString("yyyyMMdd_HHmmss"),

            ["year"] = date.Value.ToString("yyyy"),
            ["month"] = date.Value.ToString("MM"),
            ["day"] = date.Value.ToString("dd"),
            ["hour"] = date.Value.ToString("HH"),
            ["minute"] = date.Value.ToString("mm"),
            ["second"] = date.Value.ToString("ss")
        };
    }

    private static string Slugify(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        return text
            .ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-");
    }
}



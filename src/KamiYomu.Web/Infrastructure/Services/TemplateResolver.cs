using KamiYomu.CrawlerAgents.Core.Catalog;

namespace KamiYomu.Web.Infrastructure.Services;

public static class TemplateResolver
{
    public static string Resolve(string template, Manga? manga, Chapter? chapter, Page? page)
    {
        if (string.IsNullOrWhiteSpace(template))
            return string.Empty;

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Merge(map, GetMangaVariables(manga));
        Merge(map, GetChapterVariables(chapter));
        Merge(map, GetDateTimeVariables());

        foreach (var kv in map)
        {
            template = template.Replace("{" + kv.Key + "}", kv.Value ?? string.Empty);
        }

        return template;
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
            ["manga_title"] = manga.Title ?? "",
            ["manga_title_slug"] = Slugify(manga.Title ?? ""),
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
            ["chapter_title"] = chapter.Title ?? "",
            ["chapter_title_slug"] = Slugify(chapter.Title ?? ""),
            ["volume"] = chapter.Volume.ToString()
        };
    }


    public static Dictionary<string, string> GetDateTimeVariables()
    {
        var now = DateTime.Now;

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["date"] = now.ToString("yyyy-MM-dd"),
            ["date_short"] = now.ToString("yyyy-MM-dd"),
            ["date_compact"] = now.ToString("yyyyMMdd"),

            ["time"] = now.ToString("HH-mm-ss"),
            ["time_compact"] = now.ToString("HHmmss"),

            ["datetime"] = now.ToString("yyyy-MM-dd HH-mm-ss"),
            ["datetime_compact"] = now.ToString("yyyyMMdd_HHmmss"),

            ["year"] = now.ToString("yyyy"),
            ["month"] = now.ToString("MM"),
            ["day"] = now.ToString("dd"),
            ["hour"] = now.ToString("HH"),
            ["minute"] = now.ToString("mm"),
            ["second"] = now.ToString("ss")
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



using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Infrastructure.Storage;

namespace KamiYomu.Web.Infrastructure.Services;

public static class TemplateResolver
{
    public static string Resolve(string template, Manga? manga, Chapter? chapter, DateTime? date = null)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);

        Merge(map, GetMangaVariables(manga));
        Merge(map, GetChapterVariables(chapter));
        Merge(map, GetDateTimeVariables(date));

        foreach (KeyValuePair<string, string> kv in map)
        {
            template = template.Replace("{" + kv.Key + "}", kv.Value ?? string.Empty);
        }

        return template.Trim('/').Trim();
    }

    private static void Merge(Dictionary<string, string> target, Dictionary<string, string> source)
    {
        foreach (KeyValuePair<string, string> kv in source)
        {
            target[kv.Key] = kv.Value;
        }
    }

    public static Dictionary<string, string> GetMangaVariables(Manga? manga)
    {
        return manga == null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["manga_title"] = "",
                ["manga_title_slug"] = "",
                ["manga_familysafe"] = "",
            }
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["manga_title"] = FileNameHelper.SanitizeFileName(manga.Title) ?? "",
            ["manga_title_slug"] = Slugify(FileNameHelper.SanitizeFileName(manga.Title) ?? ""),
            ["manga_familysafe"] = manga.IsFamilySafe.ToString(),
        };
    }


    public static Dictionary<string, string> GetChapterVariables(Chapter? chapter)
    {
        return chapter == null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["chapter"] = "",
                ["chapter_padded_1"] = "",
                ["chapter_padded_2"] = "",
                ["chapter_padded_3"] = "",
                ["chapter_padded_4"] = "",
                ["chapter_padded_5"] = "",
                ["chapter_title"] = "",
                ["chapter_title_slug"] = "",
                ["volume"] = "",
                ["volume_padded_1"] = "",
                ["volume_padded_2"] = "",
                ["volume_padded_3"] = "",
                ["volume_padded_4"] = "",
                ["volume_padded_5"] = ""
            }
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["chapter"] = chapter.Number.ToString(),
            ["chapter_padded_1"] = chapter.Number.ToString("0"),
            ["chapter_padded_2"] = chapter.Number.ToString("00"),
            ["chapter_padded_3"] = chapter.Number.ToString("000"),
            ["chapter_padded_4"] = chapter.Number.ToString("0000"),
            ["chapter_padded_5"] = chapter.Number.ToString("00000"),
            ["chapter_title"] = FileNameHelper.SanitizeFileName(chapter.Title) ?? "",
            ["chapter_title_slug"] = Slugify(FileNameHelper.SanitizeFileName(chapter.Title) ?? ""),
            ["volume"] = chapter.Volume.ToString(),
            ["volume_padded_1"] = chapter.Volume.ToString("0"),
            ["volume_padded_2"] = chapter.Volume.ToString("00"),
            ["volume_padded_3"] = chapter.Volume.ToString("000"),
            ["volume_padded_4"] = chapter.Volume.ToString("0000"),
            ["volume_padded_5"] = chapter.Volume.ToString("00000")
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
        return string.IsNullOrWhiteSpace(text)
            ? ""
            : text
            .ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-");
    }
}



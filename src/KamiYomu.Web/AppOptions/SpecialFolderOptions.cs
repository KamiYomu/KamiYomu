namespace KamiYomu.Web.AppOptions;

public class SpecialFolderOptions
{
    public string LogDir { get; init; } = "/logs";
    public string AgentsDir { get; init; } = "/agents";
    public string DbDir { get; init; } = "/db";
    public string MangaDir { get; init; } = "/manga";
    public string FilePathFormat { get; init; } = "{manga_title}/{manga_title} ch.{chapter_padded_4}";
    public string ComicInfoTitleFormat { get; init; } = "{manga_title} ch.{chapter_padded_4}";
    public string ComicInfoSeriesFormat { get; set; } = "{manga_title}";
}

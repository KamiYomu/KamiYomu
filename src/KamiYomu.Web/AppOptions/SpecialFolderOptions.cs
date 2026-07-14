namespace KamiYomu.Web.AppOptions;

public class SpecialFolderOptions
{
    public string LogDir { get; set; } = "/logs";
    public string AgentsDir { get; set; } = "/agents";
    public string DbDir { get; set; } = "/db";
    public string MangaDir { get; set; } = "/manga";
    public string FilePathFormat { get; init; } = "{manga_title}/{manga_title} ch.{chapter_padded_4}";
    public string ComicInfoTitleFormat { get; init; } = "{manga_title} ch.{chapter_padded_4}";
    public string ComicInfoSeriesFormat { get; init; } = "{manga_title}";
}

namespace KamiYomu.Web.Models;

public record AddItemCollection
{
    public Guid CrawlerAgentId { get; internal set; }
    public string MangaId { get; internal set; }
    public string? FilePathTemplate { get; internal set; }
    public string? ComicInfoTitleTemplate { get; internal set; }
    public string? ComicInfoSeriesTemplate { get; internal set; }
    public bool MakeThisConfigurationDefault { get; internal set; }
}

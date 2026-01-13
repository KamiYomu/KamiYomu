namespace KamiYomu.Web.AppOptions;

public class StartupOptions
{
    public string DefaultLanguage { get; set; } = "en";
    public bool FamilyMode { get; set; } = true;
    public string DefaultSearchTerm { get; set; } = "CrawlerAgents";
}

namespace KamiYomu.Web.AppOptions;

public class ChromiumOptions
{
    public bool Enabled { get; init; }
    public int RequestTimeout { get; init; }
    public string[] Arguments { get; init; }
}

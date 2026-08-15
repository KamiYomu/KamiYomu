namespace KamiYomu.Web.AppOptions;
/// <summary>
/// CloudflareSolverOptions represents configuration options for the Cloudflare solver functionality.
/// </summary>
public class CloudflareSolverOptions
{
    /// <summary>
    /// 
    /// </summary>
    public bool Enabled { get; init; } = false;
    /// <summary>
    /// 
    /// </summary>
    public Uri? Uri { get; init; }
    /// <summary>
    /// 
    /// </summary>
    public int MaxTimeout { get; init; } = 60_000;
}

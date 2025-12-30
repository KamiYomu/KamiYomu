namespace KamiYomu.Web.AppOptions;

public class BasicAuthOptions
{
    public bool Enabled { get; init; }
    public required string AdminUsername { get; init; }
    public required string AdminPassword { get; init; }
}

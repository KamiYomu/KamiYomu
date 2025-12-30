namespace KamiYomu.Web.Entities.Addons;

public class NugetDependencyInfo
{
    public string? Id { get; init; }
    public string? TargetFramework { get; init; }
    public string? VersionRange { get; init; }
}
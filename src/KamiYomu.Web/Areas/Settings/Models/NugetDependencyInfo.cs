namespace KamiYomu.Web.Areas.Settings.Models;

public class NugetDependencyInfo
{
    public string? Id { get; init; }
    public string? TargetFramework { get; init; }
    public string? VersionRange { get; init; }
}
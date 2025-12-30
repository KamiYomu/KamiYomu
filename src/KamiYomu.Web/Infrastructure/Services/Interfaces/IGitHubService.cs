namespace KamiYomu.Web.Infrastructure.Services.Interfaces;

public interface IGitHubService
{
    Task<string> GetLatestVersionAsync(CancellationToken cancellationToken);
    Task<bool> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken);
}

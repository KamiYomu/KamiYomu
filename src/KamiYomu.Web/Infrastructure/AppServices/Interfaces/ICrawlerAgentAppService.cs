using KamiYomu.Web.Entities;

namespace KamiYomu.Web.Infrastructure.AppServices.Interfaces;

/// <summary>
/// 
/// </summary>
public interface ICrawlerAgentAppService
{
    /// <summary>
    /// Recreates the manga download collection for the specified library, updating its crawler agent information and ensuring that the latest chapters are available.
    /// </summary>
    /// <param name="library">The library to refresh.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The refreshed library.</returns>
    Task<Library> RefreshCollectionAsync(Library library, CancellationToken cancellationToken);
    /// <summary>
    /// Upgrade crawler agent in the library and restart all jobs
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="crawlerAgentId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Library> UpgradeCrawlerAgentAsync(Guid libraryId, Guid crawlerAgentId, CancellationToken cancellationToken);
}

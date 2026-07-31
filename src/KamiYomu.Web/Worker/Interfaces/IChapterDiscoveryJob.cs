using Hangfire;
using Hangfire.Server;

using KamiYomu.Web.Worker.Attributes;

using System.ComponentModel;

namespace KamiYomu.Web.Worker.Interfaces;
/// <summary>
/// Defines a contract for dispatching asynchronous jobs to discover new chapters in a library.
/// </summary>
/// <remarks>Implementations should handle concurrency per crawler and support integration with background job
/// processing frameworks.</remarks>
public interface IChapterDiscoveryJob
{
    /// <summary>
    /// Scanning for new chapters for downloading
    /// </summary>
    /// <param name="queue">The name of the queue from which to dispatch the message.</param>
    /// <param name="crawlerId">The unique identifier for the crawler instance.</param>
    /// <param name="libraryId">The unique identifier for the library associated with the manga.</param>
    /// <param name="context">The context for performing the operation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Queue("{0}")]
    [DisplayName("Discovery New Chapter")]
    [PerKeyConcurrency("crawlerId")]
    Task DispatchAsync(string queue, Guid crawlerId, Guid libraryId, PerformContext context, CancellationToken cancellationToken);
}

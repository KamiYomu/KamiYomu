using Hangfire;
using Hangfire.Server;
using System.ComponentModel;

namespace KamiYomu.Web.Worker.Interfaces
{
    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    public interface IChapterDiscoveryJob
    {
        [DisplayName("Discovery New Chapter")]
        Task DispatchAsync(Guid crawlerId, Guid libraryId, PerformContext context, CancellationToken cancellationToken);
    }
}

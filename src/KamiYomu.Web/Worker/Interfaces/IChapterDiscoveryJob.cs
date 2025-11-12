using Hangfire;
using Hangfire.Server;
using KamiYomu.Web.Worker.Attributes;
using System.ComponentModel;

namespace KamiYomu.Web.Worker.Interfaces
{
    public interface IChapterDiscoveryJob
    {
        [PerKeyConcurrency("crawlerId")]
        [DisplayName("Discovery New Chapter")]
        Task DispatchAsync(Guid crawlerId, Guid libraryId, PerformContext context, CancellationToken cancellationToken);
    }
}

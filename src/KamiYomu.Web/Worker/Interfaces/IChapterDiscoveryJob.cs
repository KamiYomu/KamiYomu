using Hangfire;
using Hangfire.Server;
using System.ComponentModel;

namespace KamiYomu.Web.Worker.Interfaces
{
    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    public interface IChapterDiscoveryJob
    {
        [DisplayName("Monitor Chapter Release")]
        Task DispatchAsync(PerformContext context, CancellationToken cancellationToken);
    }
}

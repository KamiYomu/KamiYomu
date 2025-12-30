using Hangfire;
using Hangfire.Server;

using System.ComponentModel;

namespace KamiYomu.Web.Worker.Interfaces;

public interface IDeferredExecutionCoordinator
{

    [Queue("{0}")]
    [DisplayName("Deferred Execution Coordinator")]
    Task DispatchAsync(string queue, PerformContext context, CancellationToken cancellationToken);
}

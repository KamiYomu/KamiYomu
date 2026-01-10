using Hangfire;
using Hangfire.Server;

namespace KamiYomu.Web.Worker.Interfaces;

public interface INotifyKavitaJob
{
    [Queue("{0}")]
    [JobDisplayName("Notify Kavita")]
    Task DispatchAsync(string queue, PerformContext context, CancellationToken cancellationToken);
}

using Hangfire;
using Hangfire.Server;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities.Worker;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Worker.Interfaces;

namespace KamiYomu.Web.Worker;

public class DeferredExecutionCoordinator(ILogger<DeferredExecutionCoordinator> logger) : IDeferredExecutionCoordinator
{
    public Task DispatchAsync(string queue, PerformContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Dispatch \"{title}\".", nameof(DeferredExecutionCoordinator));
        var monitoring = JobStorage.Current.GetMonitoringApi();
        var now = DateTime.UtcNow;

        var enqueued = monitoring.EnqueuedJobs(queue, 0, int.MaxValue)
            .Where(j => j.Value.EnqueuedAt.HasValue && j.Value.EnqueuedAt.Value < now)
            .Select(j => new PastJobInfo(j.Key, j.Value.EnqueuedAt, "Enqueued", j.Value.Job.Method.Name, j.Value.Job.Type.FullName))
            .ToList();

        var scheduled = monitoring.ScheduledJobs(0, int.MaxValue)
            .Where(j => 
                j.Value.EnqueueAt < now)
            .Select(j => new PastJobInfo(j.Key, j.Value.EnqueueAt, "Scheduled", j.Value.Job.Method.Name, j.Value.Job.Type.FullName))
            .ToList();

        var allPastJobs = enqueued.Concat(scheduled).ToList();

        foreach (var job in allPastJobs)
        {
            job.EnqueueAfterDelay(TimeSpan.FromMinutes(Defaults.Worker.StaleLockTimeout + 1));

            logger.LogInformation(
                "[{State}] JobId={JobId} Method={Method} Type={Type} Time={Time}",
                job.State,
                job.JobId,
                job.Method,
                job.Type,
                job.Time
            );

        }

        logger.LogInformation("Dispatch \"{title}\" completed.", nameof(DeferredExecutionCoordinator));

        return Task.CompletedTask;
    }
}

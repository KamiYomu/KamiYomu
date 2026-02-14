using Hangfire;
using Hangfire.Server;

using KamiYomu.Web.Areas.Settings.Models;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Worker.Interfaces;

namespace KamiYomu.Web.Worker;

public class DeferredExecutionCoordinator(ILogger<DeferredExecutionCoordinator> logger) : IDeferredExecutionCoordinator
{
    public Task DispatchAsync(string queue, PerformContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Dispatch \"{title}\".", nameof(DeferredExecutionCoordinator));
        Hangfire.Storage.IMonitoringApi monitoring = JobStorage.Current.GetMonitoringApi();
        DateTime now = DateTime.UtcNow;

        List<PastJobInfo> enqueued = [.. monitoring.EnqueuedJobs(queue, 0, int.MaxValue)
            .Where(j => j.Value.EnqueuedAt.HasValue && j.Value.EnqueuedAt.Value < now)
            .Select(j => new PastJobInfo(j.Key, j.Value.EnqueuedAt, "Enqueued", j.Value.Job.Method.Name, j.Value.Job.Type.FullName))];

        List<PastJobInfo> scheduled = [.. monitoring.ScheduledJobs(0, int.MaxValue)
            .Where(j =>
                j.Value.EnqueueAt < now)
            .Select(j => new PastJobInfo(j.Key, j.Value.EnqueueAt, "Scheduled", j.Value.Job.Method.Name, j.Value.Job.Type.FullName))];

        List<PastJobInfo> allPastJobs = [.. enqueued, .. scheduled];

        foreach (PastJobInfo? job in allPastJobs)
        {
            job.EnqueueImmediately();

            logger.LogInformation(
                "[{State}] JobId={JobId} Method={Method} Type={Type} Time={Time}",
                job.State,
                job.JobId,
                job.Method,
                job.Type,
                job.Time
            );

        }

        context.SetJobParameter($"{nameof(enqueued)}Found", enqueued.Count);
        context.SetJobParameter($"{nameof(scheduled)}Found", scheduled.Count);
        context.SetJobParameter($"totalFound", allPastJobs.Count);
        logger.LogInformation("Dispatch \"{title}\" completed.", nameof(DeferredExecutionCoordinator));
        return Task.CompletedTask;
    }
}

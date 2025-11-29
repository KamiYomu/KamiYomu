using Hangfire;
using Hangfire.States;
using Hangfire.Storage;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities.Worker;

namespace KamiYomu.Web.Extensions;

public static class HangfireExtensions
{
    public static void EnqueueAfterDelay(this BackgroundJob backgroundJob, TimeSpan delay, JobStorage storage)
    {
        using var connection = storage.GetConnection();

        using var transaction = connection.CreateWriteTransaction();

        var queue = backgroundJob.Job.Queue;

        var newState = new ScheduledState(delay);

        transaction.SetJobState(backgroundJob.Id, newState);

        connection.SetJobParameter(backgroundJob.Id, "Queue", queue);

        transaction.Commit();
    }


    public static void EnqueueAfterDelay(this PastJobInfo pastJobInfo, TimeSpan delay, JobStorage storage)
    {
        using var connection = storage.GetConnection();

        using var transaction = connection.CreateWriteTransaction();

        var monitoringApi = storage.GetMonitoringApi();

        var jobDetails = monitoringApi.JobDetails(pastJobInfo.JobId);

        var enqueuedState = jobDetails.History.FirstOrDefault(h => h.StateName == "Enqueued");

        var queue = (enqueuedState?.Data != null
                        && enqueuedState.Data.TryGetValue("Queue", out var value)
                        && !string.IsNullOrWhiteSpace(value))
                    ? value
                    : Defaults.Worker.DefaultQueue;

        var newState = new ScheduledState(delay);
        
        transaction.SetJobState(pastJobInfo.JobId, newState);

        connection.SetJobParameter(pastJobInfo.JobId, "Queue", queue);

        transaction.Commit();
    }
}

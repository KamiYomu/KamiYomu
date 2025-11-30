using Hangfire;
using Hangfire.States;
using Hangfire.Storage;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities.Worker;

namespace KamiYomu.Web.Extensions;

public static class HangfireExtensions
{
    public static void EnqueueAfterDelay(this BackgroundJob backgroundJob, TimeSpan delay)
    {
        using var connection = JobStorage.Current.GetConnection();

        using var transaction = connection.CreateWriteTransaction();

        var queue = backgroundJob.Job.Queue;

        connection.SetJobParameter(backgroundJob.Id, "Queue", queue);

        var newState = new ScheduledState(delay);

        transaction.SetJobState(backgroundJob.Id, newState);

        transaction.AddToQueue(queue, backgroundJob.Id);

        transaction.Commit();
    }


    public static void EnqueueImmediately(this PastJobInfo pastJobInfo)
    {
        using var connection = JobStorage.Current.GetConnection();

        using var transaction = connection.CreateWriteTransaction();

        var monitoringApi = JobStorage.Current.GetMonitoringApi();

        var jobDetails = monitoringApi.JobDetails(pastJobInfo.JobId);

        var enqueuedState = jobDetails.History.FirstOrDefault(h => h.StateName == "Enqueued");

        var queue = enqueuedState?.Data["Queue"] ?? EnqueuedState.DefaultQueue;

        var newState = new EnqueuedState(queue);

        transaction.SetJobState(pastJobInfo.JobId, newState);

        transaction.AddToQueue(queue, pastJobInfo.JobId);

        transaction.Commit();
    }
}

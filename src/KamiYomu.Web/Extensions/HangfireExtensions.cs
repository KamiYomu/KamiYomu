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
        using IStorageConnection connection = JobStorage.Current.GetConnection();

        using IWriteOnlyTransaction transaction = connection.CreateWriteTransaction();

        string queue = backgroundJob.Job.Queue;

        connection.SetJobParameter(backgroundJob.Id, "Queue", queue);

        ScheduledState newState = new(delay);

        transaction.SetJobState(backgroundJob.Id, newState);

        transaction.Commit();
    }


    public static void EnqueueImmediately(this PastJobInfo pastJobInfo)
    {
        using IStorageConnection connection = JobStorage.Current.GetConnection();

        using IWriteOnlyTransaction transaction = connection.CreateWriteTransaction();

        IMonitoringApi monitoringApi = JobStorage.Current.GetMonitoringApi();

        Hangfire.Storage.Monitoring.JobDetailsDto jobDetails = monitoringApi.JobDetails(pastJobInfo.JobId);

        Hangfire.Storage.Monitoring.StateHistoryDto? enqueuedState = jobDetails.History.FirstOrDefault(h => h.StateName == "Enqueued");

        string queue = enqueuedState?.Data["Queue"] ?? EnqueuedState.DefaultQueue;

        transaction.AddToQueue(queue, pastJobInfo.JobId);

        transaction.Commit();
    }
}

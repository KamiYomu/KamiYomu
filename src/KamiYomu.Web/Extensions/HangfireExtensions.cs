using Hangfire;
using Hangfire.States;
using Hangfire.Storage;

namespace KamiYomu.Web.Extensions;

public static class HangfireExtensions
{
    public static void EnqueueAfterDelay(this BackgroundJob backgroundJob, TimeSpan delay, JobStorage storage)
    {
        using var connection = storage.GetConnection();
        using var transaction = connection.CreateWriteTransaction();

        var queue = backgroundJob.Job.Queue;

        var stateData = connection.GetStateData(backgroundJob.Id);

        var newState = new ScheduledState(delay);

        transaction.SetJobState(backgroundJob.Id, newState);

        connection.SetJobParameter(backgroundJob.Id, "Queue", queue);

        transaction.Commit();
    }
}

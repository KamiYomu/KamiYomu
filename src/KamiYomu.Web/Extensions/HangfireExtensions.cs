using Hangfire;
using Hangfire.States;
using Hangfire.Storage;

using KamiYomu.Web.Areas.Settings.Models;

using static System.Runtime.InteropServices.JavaScript.JSType;

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
    /// <summary>
    /// Gets a cron expression for a daily job at the specified time span.
    /// </summary>
    /// <param name="timeSpan"></param>
    /// <returns></returns>
    public static string ToCronDailyExpression(this TimeSpan timeSpan)
    {
        return Cron.Daily(timeSpan.Hours, timeSpan.Minutes);
    }
    /// <summary>
    /// Converts a daily cron expression to a TimeSpan representing the time of day.
    /// </summary>
    /// <param name="dailyCronExpression"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FormatException"></exception>
    public static TimeSpan ConvertCronDailyToTimeSpan(string dailyCronExpression)
    {
        if (string.IsNullOrWhiteSpace(dailyCronExpression))
        {
            throw new ArgumentException("Cron expression cannot be null or empty.", nameof(dailyCronExpression));
        }

        string[] parts = dailyCronExpression.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 5)
        {
            throw new FormatException(
                "Expected a 5-field cron expression: minute hour day-of-month month day-of-week.");
        }

        if (!int.TryParse(parts[0], out int minute) ||
            !int.TryParse(parts[1], out int hour))
        {
            throw new FormatException("The minute and hour must be numeric.");
        }

        if (minute is < 0 or > 59)
        {
            throw new FormatException("Minute must be between 0 and 59.");
        }

        if (hour is < 0 or > 23)
        {
            throw new FormatException("Hour must be between 0 and 23.");
        }

        // For a daily cron, the remaining fields should apply every day.
        return parts[2] != "*" || parts[3] != "*" || parts[4] != "*"
            ? throw new FormatException(
                "The cron expression is not a daily schedule.")
            : new TimeSpan(hour, minute, 0);
    }
}

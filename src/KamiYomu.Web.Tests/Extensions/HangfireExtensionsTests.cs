using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;

using KamiYomu.Web.Areas.Settings.Models;
using KamiYomu.Web.Extensions;

namespace KamiYomu.Web.Tests.Extensions;

[Collection(JobStorageTestCollection.Name)]
public class HangfireExtensionsTests
{
    [Fact]
    public void EnqueueAfterDelay_SetsQueueParameterAndScheduledState()
    {
        Mock<IStorageConnection> connection = new();
        Mock<IWriteOnlyTransaction> transaction = new();
        Mock<JobStorage> jobStorage = new();
        JobStorage? originalJobStorage = JobStorageTestHelper.GetCurrentOrNull();

        _ = connection.Setup(x => x.CreateWriteTransaction()).Returns(transaction.Object);
        _ = jobStorage.Setup(x => x.GetConnection()).Returns(connection.Object);

        JobStorage.Current = jobStorage.Object;

        BackgroundJob backgroundJob = new(
            "job-1",
            Job.FromExpression(() => NoOp(), "critical"),
            DateTime.UtcNow);

        try
        {
            backgroundJob.EnqueueAfterDelay(TimeSpan.FromMinutes(5));
        }
        finally
        {
            JobStorageTestHelper.Restore(originalJobStorage);
        }

        connection.Verify(x => x.SetJobParameter("job-1", "Queue", "critical"), Times.Once);
        transaction.Verify(x => x.SetJobState("job-1", It.Is<IState>(s => s is ScheduledState)), Times.Once);
        transaction.Verify(x => x.Commit(), Times.Once);
    }

    [Fact]
    public void EnqueueImmediately_UsesEnqueuedQueueFromJobHistory()
    {
        Mock<IStorageConnection> connection = new();
        Mock<IWriteOnlyTransaction> transaction = new();
        Mock<IMonitoringApi> monitoringApi = new();
        Mock<JobStorage> jobStorage = new();
        JobStorage? originalJobStorage = JobStorageTestHelper.GetCurrentOrNull();

        _ = connection.Setup(x => x.CreateWriteTransaction()).Returns(transaction.Object);
        _ = jobStorage.Setup(x => x.GetConnection()).Returns(connection.Object);
        _ = jobStorage.Setup(x => x.GetMonitoringApi()).Returns(monitoringApi.Object);
        _ = monitoringApi.Setup(x => x.JobDetails("job-1")).Returns(new JobDetailsDto
        {
            History = new List<StateHistoryDto>
            {
                new()
                {
                    StateName = "Enqueued",
                    Data = new Dictionary<string, string> { ["Queue"] = "priority" }
                }
            }
        });

        JobStorage.Current = jobStorage.Object;

        try
        {
            new PastJobInfo("job-1", DateTime.UtcNow, "Scheduled", "Run", "Type").EnqueueImmediately();
        }
        finally
        {
            JobStorageTestHelper.Restore(originalJobStorage);
        }

        transaction.Verify(x => x.AddToQueue("priority", "job-1"), Times.Once);
        transaction.Verify(x => x.Commit(), Times.Once);
    }

    [Fact]
    public void EnqueueImmediately_UsesDefaultQueueWhenEnqueuedStateIsMissing()
    {
        Mock<IStorageConnection> connection = new();
        Mock<IWriteOnlyTransaction> transaction = new();
        Mock<IMonitoringApi> monitoringApi = new();
        Mock<JobStorage> jobStorage = new();
        JobStorage? originalJobStorage = JobStorageTestHelper.GetCurrentOrNull();

        _ = connection.Setup(x => x.CreateWriteTransaction()).Returns(transaction.Object);
        _ = jobStorage.Setup(x => x.GetConnection()).Returns(connection.Object);
        _ = jobStorage.Setup(x => x.GetMonitoringApi()).Returns(monitoringApi.Object);
        _ = monitoringApi.Setup(x => x.JobDetails("job-2")).Returns(new JobDetailsDto
        {
            History = new List<StateHistoryDto>()
        });

        JobStorage.Current = jobStorage.Object;

        try
        {
            new PastJobInfo("job-2", DateTime.UtcNow, "Scheduled", "Run", "Type").EnqueueImmediately();
        }
        finally
        {
            JobStorageTestHelper.Restore(originalJobStorage);
        }

        transaction.Verify(x => x.AddToQueue(EnqueuedState.DefaultQueue, "job-2"), Times.Once);
        transaction.Verify(x => x.Commit(), Times.Once);
    }

    [Fact]
    public void ToCronDailyExpression_ReturnsDailyCronExpression()
    {
        string cron = new TimeSpan(14, 35, 0).ToCronDailyExpression();

        Assert.Equal(Cron.Daily(14, 35), cron);
    }

    [Fact]
    public void ConvertCronDailyToTimeSpan_ReturnsTimeSpanForValidDailyExpression()
    {
        TimeSpan result = HangfireExtensions.ConvertCronDailyToTimeSpan("35 14 * * *");

        Assert.Equal(new TimeSpan(14, 35, 0), result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertCronDailyToTimeSpan_ThrowsArgumentExceptionForNullOrWhitespace(string? cronExpression)
    {
        _ = Assert.Throws<ArgumentException>(() => HangfireExtensions.ConvertCronDailyToTimeSpan(cronExpression!));
    }

    [Theory]
    [InlineData("* * * *")]
    [InlineData("* * * * * *")]
    public void ConvertCronDailyToTimeSpan_ThrowsFormatExceptionForIncorrectFieldCount(string cronExpression)
    {
        _ = Assert.Throws<FormatException>(() => HangfireExtensions.ConvertCronDailyToTimeSpan(cronExpression));
    }

    [Theory]
    [InlineData("AA 14 * * *")]
    [InlineData("15 BB * * *")]
    [InlineData("60 14 * * *")]
    [InlineData("15 24 * * *")]
    [InlineData("15 14 1 * *")]
    public void ConvertCronDailyToTimeSpan_ThrowsFormatExceptionForInvalidDailyExpression(string cronExpression)
    {
        _ = Assert.Throws<FormatException>(() => HangfireExtensions.ConvertCronDailyToTimeSpan(cronExpression));
    }

    public static void NoOp()
    {
    }
}

[CollectionDefinition("job-storage-tests", DisableParallelization = true)]
public class JobStorageTestCollection
{
    public const string Name = "job-storage-tests";
}

/// <summary>
/// Hangfire's <see cref="JobStorage.Current"/> throws <see cref="InvalidOperationException"/>
/// until something has assigned it at least once in the process. Tests that save/restore the
/// ambient value need to tolerate that "never initialized" state instead of crashing when they
/// happen to be the first test in the run to touch it.
/// </summary>
internal static class JobStorageTestHelper
{
    public static JobStorage? GetCurrentOrNull()
    {
        try
        {
            return JobStorage.Current;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public static void Restore(JobStorage? original)
    {
        if (original != null)
        {
            JobStorage.Current = original;
        }
    }
}

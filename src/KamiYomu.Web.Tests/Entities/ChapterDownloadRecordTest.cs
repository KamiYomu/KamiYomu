using System.Reflection;

using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
namespace KamiYomu.Web.Tests.Entities;
public class ChapterDownloadRecordTests
{
    private Library CreateLibrary()
    {
        return new Library(new CrawlerAgent(), new Manga(), "test", "test", "test");
    }
    private ChapterDownloadRecord CreateTestRecord()
    {
        return new ChapterDownloadRecord(
            new CrawlerAgent(),
            new MangaDownloadRecord(CreateLibrary(), "job_01"),
            new Chapter()
        );
    }

    [Fact]
    public void Constructor_SetsInitialStateCorrectly()
    {
        // Act
        ChapterDownloadRecord record = CreateTestRecord();

        // Assert
        Assert.Equal(DownloadStatus.ToBeRescheduled, record.DownloadStatus);
        Assert.True((DateTimeOffset.UtcNow - record.CreateAt).TotalSeconds < 5);
        _ = Assert.NotNull(record.StatusUpdateAt);
    }

    [Fact]
    public void Scheduled_UpdatesJobIdAndStatus()
    {
        // Arrange
        ChapterDownloadRecord record = CreateTestRecord();
        string expectedJobId = "job_99";

        // Act
        record.Scheduled(expectedJobId);

        // Assert
        Assert.Equal(DownloadStatus.Scheduled, record.DownloadStatus);
        Assert.Equal(expectedJobId, record.BackgroundJobId);
        Assert.Null(record.StatusReason);
    }

    [Fact]
    public void Cancelled_SetsStatusAndReason_AndClearsJobId()
    {
        // Arrange
        ChapterDownloadRecord record = CreateTestRecord();
        record.Scheduled("old_job");

        // Act
        record.Cancelled("Disk Full");

        // Assert
        Assert.Equal(DownloadStatus.Cancelled, record.DownloadStatus);
        Assert.Equal("Disk Full", record.StatusReason);
        Assert.Equal(string.Empty, record.BackgroundJobId);
    }

    [Theory]
    [InlineData(DownloadStatus.ToBeRescheduled, true)]
    [InlineData(DownloadStatus.Scheduled, true)]
    [InlineData(DownloadStatus.InProgress, false)]
    [InlineData(DownloadStatus.Completed, false)]
    [InlineData(DownloadStatus.Cancelled, false)]
    public void ShouldRun_ReturnsCorrectBooleanBasedOnStatus(DownloadStatus status, bool expected)
    {
        // Arrange
        ChapterDownloadRecord record = CreateTestRecord();
        // Since DownloadStatus is private set, we simulate the state transitions
        if (status == DownloadStatus.Scheduled)
        {
            record.Scheduled("test");
        }
        else if (status == DownloadStatus.InProgress)
        {
            record.Processing();
        }
        else if (status == DownloadStatus.Completed)
        {
            record.Complete();
        }
        else if (status == DownloadStatus.Cancelled)
        {
            record.Cancelled("test");
        }

        // Act
        bool result = record.ShouldRun();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsReschedulable_ReturnsTrue_OnlyWhenFinished()
    {
        // Arrange
        ChapterDownloadRecord record = CreateTestRecord();

        // Act & Assert
        record.Complete();
        Assert.True(record.IsReschedulable());

        record.Cancelled("Error");
        Assert.True(record.IsReschedulable());

        record.Processing();
        Assert.False(record.IsReschedulable());
    }

    [Fact]
    public void LastUpdatedStatusTotalDays_ReturnsZeroForNewRecord()
    {
        // Arrange
        ChapterDownloadRecord record = CreateTestRecord();

        // Act
        int days = record.LastUpdatedStatusTotalDays();

        // Assert
        Assert.Equal(0, days);
    }

    [Fact]
    public void IsInProgress_ShouldReturnTrue_WhenStatusIsScheduled()
    {
        ChapterDownloadRecord record = CreateTestRecord();
        record.Scheduled("job-1");

        Assert.True(record.IsInProgress());
    }

    [Fact]
    public void IsInProgress_ShouldReturnTrue_WhenStatusIsInProgressAndNotStale()
    {
        ChapterDownloadRecord record = CreateTestRecord();

        record.Processing();

        Assert.True(record.IsInProgress());
    }

    [Fact]
    public void IsInProgress_ShouldReturnFalse_WhenStatusIsInProgressButStale()
    {
        ChapterDownloadRecord record = CreateTestRecord();
        record.Processing();

        SetStatusUpdateAt(record, DateTimeOffset.UtcNow.AddDays(-2));

        Assert.True(record.IsStale());
        Assert.False(record.IsInProgress());
    }

    [Theory]
    [InlineData(DownloadStatus.Completed, true)]
    [InlineData(DownloadStatus.Cancelled, true)]
    [InlineData(DownloadStatus.InProgress, false)]
    [InlineData(DownloadStatus.Scheduled, false)]
    public void IsReschedulable_ShouldReturnExpectedResult(DownloadStatus status, bool expected)
    {
        ChapterDownloadRecord record = CreateTestRecord();

        // Arrange state
        if (status == DownloadStatus.Completed)
        {
            record.Complete();
        }

        if (status == DownloadStatus.Cancelled)
        {
            record.Cancelled("test");
        }

        if (status == DownloadStatus.InProgress)
        {
            record.Processing();
        }

        if (status == DownloadStatus.Scheduled)
        {
            record.Scheduled("test");
        }

        // Act & Assert
        Assert.Equal(expected, record.IsReschedulable());
    }

    [Fact]
    public void LastUpdatedStatusTotalDays_ShouldReturnCorrectDays()
    {
        ChapterDownloadRecord record = CreateTestRecord();

        // Set timestamp to 5 days ago
        SetStatusUpdateAt(record, DateTimeOffset.UtcNow.AddDays(-5.5));

        // Act
        int days = record.LastUpdatedStatusTotalDays();

        // Assert
        Assert.Equal(5, days);
    }

    [Fact]
    public void IsCancelled_And_IsCompleted_ShouldReturnCorrectBooleans()
    {
        ChapterDownloadRecord record = CreateTestRecord();

        record.Complete();
        Assert.True(record.IsCompleted());
        Assert.False(record.IsCancelled());

        record.Cancelled("User stop");
        Assert.True(record.IsCancelled());
        Assert.False(record.IsCompleted());
    }

    private void SetStatusUpdateAt(ChapterDownloadRecord record, DateTimeOffset value)
    {
        PropertyInfo? prop = typeof(ChapterDownloadRecord).GetProperty("StatusUpdateAt", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(record, value);
    }
}

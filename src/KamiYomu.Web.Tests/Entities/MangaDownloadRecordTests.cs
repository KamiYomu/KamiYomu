using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;

namespace KamiYomu.Web.Tests.Entities;
public class MangaDownloadRecordTests
{
    private Library CreateLibrary()
    {
        return new Library(new CrawlerAgent(), new Manga(), "test", "test", "test");
    }
    private MangaDownloadRecord CreateRecord()
    {
        return new MangaDownloadRecord(CreateLibrary(), "initial-job-id");
    }

    [Fact]
    public void Constructor_ShouldSetInitialProperties()
    {
        // Act
        MangaDownloadRecord record = CreateRecord();

        // Assert
        Assert.Equal(DownloadStatus.ToBeRescheduled, record.DownloadStatus);
        Assert.Equal("initial-job-id", record.BackgroundJobId);
        Assert.True(record.CreateAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Schedule_ShouldUpdateStatusAndNewJobId()
    {
        // Arrange
        MangaDownloadRecord record = CreateRecord();

        // Act
        record.Schedule("new-job-123");

        // Assert
        Assert.Equal(DownloadStatus.Scheduled, record.DownloadStatus);
        Assert.Equal("new-job-123", record.BackgroundJobId);
        Assert.Null(record.StatusReason);
    }

    [Fact]
    public void Cancelled_ShouldUpdateStatusAndReason()
    {
        // Arrange
        MangaDownloadRecord record = CreateRecord();

        // Act
        record.Cancelled("Manual stop");

        // Assert
        Assert.Equal(DownloadStatus.Cancelled, record.DownloadStatus);
        Assert.Equal("Manual stop", record.StatusReason);
    }

    [Theory]
    [InlineData(DownloadStatus.ToBeRescheduled, true)]
    [InlineData(DownloadStatus.Scheduled, true)]
    [InlineData(DownloadStatus.Completed, false)]
    [InlineData(DownloadStatus.Cancelled, false)]
    public void ShouldRun_ReturnsTrue_ForPrimaryStatuses(DownloadStatus status, bool expected)
    {
        // Arrange
        MangaDownloadRecord record = CreateRecord();
        SetStatusViaMethod(record, status);

        // Act
        bool result = record.ShouldRun();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ShouldRun_ReturnsTrue_WhenInProgressButStale()
    {
        // Arrange
        MangaDownloadRecord record = CreateRecord();
        record.Processing();

        // Force StatusUpdateAt to 2 days ago
        SetStatusUpdateAt(record, DateTimeOffset.UtcNow.AddDays(-2));

        // Act & Assert
        Assert.True(record.IsStale());
        Assert.True(record.ShouldRun()); // ShouldRun logic: (ToBeRescheduled || Scheduled || IsStale)
    }

    [Fact]
    public void IsStale_ReturnsFalse_WhenInProgressAndFresh()
    {
        // Arrange
        MangaDownloadRecord record = CreateRecord();
        record.Processing();

        // Act & Assert
        Assert.False(record.IsStale());
    }

    [Fact]
    public void Pending_UpdatesStatusAndReason()
    {
        // Arrange
        MangaDownloadRecord record = CreateRecord();

        // Act
        record.Pending("Network timeout");

        // Assert
        Assert.Equal(DownloadStatus.ToBeRescheduled, record.DownloadStatus);
        Assert.Equal("Network timeout", record.StatusReason);
    }


    private void SetStatusUpdateAt(MangaDownloadRecord record, DateTimeOffset value)
    {
        System.Reflection.PropertyInfo? prop = typeof(MangaDownloadRecord).GetProperty("StatusUpdateAt");
        prop?.SetValue(record, value);
    }

    private void SetStatusViaMethod(MangaDownloadRecord record, DownloadStatus status)
    {
        switch (status)
        {
            case DownloadStatus.Scheduled: record.Schedule("test"); break;
            case DownloadStatus.InProgress: record.Processing(); break;
            case DownloadStatus.Completed: record.Complete(); break;
            case DownloadStatus.Cancelled: record.Cancelled("test"); break;
            case DownloadStatus.ToBeRescheduled: record.Pending("test"); break;
            default:
                break;
        }
    }
}

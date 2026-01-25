using System.Reflection;

using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.ViewComponents;

using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.ViewComponents;
public class BadgeChapterDownloadStatusViewComponentTests
{
    private readonly Mock<IUserClockManager> _mockUserClockManager;
    private readonly BadgeChapterDownloadStatusViewComponent _component;

    public BadgeChapterDownloadStatusViewComponentTests()
    {
        _mockUserClockManager = new Mock<IUserClockManager>();
        _component = new BadgeChapterDownloadStatusViewComponent(_mockUserClockManager.Object);
    }


    /// <summary>
    /// Reflection Helper to populate the internal/private properties of ChapterDownloadRecord
    /// </summary>
    private ChapterDownloadRecord CreateRecord(DownloadStatus status, DateTimeOffset? updatedAt, string? reason)
    {
        ChapterDownloadRecord record = (ChapterDownloadRecord)Activator.CreateInstance(typeof(ChapterDownloadRecord), true)!;
        Type type = typeof(ChapterDownloadRecord);
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        type.GetProperty(nameof(ChapterDownloadRecord.DownloadStatus), flags)?.SetValue(record, status);
        type.GetProperty(nameof(ChapterDownloadRecord.StatusUpdateAt), flags)?.SetValue(record, updatedAt);
        type.GetProperty(nameof(ChapterDownloadRecord.StatusReason), flags)?.SetValue(record, reason);

        return record;
    }

    [Fact]
    public void Invoke_WithDateTimeOffset_ReturnsCorrectLegend()
    {
        // Arrange
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        DateTimeOffset userTime = new(2026, 1, 25, 10, 0, 0, TimeSpan.FromHours(-5)); // Your specific time
        string reason = "Server Timeout";

        ChapterDownloadRecord record = CreateRecord(DownloadStatus.ToBeRescheduled, utcNow, reason);

        // Match the call to the service using the record's timestamp
        _ = _mockUserClockManager
            .Setup(x => x.ConvertToUserTime(utcNow))
            .Returns(userTime);

        // Act
        Microsoft.AspNetCore.Mvc.IViewComponentResult result = _component.Invoke(record);

        // Assert
        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        BadgeChapterDownloadStatusViewComponentModel model = Assert.IsType<BadgeChapterDownloadStatusViewComponentModel>(viewResult.ViewData.Model);

        // Verify the CSS class mapping
        Assert.Equal("bg-warning", model.BadgeClass);

        // Verify the Legend string construction
        // Component uses: .ToString("g")
        string expectedTimeStr = userTime.ToString("g");
        Assert.Contains(expectedTimeStr, model.Legend);
        Assert.Contains(reason, model.Legend);
    }

    [Fact]
    public void Invoke_HandlesNullTimestamp_UsingGetValueOrDefault()
    {
        // Arrange
        // If StatusUpdateAt is null, .GetValueOrDefault() returns DateTimeOffset.MinValue
        ChapterDownloadRecord record = CreateRecord(DownloadStatus.Scheduled, null, null);
        DateTimeOffset defaultTime = DateTimeOffset.MinValue;
        DateTimeOffset userTimeResult = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        _ = _mockUserClockManager
            .Setup(x => x.ConvertToUserTime(defaultTime))
            .Returns(userTimeResult);

        // Act
        Microsoft.AspNetCore.Mvc.IViewComponentResult result = _component.Invoke(record);

        // Assert
        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        BadgeChapterDownloadStatusViewComponentModel model = Assert.IsType<BadgeChapterDownloadStatusViewComponentModel>(viewResult.ViewData.Model);

        Assert.Contains(userTimeResult.ToString("g"), model.Legend);
    }
}

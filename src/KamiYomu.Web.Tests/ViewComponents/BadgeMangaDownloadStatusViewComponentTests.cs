using System.Globalization;
using System.Reflection;

using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.ViewComponents;

using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.ViewComponents;
public class BadgeMangaDownloadStatusViewComponentTests
{
    private readonly Mock<IUserClockManager> _mockUserClockManager;
    private readonly BadgeMangaDownloadStatusViewComponent _component;

    public BadgeMangaDownloadStatusViewComponentTests()
    {
        // Fix culture for consistent string formatting in tests
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        _mockUserClockManager = new Mock<IUserClockManager>();
        _component = new BadgeMangaDownloadStatusViewComponent(_mockUserClockManager.Object);
    }

    /// <summary>
    /// Helper to create MangaDownloadRecord via reflection
    /// </summary>
    private MangaDownloadRecord CreateMangaRecord(DownloadStatus status, DateTimeOffset? updatedAt, string? reason)
    {
        MangaDownloadRecord record = (MangaDownloadRecord)Activator.CreateInstance(typeof(MangaDownloadRecord), true)!;
        Type type = typeof(MangaDownloadRecord);
        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        type.GetProperty(nameof(MangaDownloadRecord.DownloadStatus), flags)?.SetValue(record, status);
        type.GetProperty(nameof(MangaDownloadRecord.StatusUpdateAt), flags)?.SetValue(record, updatedAt);
        type.GetProperty(nameof(MangaDownloadRecord.StatusReason), flags)?.SetValue(record, reason);

        return record;
    }

    [Theory]
    [InlineData(DownloadStatus.InProgress, "bg-info")]
    [InlineData(DownloadStatus.Cancelled, "bg-danger")]
    [InlineData((DownloadStatus)88, "bg-secondary")] // Default case
    public void Invoke_MapsStatusToBadgeClass(DownloadStatus status, string expectedClass)
    {
        // Arrange
        MangaDownloadRecord record = CreateMangaRecord(status, DateTimeOffset.UtcNow, null);
        _ = _mockUserClockManager
            .Setup(x => x.ConvertToUserTime(It.IsAny<DateTimeOffset>()))
            .Returns(DateTimeOffset.Now);

        // Act
        Microsoft.AspNetCore.Mvc.IViewComponentResult result = _component.Invoke(record);

        // Assert
        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        BadgeMangaDownloadStatusViewComponentModel model = Assert.IsType<BadgeMangaDownloadStatusViewComponentModel>(viewResult.ViewData.Model);
        Assert.Equal(expectedClass, model.BadgeClass);
    }

    [Fact]
    public void Invoke_BuildsLegendWithTimeAndReason()
    {
        // Arrange
        DateTimeOffset utcUpdate = new(2026, 1, 25, 14, 0, 0, TimeSpan.Zero);
        DateTimeOffset userLocal = new(2026, 1, 25, 9, 0, 0, TimeSpan.FromHours(-5));
        string reason = "Manga source unavailable";

        MangaDownloadRecord record = CreateMangaRecord(DownloadStatus.ToBeRescheduled, utcUpdate, reason);

        _ = _mockUserClockManager
            .Setup(x => x.ConvertToUserTime(utcUpdate))
            .Returns(userLocal);

        // Act
        Microsoft.AspNetCore.Mvc.IViewComponentResult result = _component.Invoke(record);

        // Assert
        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        BadgeMangaDownloadStatusViewComponentModel model = Assert.IsType<BadgeMangaDownloadStatusViewComponentModel>(viewResult.ViewData.Model);

        // Verify the formatted string components
        string expectedTimePart = userLocal.ToString("g");
        Assert.Contains(expectedTimePart, model.Legend);
        Assert.Contains(reason, model.Legend);
    }

    [Fact]
    public void Invoke_WhenReasonIsEmpty_LegendDoesNotContainExtraLines()
    {
        // Arrange
        DateTimeOffset userLocal = new(2026, 1, 25, 10, 0, 0, TimeSpan.Zero);
        MangaDownloadRecord record = CreateMangaRecord(DownloadStatus.Completed, DateTimeOffset.UtcNow, " "); // Whitespace reason

        _ = _mockUserClockManager
            .Setup(x => x.ConvertToUserTime(It.IsAny<DateTimeOffset>()))
            .Returns(userLocal);

        // Act
        Microsoft.AspNetCore.Mvc.IViewComponentResult result = _component.Invoke(record);

        // Assert
        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        BadgeMangaDownloadStatusViewComponentModel model = Assert.IsType<BadgeMangaDownloadStatusViewComponentModel>(viewResult.ViewData.Model);

        // The legend should only have the "Last Update" line
        // Component uses AppendLine which adds a newline
        string[] lines = model.Legend.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
        _ = Assert.Single(lines);
    }
}

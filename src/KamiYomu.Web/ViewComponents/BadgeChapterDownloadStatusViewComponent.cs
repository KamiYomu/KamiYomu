using System.Text;

using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.ViewComponents;

public class BadgeChapterDownloadStatusViewComponent(IUserClockManager userClockService) : ViewComponent
{
    public IViewComponentResult Invoke(ChapterDownloadRecord chapterDownloadRecord)
    {
        string badgeClass = chapterDownloadRecord.DownloadStatus switch
        {
            Entities.Definitions.DownloadStatus.ToBeRescheduled => "bg-warning",
            Entities.Definitions.DownloadStatus.Scheduled => "bg-primary",
            Entities.Definitions.DownloadStatus.InProgress => "bg-info",
            Entities.Definitions.DownloadStatus.Completed => "bg-success",
            Entities.Definitions.DownloadStatus.Cancelled => "bg-danger",
            _ => "bg-secondary"
        };
        StringBuilder lastUpdateBuilder = new();
        _ = lastUpdateBuilder.AppendLine($"{I18n.LastUpdate}: {userClockService.ConvertToUserTime(chapterDownloadRecord.StatusUpdateAt.GetValueOrDefault()).ToString("g")}.");
        if (!string.IsNullOrWhiteSpace(chapterDownloadRecord.StatusReason))
        {
            _ = lastUpdateBuilder.AppendLine(chapterDownloadRecord.StatusReason);
        }

        return View(new BadgeChapterDownloadStatusViewComponentModel(chapterDownloadRecord, badgeClass, lastUpdateBuilder.ToString()));
    }
}

public record BadgeChapterDownloadStatusViewComponentModel(
    ChapterDownloadRecord ChapterDownloadRecord,
    string BadgeClass,
    string Legend);

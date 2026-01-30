using System.Text;

using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.ViewComponents;

public class BadgeMangaDownloadStatusViewComponent(IUserClockManager userClockService) : ViewComponent
{
    public IViewComponentResult Invoke(MangaDownloadRecord mangaDownloadRecord)
    {
        if (mangaDownloadRecord == null)
        {
            return Content(string.Empty);
        }

        string badgeClass = mangaDownloadRecord.DownloadStatus switch
        {
            Entities.Definitions.DownloadStatus.ToBeRescheduled => "bg-warning",
            Entities.Definitions.DownloadStatus.Scheduled => "bg-primary",
            Entities.Definitions.DownloadStatus.InProgress => "bg-info",
            Entities.Definitions.DownloadStatus.Completed => "bg-success",
            Entities.Definitions.DownloadStatus.Cancelled => "bg-danger",
            _ => "bg-secondary"
        };
        StringBuilder lastUpdateBuilder = new();
        _ = lastUpdateBuilder.AppendLine($"{I18n.LastUpdate}: {userClockService.ConvertToUserTime(mangaDownloadRecord.StatusUpdateAt.GetValueOrDefault()).ToString("g")}.");
        if (!string.IsNullOrWhiteSpace(mangaDownloadRecord.StatusReason))
        {
            _ = lastUpdateBuilder.AppendLine(mangaDownloadRecord.StatusReason);
        }

        return View(new BadgeMangaDownloadStatusViewComponentModel(mangaDownloadRecord, badgeClass, lastUpdateBuilder.ToString()));
    }
}

public record BadgeMangaDownloadStatusViewComponentModel(
    MangaDownloadRecord MangaDownloadRecord,
    string BadgeClass,
    string Legend);

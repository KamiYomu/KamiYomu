using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Integrations;

namespace KamiYomu.Web.Infrastructure.Services.Interfaces;

public interface IGotifyService
{
    Task<bool> TestConnection(GotifySettings settings, CancellationToken cancellationToken);
    Task PushNotificationAsync(string message, CancellationToken cancellationToken);
    Task PushChapterDownloadedNotificationAsync(ChapterDownloadRecord chapterDownload, CancellationToken cancellationToken);
    Task PushSearchForChaptersCompletedNotificationAsync(MangaDownloadRecord mangaDownload, CancellationToken cancellationToken);
}

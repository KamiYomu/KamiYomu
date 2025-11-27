using KamiYomu.Web.Entities;

namespace KamiYomu.Web.Infrastructure.Services.Interfaces
{
    public interface IWorkerService
    {
        string ScheduleMangaDownload(MangaDownloadRecord mangaDownloadRecord);
        void CancelMangaDownload(MangaDownloadRecord mangaDownloadRecord);
    }
}

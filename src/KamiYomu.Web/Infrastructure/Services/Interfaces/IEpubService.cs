using KamiYomu.Web.Models;

namespace KamiYomu.Web.Infrastructure.Services.Interfaces;

public interface IEpubService
{
    DownloadResponse? GetDownloadResponse(Guid libraryId, Guid chapterDownloadId);
}

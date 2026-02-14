using KamiYomu.Web.Models;

namespace KamiYomu.Web.Infrastructure.Services.Interfaces;

public interface IPdfService
{
    DownloadResponse? GetDownloadResponse(Guid libraryId, Guid chapterDownloadId);
}

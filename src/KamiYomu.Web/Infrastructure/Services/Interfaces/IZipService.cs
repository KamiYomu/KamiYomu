using KamiYomu.Web.Models;

namespace KamiYomu.Web.Infrastructure.Services.Interfaces;

public interface IZipService
{
    DownloadResponse? GetDownloadZipResponse(Guid libraryId, Guid chapterDownloadId);
    DownloadResponse? GetDownloadCbzResponse(Guid libraryId, Guid chapterDownloadId);
}

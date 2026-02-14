namespace KamiYomu.Web.Models;

public record DownloadResponse(Stream Content, string FileName, string ContentType)
{
}

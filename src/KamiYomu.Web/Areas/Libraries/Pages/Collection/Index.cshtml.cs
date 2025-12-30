using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.StaticFiles;

namespace KamiYomu.Web.Areas.Libraries.Pages.Collection;

public class IndexModel(
    ILogger<IndexModel> logger,
    IHttpClientFactory httpClientFactory,
    ImageDbContext imageDbContext) : PageModel
{
    public void OnGet()
    {

    }

    public async Task<IActionResult> OnGetImageAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new NoContentResult();
        }

        if (uri == null || !uri.IsAbsoluteUri)
        {
            return BadRequest("Invalid image URI.");
        }

        LiteDB.ILiteStorage<Uri> fs = imageDbContext.CoverImageFileStorage;

        if (!fs.Exists(uri))
        {
            using HttpClient httpClient = httpClientFactory.CreateClient(Defaults.Worker.HttpClientBackground);
            try
            {
                using HttpResponseMessage response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                _ = response.EnsureSuccessStatusCode();

                await using Stream httpStream = await response.Content.ReadAsStreamAsync(cancellationToken);

                _ = fs.Upload(
                    uri,
                    Path.GetFileName(uri.LocalPath),
                    httpStream
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to download image.");
                return StatusCode(500, "Failed to download image.");
            }
        }

        LiteDB.LiteFileStream<Uri> fileStream = fs.OpenRead(uri);
        if (fileStream == null)
        {
            return NotFound("Image not found in cache.");
        }

        FileStreamResult result = File(fileStream, GetContentType(fileStream.FileInfo.Filename));
        Response.Headers["Cache-Control"] = "public,max-age=2592000"; // 30 days
        Response.Headers["Expires"] = DateTimeOffset.UtcNow.AddDays(30).ToString("R"); // RFC1123 format
        return result;

    }

    // Helper to determine MIME type
    private string GetContentType(string fileName)
    {
        FileExtensionContentTypeProvider provider = new();
        if (!provider.TryGetContentType(fileName, out string? contentType))
        {
            contentType = "application/octet-stream"; // Fallback
        }
        return contentType;
    }
}

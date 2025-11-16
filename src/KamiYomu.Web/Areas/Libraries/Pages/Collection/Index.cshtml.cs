using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Contexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.StaticFiles;
namespace KamiYomu.Web.Areas.Libraries.Pages.Mangas;

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
        if(cancellationToken.IsCancellationRequested)
        {
            return new NoContentResult();
        }

        if (uri == null || !uri.IsAbsoluteUri)
        {
            return BadRequest("Invalid image URI.");
        }

        var fs = imageDbContext.CoverImageFileStorage;

        // Check if image is already cached
        if (!fs.Exists(uri))
        {
            using var httpClient = httpClientFactory.CreateClient(Defaults.Worker.HttpClientBackground);
            try
            {
                var imageBytes = await httpClient.GetByteArrayAsync(uri, cancellationToken);
                using var stream = new MemoryStream(imageBytes);

                fs.Upload(uri, Path.GetFileName(uri.LocalPath), stream);
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "Failed to download image.");
                return StatusCode(500, "Failed to download image.");
            }
        }

        // Retrieve image stream from LiteDB
        var fileStream = fs.OpenRead(uri);
        if (fileStream == null)
        {
            return NotFound("Image not found in cache.");
        }

        var result = File(fileStream, GetContentType(fileStream.FileInfo.Filename));
        Response.Headers["Cache-Control"] = "public,max-age=2592000"; // 30 days
        Response.Headers["Expires"] = DateTime.UtcNow.AddDays(30).ToString("R"); // RFC1123 format
        return result;

    }

    // Helper to determine MIME type
    private string GetContentType(string fileName)
    {
        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(fileName, out var contentType))
        {
            contentType = "application/octet-stream"; // Fallback
        }
        return contentType;
    }
}

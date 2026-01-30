using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;

using LiteDB;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.StaticFiles;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Areas.Libraries.Pages.Collection;

public class IndexModel(
    ILogger<IndexModel> logger,
    [FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext,
    IHttpClientFactory httpClientFactory,
    ImageDbContext imageDbContext,
    ICrawlerAgentRepository agentCrawlerRepository) : PageModel
{

    [BindProperty(SupportsGet = true)]
    public string Query { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public Guid? SelectedAgent { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Offset { get; set; } = 0;

    [BindProperty(SupportsGet = true)]
    public int Limit { get; set; } = 30;

    [BindProperty(SupportsGet = true)]
    public string? ContinuationToken { get; set; } = string.Empty;

    public IEnumerable<Library> Results { get; set; } = [];

    public void OnGet()
    {
        UserPreference userPreference = dbContext.UserPreferences.Query().FirstOrDefault();
        Results = [.. dbContext.Libraries.Include(p => p.CrawlerAgent)
                                         .Find(p => (Query == string.Empty || p.Manga.Title.Contains(Query))
                                           && (p.Manga.IsFamilySafe || p.Manga.IsFamilySafe == userPreference.FamilySafeMode))
                                         .Skip(Offset)
                                         .Take(Limit)];
        ViewData["ShowAddToLibrary"] = false;
        ViewData["Handler"] = "Search";
        ViewData[nameof(Query)] = Query;
        ViewData[nameof(PaginationOptions.OffSet)] = Offset + Limit;
        ViewData[nameof(PaginationOptions.Limit)] = Limit;
        ViewData[nameof(PaginationOptions.ContinuationToken)] = string.Empty;
    }

    public IActionResult OnGetSearch()
    {
        UserPreference userPreference = dbContext.UserPreferences.Query().FirstOrDefault();
        Results = [.. dbContext.Libraries.Include(p => p.CrawlerAgent)
                                         .Find(p => (Query == string.Empty || p.Manga.Title.Contains(Query))
                                           && (p.Manga.IsFamilySafe || p.Manga.IsFamilySafe == userPreference.FamilySafeMode))
                                         .Skip(Offset)
                                         .Take(Limit)];
        ViewData["ShowAddToLibrary"] = false;
        ViewData["Handler"] = "Search";
        ViewData[nameof(Query)] = Query;
        ViewData[nameof(PaginationOptions.OffSet)] = Offset + Limit;
        ViewData[nameof(PaginationOptions.Limit)] = Limit;
        ViewData[nameof(PaginationOptions.ContinuationToken)] = string.Empty;

        if (Request.Headers.ContainsKey("HX-Request"))
        {
            return ViewComponent("SearchMangaResult", new
            {
                libraries = Results,
                searchUri = Url.Page("/Collection/Index", new
                {
                    Area = "Libraries",
                    Handler = "Search",
                    Query = ViewData[nameof(Query)],
                    SelectedAgent = ViewData[nameof(SelectedAgent)],
                    OffSet = ViewData[nameof(Offset)],
                    Limit = ViewData[nameof(Limit)],
                    ContinuationToken = ViewData[nameof(ContinuationToken)]
                })
            });
        }
        return Page();
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

        ILiteStorage<Uri> fs = imageDbContext.CoverImageFileStorage;

        if (!fs.Exists(uri))
        {
            using HttpClient httpClient = httpClientFactory.CreateClient(Defaults.Worker.HttpClientApp);
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

        LiteFileStream<Uri> fileStream = fs.OpenRead(uri);
        if (fileStream == null)
        {
            return NotFound("Image not found in cache.");
        }

        FileStreamResult result = File(fileStream, GetContentType(fileStream.FileInfo.Filename));
        Response.Headers.CacheControl = "public,max-age=2592000"; // 30 days
        Response.Headers.Expires = DateTimeOffset.UtcNow.AddDays(30).ToString("R"); // RFC1123 format
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

using KamiYomu.Web.AppOptions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Areas.Settings.Pages.AuditTrail;

public class IndexModel(ILogger<IndexModel> logger, IOptions<SpecialFolderOptions> specialFolderOptions) : PageModel
{
    public void OnGet()
    {

    }

    public async Task<IActionResult> OnGetLogStreamAsync()
    {
        Response.Headers["Content-Type"] = "text/event-stream";

        string logFolder = specialFolderOptions.Value.LogDir;

        // Track read positions per file
        Dictionary<string, long> filePositions = [];

        while (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            // Get all log files sorted by creation time
            List<FileInfo> files = [.. Directory
                .GetFiles(logFolder, "log-*.txt")
                .Select(f => new FileInfo(f))
                .Where(p => p.CreationTimeUtc.Date == DateTime.Today)
                .OrderBy(f => f.CreationTimeUtc)];

            foreach (FileInfo? file in files)
            {
                if (!filePositions.TryGetValue(file.FullName, out long lastPos))
                {
                    lastPos = 0;
                }

                if (file.Length > lastPos)
                {
                    using FileStream stream = new(
                        file.FullName,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite
                    );

                    _ = stream.Seek(lastPos, SeekOrigin.Begin);

                    using StreamReader reader = new(stream);
                    string? line;

                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        await Response.WriteAsync($"data: {line}\n\n");
                        await Response.Body.FlushAsync();
                    }

                    filePositions[file.FullName] = file.Length;
                }
            }

            await Task.Delay(500);
        }

        return new EmptyResult();
    }

}

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

        long lastSize = 0;
        string? currentFile = null;

        while (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            // Get the latest log file (by date in the filename)
            string? latestFile = Directory
                .GetFiles(logFolder, "log-*.txt")
                .OrderByDescending(f => f)
                .FirstOrDefault();

            if (latestFile == null)
            {
                await Task.Delay(1000);
                continue;
            }

            if (currentFile != latestFile)
            {
                currentFile = latestFile;
                lastSize = 0; // reset read position for new file
            }

            FileInfo info = new(currentFile);
            if (info.Length > lastSize)
            {
                using FileStream stream = new(
                    currentFile,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite
                );

                _ = stream.Seek(lastSize, SeekOrigin.Begin);

                using StreamReader reader = new(stream);
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    await Response.WriteAsync($"data: {line}\n\n");
                    await Response.Body.FlushAsync();
                }

                lastSize = info.Length;
            }

            await Task.Delay(500);
        }

        return new EmptyResult();
    }
}

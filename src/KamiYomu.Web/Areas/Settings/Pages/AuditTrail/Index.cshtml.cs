using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Settings.Pages.AuditTrail
{
    public class IndexModel(ILogger<IndexModel> logger) : PageModel
    {
        public void OnGet()
        {

        }

        public async Task<IActionResult> OnGetLogStreamAsync()
        {

            Response.Headers["Content-Type"] = "text/event-stream";

            string logFolder = Web.Settings.SpecialFolders.LogDir;

            long lastSize = 0;
            string? currentFile = null;

            while (!HttpContext.RequestAborted.IsCancellationRequested)
            {
                // Get the latest log file (by date in the filename)
                var latestFile = Directory
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

                var info = new FileInfo(currentFile);
                if (info.Length > lastSize)
                {
                    using var stream = new FileStream(
                        currentFile,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite
                    );

                    stream.Seek(lastSize, SeekOrigin.Begin);

                    using var reader = new StreamReader(stream);
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
}

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Areas.Settings.Pages.AuditTrail;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Tests.Areas.Settings.Pages.AuditTrail;

public class IndexModelTests : IDisposable
{
    private readonly string _logDir = Path.Combine(Path.GetTempPath(), "KamiYomu.Tests.AuditTrail", Guid.NewGuid().ToString("N"));
    private readonly Mock<ILogger<IndexModel>> _logger = new();

    public IndexModelTests()
    {
        _ = Directory.CreateDirectory(_logDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_logDir))
            {
                Directory.Delete(_logDir, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    private IndexModel CreateModel(CancellationToken requestAborted)
    {
        IOptions<SpecialFolderOptions> options = Options.Create(new SpecialFolderOptions
        {
            LogDir = _logDir
        });

        return new IndexModel(_logger.Object, options)
        {
            PageContext = ServiceTestHelpers.CreatePageContext(httpContext => httpContext.RequestAborted = requestAborted)
        };
    }

    [Fact]
    public void OnGet_DoesNotThrow()
    {
        IndexModel model = CreateModel(CancellationToken.None);

        model.OnGet();
    }

    [Fact]
    public async Task OnGetLogStreamAsync_WhenRequestAlreadyAborted_ReturnsEmptyResultWithoutHanging()
    {
        // The endpoint is an infinite SSE polling loop (`while (!RequestAborted.IsCancellationRequested)`
        // with `await Task.Delay(500)` each iteration) intended to tail today's log files forever until
        // the client disconnects. Pre-cancelling RequestAborted makes the loop condition false on the
        // very first check, giving a fast smoke test of the method's shape (headers + EmptyResult)
        // without exercising the actual tailing logic - doing so would require real log files and
        // waiting through real Task.Delay calls, which is disproportionate for a background log-tail
        // endpoint per the testability-only scope of this effort.
        using CancellationTokenSource cts = new();
        cts.Cancel();

        IndexModel model = CreateModel(cts.Token);

        IActionResult result = await model.OnGetLogStreamAsync();

        _ = Assert.IsType<EmptyResult>(result);
        Assert.Equal("text/event-stream", model.Response.Headers["Content-Type"]);
    }
}

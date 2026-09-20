using System.Text;

using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services;
using KamiYomu.Web.Models;

using Microsoft.AspNetCore.Hosting;

namespace KamiYomu.Web.Tests.Infrastructure.Services;

public class PdfServiceTests
{
    [Fact]
    public void GetDownloadResponse_ReturnsNull_WhenLibraryDoesNotExist()
    {
        using DbContext dbContext = new(":memory:");
        Mock<IWebHostEnvironment> environment = new();
        _ = environment.SetupGet(x => x.ContentRootPath).Returns(GetWebProjectPath());

        PdfService service = new(dbContext, environment.Object);

        DownloadResponse? result = service.GetDownloadResponse(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public void GetDownloadResponse_ReturnsPdfStream_WhenCompletedCbzExists()
    {
        ServiceTestHelpers.EnsureServiceLocatorConfigured();

        using DbContext dbContext = new(":memory:");
        StoredLibraryRecord stored = ServiceTestHelpers.CreateCompletedStoredChapter(dbContext);
        _ = ServiceTestHelpers.CreateCbzFile(stored.Library, stored.Chapter, "001.png");

        try
        {
            Mock<IWebHostEnvironment> environment = new();
            _ = environment.SetupGet(x => x.ContentRootPath).Returns(GetWebProjectPath());

            PdfService service = new(dbContext, environment.Object);

            DownloadResponse? result = service.GetDownloadResponse(stored.Library.Id, stored.ChapterDownload.Id);

            Assert.NotNull(result);
            Assert.Equal("application/pdf", result.ContentType);
            Assert.True(result.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));

            using MemoryStream copy = new();
            result.Content.CopyTo(copy);
            string header = Encoding.ASCII.GetString(copy.ToArray(), 0, 4);
            Assert.Equal("%PDF", header);
            result.Content.Dispose();
        }
        finally
        {
            ServiceTestHelpers.CleanupLibraryArtifacts(stored.Library, stored.Chapter);
        }
    }

    private static string GetWebProjectPath()
    {
        DirectoryInfo current = new(AppContext.BaseDirectory);

        while (current.Parent != null)
        {
            string candidate = Path.Combine(current.FullName, "KamiYomu.Web", "KamiYomu.Web.csproj");
            if (File.Exists(candidate))
            {
                return Path.GetDirectoryName(candidate)!;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate KamiYomu.Web project directory.");
    }
}

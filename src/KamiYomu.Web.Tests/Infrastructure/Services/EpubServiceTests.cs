using System.IO.Compression;

using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services;
using KamiYomu.Web.Models;

namespace KamiYomu.Web.Tests.Infrastructure.Services;

public class EpubServiceTests
{
    [Fact]
    public void GetDownloadResponse_ReturnsNull_WhenLibraryDoesNotExist()
    {
        using DbContext dbContext = new(":memory:");
        EpubService service = new(dbContext);

        DownloadResponse? result = service.GetDownloadResponse(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public void GetDownloadResponse_ReturnsEpubArchive_WhenCompletedCbzExists()
    {
        using DbContext dbContext = new(":memory:");
        StoredLibraryRecord stored = ServiceTestHelpers.CreateCompletedStoredChapter(dbContext);
        _ = ServiceTestHelpers.CreateCbzFile(stored.Library, stored.Chapter, "001.png", "002.jpg");

        try
        {
            EpubService service = new(dbContext);

            DownloadResponse? result = service.GetDownloadResponse(stored.Library.Id, stored.ChapterDownload.Id);

            Assert.NotNull(result);
            Assert.Equal("application/epub+zip", result.ContentType);
            Assert.True(result.FileName.EndsWith(".epub", StringComparison.OrdinalIgnoreCase));

            using ZipArchive archive = new(result.Content, ZipArchiveMode.Read, leaveOpen: false);
            Assert.NotNull(archive.GetEntry("mimetype"));
            Assert.NotNull(archive.GetEntry("META-INF/container.xml"));
            Assert.NotNull(archive.GetEntry("OEBPS/content.opf"));
            Assert.NotNull(archive.GetEntry("OEBPS/toc.ncx"));
            Assert.NotNull(archive.GetEntry("OEBPS/images/page_0.png"));
            Assert.NotNull(archive.GetEntry("OEBPS/images/page_1.jpg"));
            Assert.NotNull(archive.GetEntry("OEBPS/text/page_0.xhtml"));
            Assert.NotNull(archive.GetEntry("OEBPS/text/page_1.xhtml"));
        }
        finally
        {
            ServiceTestHelpers.CleanupLibraryArtifacts(stored.Library, stored.Chapter);
        }
    }
}

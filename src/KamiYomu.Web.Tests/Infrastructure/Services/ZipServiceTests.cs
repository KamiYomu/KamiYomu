using System.IO.Compression;

using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services;
using KamiYomu.Web.Models;

namespace KamiYomu.Web.Tests.Infrastructure.Services;

public class ZipServiceTests
{
    [Fact]
    public void GetDownloadCbzResponse_ReturnsNull_WhenLibraryDoesNotExist()
    {
        using DbContext dbContext = new(":memory:");
        ZipService service = new(dbContext);

        DownloadResponse? result = service.GetDownloadCbzResponse(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public void GetDownloadCbzResponse_ReturnsFileStream_WhenCompletedCbzExists()
    {
        using DbContext dbContext = new(":memory:");
        StoredLibraryRecord stored = ServiceTestHelpers.CreateCompletedStoredChapter(dbContext);
        string cbzPath = ServiceTestHelpers.CreateCbzFile(stored.Library, stored.Chapter, "001.png");

        try
        {
            ZipService service = new(dbContext);

            DownloadResponse? result = service.GetDownloadCbzResponse(stored.Library.Id, stored.ChapterDownload.Id);

            Assert.NotNull(result);
            Assert.Equal("application/x-cbz", result.ContentType);
            Assert.Equal(Path.GetFileName(cbzPath), result.FileName);

            using ZipArchive archive = new(result.Content, ZipArchiveMode.Read, leaveOpen: false);
            _ = Assert.Single(archive.Entries);
        }
        finally
        {
            ServiceTestHelpers.CleanupLibraryArtifacts(stored.Library, stored.Chapter);
        }
    }

    [Fact]
    public void GetDownloadZipResponse_ReturnsZipResponse_WhenCompletedCbzExists()
    {
        using DbContext dbContext = new(":memory:");
        StoredLibraryRecord stored = ServiceTestHelpers.CreateCompletedStoredChapter(dbContext);
        string cbzPath = ServiceTestHelpers.CreateCbzFile(stored.Library, stored.Chapter, "001.png");

        try
        {
            ZipService service = new(dbContext);

            DownloadResponse? result = service.GetDownloadZipResponse(stored.Library.Id, stored.ChapterDownload.Id);

            Assert.NotNull(result);
            Assert.Equal("application/zip", result.ContentType);
            Assert.Equal(Path.GetFileNameWithoutExtension(cbzPath) + ".zip", result.FileName);
            Assert.True(result.Content.CanRead);
        }
        finally
        {
            ServiceTestHelpers.CleanupLibraryArtifacts(stored.Library, stored.Chapter);
        }
    }
}

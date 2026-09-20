using System.Reflection;

using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.CrawlerAgents.Core.Catalog.Builders;
using KamiYomu.Web.Areas.Public.Models;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Extensions;

namespace KamiYomu.Web.Tests.Areas.Public.Models;

public class OpdsEntryTests
{
    [Fact]
    public void Create_FromLibrary_MapsMangaMetadataAndLinks()
    {
        Library library = CreateLibrary("One Piece", ["action", "adventure"], ["Oda"]);

        OpdsEntry entry = OpdsEntry.Create(library);

        Assert.Equal($"urn:opds:manga:{library.Id}", entry.Id);
        Assert.Equal("One Piece", entry.Title);
        Assert.Equal(library.Manga!.Description, entry.Summary);
        Assert.Equal(2, entry.Categories.Count);
        Assert.Contains(entry.Categories, c => c.Term == "action");
        Assert.Equal("Oda", entry.Publisher);
        Assert.Equal("ja", entry.Language);

        OpdsLink alternate = Assert.Single(entry.Links, l => l.Rel == "alternate");
        Assert.Equal($"/public/api/v1/opds/{library.Id}", alternate.Href);

        Assert.Contains(entry.Links, l => l.Rel == "http://opds-spec.org/image");
        Assert.Contains(entry.Links, l => l.Rel == "http://opds-spec.org/image/thumbnail");
    }

    [Fact]
    public void Create_FromLibraryAndChapter_BuildsAcquisitionLinksForAllFormats()
    {
        Library library = CreateLibrary("Bleach", ["shonen"], ["Kubo"]);
        ChapterDownloadRecord record = CreateCompletedChapterRecord(library, 4, "Chapter 4");

        OpdsEntry entry = OpdsEntry.Create(library, record);

        Assert.Equal($"urn:opds:manga:{library.Id}:chapters:{record.Id}", entry.Id);

        List<string> acquisitionTypes = entry.Links
            .Where(l => l.Rel == "http://opds-spec.org/acquisition")
            .Select(l => l.Type)
            .ToList();

        Assert.Contains("application/vnd.comicbook+zip", acquisitionTypes);
        Assert.Contains("application/epub+zip", acquisitionTypes);
        Assert.Contains(System.Net.Mime.MediaTypeNames.Application.Zip, acquisitionTypes);
        Assert.Contains(System.Net.Mime.MediaTypeNames.Application.Pdf, acquisitionTypes);
    }

    [Fact]
    public void CreateChapterEntries_MapsEachChapterDownloadRecord()
    {
        Library library = CreateLibrary("Naruto", ["action"], ["Kishimoto"]);
        ChapterDownloadRecord first = CreateCompletedChapterRecord(library, 1, "Chapter 1");
        ChapterDownloadRecord second = CreateCompletedChapterRecord(library, 2, "Chapter 2");

        List<OpdsEntry> entries = OpdsEntry.CreateChapterEntries(library, [first, second]);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Id == $"urn:opds:manga:{library.Id}:chapters:{first.Id}");
        Assert.Contains(entries, e => e.Id == $"urn:opds:manga:{library.Id}:chapters:{second.Id}");
    }

    private static Library CreateLibrary(string title, string[] tags, string[] authors)
    {
        Manga manga = MangaBuilder.Create()
            .WithTitle(title)
            .WithDescription("Description of " + title)
            .WithTags(tags)
            .WithAuthors(authors)
            .WithOriginalLanguage("ja")
            .WithCoverUrl(new Uri("https://example.com/cover.png"))
            .WithIsFamilySafe(true)
            .Build();

        CrawlerAgent crawlerAgent = new("Test.Agent.dll", "Test Agent", new Dictionary<string, object>());
        SetId(crawlerAgent, Guid.NewGuid());

        Library library = new(
            crawlerAgent,
            manga,
            "{manga_title}/chapter-{chapter_padded_4}",
            "{manga_title} ch.{chapter_padded_4}",
            "{manga_title}");
        SetId(library, Guid.NewGuid());

        return library;
    }

    private static ChapterDownloadRecord CreateCompletedChapterRecord(Library library, decimal number, string title)
    {
        Chapter chapter = ChapterBuilder.Create()
            .WithNumber(number)
            .WithTitle(title)
            .WithUri(new Uri($"https://example.com/chapters/{number}"))
            .WithParentManga(library.Manga)
            .Build();

        MangaDownloadRecord mangaDownload = new(library, "manga-job");
        SetId(mangaDownload, Guid.NewGuid());

        ChapterDownloadRecord record = new(library.CrawlerAgent, mangaDownload, chapter);
        SetId(record, Guid.NewGuid());
        record.Complete();

        return record;
    }

    private static void SetId(object target, Guid id)
    {
        PropertyInfo property = target.GetType().GetProperty("Id")!;
        _ = property.GetSetMethod(true)!.Invoke(target, [id]);
    }
}

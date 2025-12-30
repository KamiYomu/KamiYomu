using KamiYomu.CrawlerAgents.Core.Catalog.Builders;
using KamiYomu.Web.Infrastructure.Services;

namespace KamiYomu.Web.Tests.Services;

public class TemplateResolverTest
{
    [Fact]
    public void GetMangaVariables_ReturnsEmpty_WhenNull()
    {
        Dictionary<string, string> result = TemplateResolver.GetMangaVariables(null);

        Assert.Equal("", result["manga_title"]);
        Assert.Equal("", result["manga_title_slug"]);
        Assert.Equal("", result["manga_familysafe"]);
    }

    [Fact]
    public void GetMangaVariables_ReturnsCorrectValues()
    {
        CrawlerAgents.Core.Catalog.Manga manga = MangaBuilder.Create()
            .WithTitle("One Piece")
            .WithIsFamilySafe(true)
            .Build();

        Dictionary<string, string> result = TemplateResolver.GetMangaVariables(manga);

        Assert.Equal("One Piece", result["manga_title"]);
        Assert.Equal("one-piece", result["manga_title_slug"]);
        Assert.Equal("True", result["manga_familysafe"]);
    }

    [Fact]
    public void GetChapterVariables_ReturnsEmpty_WhenNull()
    {
        Dictionary<string, string> result = TemplateResolver.GetChapterVariables(null);

        Assert.Equal("", result["chapter"]);
        Assert.Equal("", result["chapter"]);
        Assert.Equal("", result["chapter_title"]);
        Assert.Equal("", result["chapter_title_slug"]);
        Assert.Equal("", result["volume"]);
    }

    [Fact]
    public void GetChapterVariables_ReturnsCorrectValues()
    {
        CrawlerAgents.Core.Catalog.Chapter chapter = ChapterBuilder.Create()
            .WithNumber(12)
            .WithTitle("Romance Dawn")
            .WithVolume(3)
            .Build();

        Dictionary<string, string> result = TemplateResolver.GetChapterVariables(chapter);

        Assert.Equal("0012", result["chapter_padded_4"]);
        Assert.Equal("12", result["chapter"]);
        Assert.Equal("Romance Dawn", result["chapter_title"]);
        Assert.Equal("romance-dawn", result["chapter_title_slug"]);
        Assert.Equal("3", result["volume"]);
    }

    [Fact]
    public void Resolve_ReturnsEmpty_WhenTemplateIsNull()
    {
        string result = TemplateResolver.Resolve(null, null, null, null);
        Assert.Equal("", result);
    }

    [Fact]
    public void Resolve_ReplacesAllVariablesCorrectly()
    {
        CrawlerAgents.Core.Catalog.Manga manga = MangaBuilder.Create()
            .WithTitle("One Piece")
            .WithIsFamilySafe(true)
            .Build();

        CrawlerAgents.Core.Catalog.Chapter chapter = ChapterBuilder.Create()
            .WithNumber(1)
            .WithTitle("Romance Dawn")
            .WithVolume(1)
            .Build();

        string template = "{manga_title}/ch.{chapter_padded_4}/{chapter_title_slug}";

        string result = TemplateResolver.Resolve(template, manga, chapter);

        Assert.Equal("One Piece/ch.0001/romance-dawn", result);
    }


    [Fact]
    public void Slugify_IsAppliedCorrectly()
    {
        CrawlerAgents.Core.Catalog.Manga manga = MangaBuilder.Create()
            .WithTitle("My Manga_Title Test")
            .WithIsFamilySafe(false)
            .Build();

        Dictionary<string, string> result = TemplateResolver.GetMangaVariables(manga);

        Assert.Equal("my-manga-title-test", result["manga_title_slug"]);
    }

}

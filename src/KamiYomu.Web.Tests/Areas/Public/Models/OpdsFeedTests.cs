using KamiYomu.Web.Areas.Public.Models;

namespace KamiYomu.Web.Tests.Areas.Public.Models;

public class OpdsFeedTests
{
    [Fact]
    public void EntriesAndLinks_DefaultToEmptyLists()
    {
        OpdsFeed feed = new();

        Assert.NotNull(feed.Entries);
        Assert.Empty(feed.Entries);
        Assert.NotNull(feed.Links);
        Assert.Empty(feed.Links);
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        DateTime updated = new(2024, 1, 1);
        OpdsEntry entry = new() { Id = "urn:1" };
        OpdsLink link = new() { Href = "/x", Rel = "self", Type = "application/atom+xml" };

        OpdsFeed feed = new()
        {
            Id = "urn:opds:feed",
            Title = "My Feed",
            Icon = "/icon.png",
            Updated = updated,
            Entries = [entry],
            Links = [link]
        };

        Assert.Equal("urn:opds:feed", feed.Id);
        Assert.Equal("My Feed", feed.Title);
        Assert.Equal("/icon.png", feed.Icon);
        Assert.Equal(updated, feed.Updated);
        _ = Assert.Single(feed.Entries);
        _ = Assert.Single(feed.Links);
    }
}

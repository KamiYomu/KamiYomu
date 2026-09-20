using KamiYomu.Web.Areas.Settings.Models;

namespace KamiYomu.Web.Tests.Areas.Settings.Models;

public class NugetSourceTests
{
    [Fact]
    public void Constructor_SetsProvidedValues()
    {
        Uri url = new("https://nuget.example.com/v3/index.json");

        NugetSource source = new("My Source", url, "user", "pass");

        Assert.Equal("My Source", source.DisplayName);
        Assert.Equal(url, source.Url);
        Assert.Equal("user", source.UserName);
        Assert.Equal("pass", source.Password);
    }

    [Fact]
    public void Constructor_DoesNotAssignId()
    {
        // The Id property has no default assignment in the constructor, so it
        // stays at the default Guid value until something else (e.g. EF Core) sets it.
        NugetSource source = new("My Source", new Uri("https://nuget.example.com"), null, null);

        Assert.Equal(Guid.Empty, source.Id);
    }

    [Fact]
    public void Constructor_AllowsNullUserNameAndPassword()
    {
        NugetSource source = new("My Source", new Uri("https://nuget.example.com"), null, null);

        Assert.Null(source.UserName);
        Assert.Null(source.Password);
    }

    [Fact]
    public void Update_ReplacesAllMutableFields()
    {
        NugetSource source = new("Old Name", new Uri("https://old.example.com"), "olduser", "oldpass");
        Uri newUri = new("https://new.example.com");

        source.Update("New Name", newUri, "newuser", "newpass");

        Assert.Equal("New Name", source.DisplayName);
        Assert.Equal(newUri, source.Url);
        Assert.Equal("newuser", source.UserName);
        Assert.Equal("newpass", source.Password);
    }
}

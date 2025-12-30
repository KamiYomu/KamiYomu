using KamiYomu.Web.Entities;

namespace KamiYomu.Web.Tests.Entities;

public class CrawlerAgentTests
{
    [Theory]
    [InlineData("kamiyomu.agent.1.0.0.nupkg", "kamiyomu.agent")]
    [InlineData("crawler.weebcentral.2.5.9.nupkg", "crawler.weebcentral")]
    [InlineData("kamiyomu.crawleragents.weebcentral.1.0.0-rc1.nupkg", "kamiyomu.crawleragents.weebcentral")]
    [InlineData("my.agent.3.2.1-beta.nupkg", "my.agent")]
    [InlineData("kamiyomu.agent.1.0.0.dll", "kamiyomu.agent")]
    [InlineData("crawler.weebcentral.2.5.9.dll", "crawler.weebcentral")]
    [InlineData("my.agent.3.2.1-beta.dll", "my.agent")]
    [InlineData("my.agent.4.0.0-alpha.2.dll", "my.agent")]
    [InlineData("kamiyomu.agent.dll", "kamiyomu.agent")]
    [InlineData("simplefile.dll", "simplefile")]
    [InlineData("a.b.dll", "a.b")]
    [InlineData("justone.dll", "justone")]
    [InlineData("kamiyomu.agent.one.two.three.dll", "kamiyomu.agent.one.two.three")]
    [InlineData("kamiyomu.agent.1.two.3.dll", "kamiyomu.agent.1.two.3")]
    [InlineData("kamiyomu.agent.1.0.x.dll", "kamiyomu.agent.1.0.x")]
    [InlineData("agent.service.9.9.9-preview.dll", "agent.service")]
    [InlineData("a.b.c.d.e.10.20.30.dll", "a.b.c.d.e")]
    [InlineData("a.b.c.10.20.30-rc5.dll", "a.b.c")]
    public void GetAgentDirName_ShouldReturnExpected(string fileName, string expected)
    {
        // Act
        string result = CrawlerAgent.GetAgentDirName(fileName);

        // Assert
        Assert.Equal(expected, result);
    }
}


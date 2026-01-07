using KamiYomu.Web.Entities;

namespace KamiYomu.Web.Tests.Entities;

public class CrawlerAgentTests
{
    [Theory]
    [InlineData("kamiyomu.agent.1.0.0.nupkg", "kamiyomu.agent.1.0.0")]
    [InlineData("kamiyomu.agent.nupkg", "kamiyomu.agent")]
    [InlineData("kamiyomu.agent.dll", "kamiyomu.agent")]
    [InlineData("kamiyomu.agent.1.0.0.dll", "kamiyomu.agent.1.0.0")]
    public void GetAgentDirName_ShouldReturnExpected(string fileName, string expected)
    {
        // Act
        string result = CrawlerAgent.GetAgentDirName(fileName);

        // Assert
        Assert.Equal(expected, result);
    }
}


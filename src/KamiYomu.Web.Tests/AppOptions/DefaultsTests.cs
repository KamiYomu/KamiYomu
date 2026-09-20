using KamiYomu.CrawlerAgents.Core.Catalog.Definitions;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Models.Definitions;

using LiteDB;

namespace KamiYomu.Web.Tests.AppOptions;

public class DefaultsTests
{
    [Fact]
    public void LiteDbConfigConfigure_RoundTripsRelativeUri()
    {
        // Arrange
        Defaults.LiteDbConfig.Configure();
        Uri uri = new("manga/one-piece", UriKind.Relative);

        // Act
        BsonValue bson = BsonMapper.Global.Serialize(uri);
        Uri? result = BsonMapper.Global.Deserialize<Uri>(bson);

        // Assert
        Assert.Equal("manga/one-piece", bson.AsString);
        Assert.NotNull(result);
        Assert.False(result.IsAbsoluteUri);
        Assert.Equal("manga/one-piece", result.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://[invalid")]
    public void LiteDbConfigConfigure_ReturnsNull_ForBlankOrInvalidUriValues(string value)
    {
        // Arrange
        Defaults.LiteDbConfig.Configure();

        // Act
        Uri? result = BsonMapper.Global.Deserialize<Uri>(new BsonValue(value));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void LiteDbConfigConfigure_RoundTripsRegisteredEnums_AsIntegerValues()
    {
        // Arrange
        Defaults.LiteDbConfig.Configure();

        // Act
        BsonValue downloadStatusBson = BsonMapper.Global.Serialize(DownloadStatus.Cancelled);
        DownloadStatus downloadStatus = BsonMapper.Global.Deserialize<DownloadStatus>(downloadStatusBson);

        BsonValue notificationTypeBson = BsonMapper.Global.Serialize(NotificationType.Warning);
        NotificationType notificationType = BsonMapper.Global.Deserialize<NotificationType>(notificationTypeBson);

        BsonValue releaseStatusBson = BsonMapper.Global.Serialize(ReleaseStatus.OnHiatus);
        ReleaseStatus releaseStatus = BsonMapper.Global.Deserialize<ReleaseStatus>(releaseStatusBson);

        // Assert
        Assert.Equal((int)DownloadStatus.Cancelled, downloadStatusBson.AsInt32);
        Assert.Equal(DownloadStatus.Cancelled, downloadStatus);

        Assert.Equal((int)NotificationType.Warning, notificationTypeBson.AsInt32);
        Assert.Equal(NotificationType.Warning, notificationType);

        Assert.Equal((int)ReleaseStatus.OnHiatus, releaseStatusBson.AsInt32);
        Assert.Equal(ReleaseStatus.OnHiatus, releaseStatus);
    }
}

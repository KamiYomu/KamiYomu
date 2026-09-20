using KamiYomu.Web.Areas.Public.Models;

namespace KamiYomu.Web.Tests.Areas.Public.Models;

public class PublicApiErrorResponseTests
{
    [Fact]
    public void Timestamp_DefaultsToNow()
    {
        DateTime before = DateTime.Now.AddSeconds(-1);

        PublicApiErrorResponse response = new() { Error = "bad_request" };

        DateTime after = DateTime.Now.AddSeconds(1);

        Assert.Equal("bad_request", response.Error);
        Assert.Null(response.Message);
        Assert.Null(response.TraceId);
        Assert.InRange(response.Timestamp, before, after);
    }

    [Fact]
    public void Properties_CanBeSetViaInit()
    {
        DateTime timestamp = new(2024, 5, 1);

        PublicApiErrorResponse response = new()
        {
            Error = "not_found",
            Message = "Resource missing",
            TraceId = "trace-123",
            Timestamp = timestamp
        };

        Assert.Equal("not_found", response.Error);
        Assert.Equal("Resource missing", response.Message);
        Assert.Equal("trace-123", response.TraceId);
        Assert.Equal(timestamp, response.Timestamp);
    }
}

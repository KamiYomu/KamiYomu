using KamiYomu.Web.Infrastructure.Services;

using Microsoft.AspNetCore.Http;

namespace KamiYomu.Web.Tests.Infrastructure.Services;

public class UserClockManagerTests
{
    [Fact]
    public void GetTimeZone_ReturnsUtc_WhenCookieIsMissing()
    {
        DefaultHttpContext httpContext = new();
        Mock<IHttpContextAccessor> accessor = new();
        _ = accessor.SetupGet(x => x.HttpContext).Returns(httpContext);

        UserClockManager manager = new(accessor.Object);

        TimeZoneInfo result = manager.GetTimeZone();

        Assert.Equal(TimeZoneInfo.Utc, result);
    }

    [Fact]
    public void ConvertToUserTime_UsesTimezoneFromCookie()
    {
        TimeZoneInfo targetTimeZone = TimeZoneInfo.GetSystemTimeZones().First(z => z.BaseUtcOffset != TimeSpan.Zero);
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers.Cookie = $"UserTimeZone={Uri.EscapeDataString(targetTimeZone.Id)}";

        Mock<IHttpContextAccessor> accessor = new();
        _ = accessor.SetupGet(x => x.HttpContext).Returns(httpContext);

        UserClockManager manager = new(accessor.Object);
        DateTimeOffset utc = new(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);

        DateTimeOffset result = manager.ConvertToUserTime(utc);

        Assert.Equal(TimeZoneInfo.ConvertTime(utc, targetTimeZone), result);
    }

    [Fact]
    public void ConvertToUtc_UsesTimezoneFromCookie()
    {
        TimeZoneInfo targetTimeZone = TimeZoneInfo.GetSystemTimeZones().First(z => z.BaseUtcOffset != TimeSpan.Zero);
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers.Cookie = $"UserTimeZone={Uri.EscapeDataString(targetTimeZone.Id)}";

        Mock<IHttpContextAccessor> accessor = new();
        _ = accessor.SetupGet(x => x.HttpContext).Returns(httpContext);

        UserClockManager manager = new(accessor.Object);
        DateTimeOffset local = new(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);

        DateTimeOffset result = manager.ConvertToUtc(local);
        DateTimeOffset expected = TimeZoneInfo.ConvertTime(local, targetTimeZone).ToUniversalTime();

        Assert.Equal(expected, result);
    }
}

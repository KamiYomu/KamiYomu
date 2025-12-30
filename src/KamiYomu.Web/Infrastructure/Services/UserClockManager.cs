using KamiYomu.Web.Infrastructure.Services.Interfaces;

namespace KamiYomu.Web.Infrastructure.Services;

public class UserClockManager : IUserClockManager
{
    private readonly IHttpContextAccessor _contextAccessor;

    public UserClockManager(IHttpContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    public TimeZoneInfo GetTimeZone()
    {
        string? tzId = _contextAccessor.HttpContext?.Request.Cookies["UserTimeZone"];
        return string.IsNullOrEmpty(tzId) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(tzId);
    }

    public DateTimeOffset ConvertToUserTime(DateTimeOffset utc)
    {
        TimeZoneInfo tz = GetTimeZone();
        return TimeZoneInfo.ConvertTime(utc, tz);
    }

    public DateTimeOffset ConvertToUtc(DateTimeOffset local)
    {
        TimeZoneInfo tz = GetTimeZone();

        DateTimeOffset targetTime = TimeZoneInfo.ConvertTime(local, tz);

        return targetTime.ToUniversalTime();
    }
}

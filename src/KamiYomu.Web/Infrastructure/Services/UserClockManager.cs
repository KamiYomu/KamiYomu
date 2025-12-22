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
        var tzId = _contextAccessor.HttpContext?.Request.Cookies["UserTimeZone"];
        if (string.IsNullOrEmpty(tzId))
            return TimeZoneInfo.Utc;

        return TimeZoneInfo.FindSystemTimeZoneById(tzId);
    }

    public DateTimeOffset ConvertToUserTime(DateTimeOffset utc)
    {
        var tz = GetTimeZone();
        return TimeZoneInfo.ConvertTime(utc, tz);
    }

    public DateTimeOffset ConvertToUtc(DateTimeOffset local)
    {
        var tz = GetTimeZone();

        var targetTime = TimeZoneInfo.ConvertTime(local, tz);

        return targetTime.ToUniversalTime();
    }
}

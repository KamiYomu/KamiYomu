namespace KamiYomu.Web.Infrastructure.Services.Interfaces;

public interface IUserClockManager
{
    DateTimeOffset ConvertToUserTime(DateTimeOffset utc);
    DateTimeOffset ConvertToUtc(DateTimeOffset local);
    TimeZoneInfo GetTimeZone();
}

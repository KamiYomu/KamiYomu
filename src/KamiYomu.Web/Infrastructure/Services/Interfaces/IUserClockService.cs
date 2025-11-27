namespace KamiYomu.Web.Infrastructure.Services.Interfaces
{
    public interface IUserClockService
    {
        DateTimeOffset ConvertToUserTime(DateTimeOffset utc);
        DateTimeOffset ConvertToUtc(DateTimeOffset local);
        TimeZoneInfo GetTimeZone();
    }
}

using KamiYomu.Web.Areas.Public.Models;

namespace KamiYomu.Web.Infrastructure.Services.Interfaces;

public interface IStatsService
{
    StatsResponse GetStats();
}

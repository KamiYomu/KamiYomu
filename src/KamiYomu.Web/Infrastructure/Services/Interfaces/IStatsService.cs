using KamiYomu.Web.Entities.Stats;

namespace KamiYomu.Web.Infrastructure.Services.Interfaces;

public interface IStatsService
{
    StatsResponse GetStats();
}

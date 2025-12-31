using KamiYomu.Web.Entities.Stats;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

namespace KamiYomu.Web.Endpoints;


public static class StatsEndpoints
{
    public static IEndpointRouteBuilder MapStatsEndpoints(
        this IEndpointRouteBuilder app)
    {
        _ = app.MapGet("/api/stats", (IStatsService statsService) =>
        {
            StatsResponse stats = statsService.GetStats();
            return Results.Ok(stats);
        })
        .WithName("GetStats")
        .WithTags("Stats");

        return app;
    }
}

namespace KamiYomu.Web.Areas.Public.Models;

public sealed record StatsResponse(
string? Version, string? CoreVersion, int CollectionSize, long WorkerQueuedTasks, long WorkerFailedTasks);

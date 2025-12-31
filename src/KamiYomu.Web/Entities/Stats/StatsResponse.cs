namespace KamiYomu.Web.Entities.Stats;

public sealed record StatsResponse(
string? Version, string? CoreVersion, int CollectionSize, long WorkerQueuedTasks, long WorkerFailedTasks);

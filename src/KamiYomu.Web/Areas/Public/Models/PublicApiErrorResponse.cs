namespace KamiYomu.Web.Areas.Public.Models;

public sealed class PublicApiErrorResponse
{
    public string Error { get; init; } = default!;

    public string? Message { get; init; }

    public string? TraceId { get; init; }

    public DateTime Timestamp { get; init; }
}

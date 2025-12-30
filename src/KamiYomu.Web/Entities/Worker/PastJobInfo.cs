namespace KamiYomu.Web.Entities.Worker;

public record PastJobInfo(string JobId,
                          DateTime? Time,
                          string State,
                          string Method,
                          string Type);

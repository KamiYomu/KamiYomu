namespace KamiYomu.Web.Areas.Settings.Models;

public record PastJobInfo(string JobId,
                          DateTime? Time,
                          string State,
                          string Method,
                          string Type);

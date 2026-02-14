using System.Text.Json.Serialization;

namespace KamiYomu.Web.Models.Definitions;


[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationType
{
    Success,
    Info,
    Warning,
    Danger
}

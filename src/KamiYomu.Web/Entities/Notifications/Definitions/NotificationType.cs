using System.Text.Json.Serialization;

namespace KamiYomu.Web.Entities.Notifications.Definitions
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum NotificationType
    {
        Success, 
        Info,    
        Warning, 
        Danger    
    }
}

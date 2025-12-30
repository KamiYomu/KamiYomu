using KamiYomu.CrawlerAgents.Core.Inputs;

namespace KamiYomu.Web.Areas.Settings.Pages.Shared;

public class CrawlerInputsViewModel
{
    public IEnumerable<AbstractInputAttribute> CrawlerInputs { get; set; } = [];
    public Dictionary<string, string?> AgentMetadata { get; set; } = [];
    public Dictionary<string, object> GetAgentMetadataValues()
    {
        Dictionary<string, object> metadata = [];
        foreach (KeyValuePair<string, string?> item in AgentMetadata)
        {
            if (bool.TryParse(item.Value, out bool boolValue))
            {
                metadata[item.Key] = boolValue;
            }
            else
            {
                metadata[item.Key] = item.Value;
            }
        }
        return metadata;
    }

    public static Dictionary<string, string> GetAgentMetadataValues(Dictionary<string, object> values)
    {
        Dictionary<string, string> metadata = [];
        foreach (KeyValuePair<string, object> item in values)
        {
            metadata[item.Key] = item.Value?.ToString();
        }
        return metadata;
    }
}
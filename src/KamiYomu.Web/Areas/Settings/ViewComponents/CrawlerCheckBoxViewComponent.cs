using System.Text.RegularExpressions;

using KamiYomu.CrawlerAgents.Core.Inputs;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace KamiYomu.Web.Areas.Settings.ViewComponents;

public class CrawlerCheckBoxViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(CrawlerCheckBoxAttribute CrawlerCheckBoxAttribute, Dictionary<string, string> SelectedData, ModelStateEntry? ModelStateEntry)
    {
        return View(new CrawlerCheckBoxViewModel(CrawlerCheckBoxAttribute, SelectedData, ModelStateEntry));
    }
}


public record CrawlerCheckBoxViewModel(CrawlerCheckBoxAttribute CrawlerCheckBoxAttribute, Dictionary<string, string> SelectedData, ModelStateEntry? ModelStateEntry)
{
    public bool IsChecked(string key)
    {
        return (SelectedData.TryGetValue($"{CrawlerCheckBoxAttribute.Name}.{key}", out string? value)
                && bool.TryParse(value, out bool boolValue)
                && boolValue) || (CrawlerCheckBoxAttribute.DefaultValue.Contains(key) && value == null);
    }

    public string GetFieldId(string key)
    {
        string safeKey = Regex.Replace(key.ToLowerInvariant(), @"[^a-z0-9\-]", "-");
        return $"checkbox-{safeKey}";
    }

    public string GetFieldName(string key)
    {
        return $"Input.CrawlerInputsViewModel.AgentMetadata[{CrawlerCheckBoxAttribute.Name}.{key}]";
    }
}

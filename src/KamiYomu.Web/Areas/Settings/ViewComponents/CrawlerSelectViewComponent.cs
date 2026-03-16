using System.Text.RegularExpressions;

using KamiYomu.CrawlerAgents.Core.Inputs;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace KamiYomu.Web.Areas.Settings.ViewComponents;

public class CrawlerSelectViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(CrawlerSelectAttribute crawlerSelectAttribute, string fieldName, string fieldValue, ModelStateEntry? modelStateEntry)
    {
        return View(new CrawlerSelectViewModel(crawlerSelectAttribute, fieldName, fieldValue, modelStateEntry));
    }
}

public record CrawlerSelectViewModel(CrawlerSelectAttribute CrawlerSelectAttribute, string FieldName, string FieldValue, ModelStateEntry? ModelStateEntry)
{
    public string FieldId => $"select-{Regex.Replace(CrawlerSelectAttribute.Name.ToLowerInvariant(), @"[^a-z0-9]+", "-")}";
}

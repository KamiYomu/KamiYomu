using KamiYomu.CrawlerAgents.Core.Inputs;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace KamiYomu.Web.Areas.Settings.ViewComponents;

public class CrawlerPasswordViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(CrawlerPasswordAttribute crawlerPasswordAttribute, string fieldName, string fieldValue, ModelStateEntry? modelStateEntry)
    {
        return View(new CrawlerPasswordViewModel(crawlerPasswordAttribute, fieldName, fieldValue, modelStateEntry));
    }
}

public record CrawlerPasswordViewModel(CrawlerPasswordAttribute CrawlerPasswordAttribute, string FieldName, string FieldValue, ModelStateEntry? ModelStateEntry)
{

}

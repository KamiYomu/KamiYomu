using KamiYomu.CrawlerAgents.Core.Inputs;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace KamiYomu.Web.Areas.Settings.ViewComponents;

public class CrawlerTextViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(CrawlerTextAttribute crawlerTextAttribute, string fieldName, string fieldValue, ModelStateEntry? modelStateEntry)
    {
        return View(new CrawlerTextViewModel(crawlerTextAttribute, fieldName, fieldValue, modelStateEntry));
    }
}

public record CrawlerTextViewModel(CrawlerTextAttribute CrawlerTextAttribute, string FieldName, string FieldValue, ModelStateEntry? ModelStateEntry)
{

}

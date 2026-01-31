using System.Security.Cryptography.Xml;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Areas.Libraries.ViewComponents;

public class MangaTemplateSelectorViewComponent(
            IOptions<SpecialFolderOptions> specialFolderOptions,
            [FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext) : ViewComponent
{
    public IViewComponentResult Invoke(string filePathTemplateSelector,
                                       string comicInfoTitleTemplateSelector,
                                       string ComicInfoSeriesTemplateSelector)
    {

        UserPreference preferences = dbContext.UserPreferences.Query().FirstOrDefault();

        return View(new MangaTemplateSelectorViewModel
        {
            Libraries = dbContext.Libraries.Query().ToList(),
            FilePathTemplateSelector = filePathTemplateSelector,
            ComicInfoTitleTemplateSelector = comicInfoTitleTemplateSelector,
            ComicInfoSeriesTemplateSelector = ComicInfoSeriesTemplateSelector,
            DefaultComicInfoSeriesTemplate = string.IsNullOrWhiteSpace(preferences.ComicInfoSeriesTemplate) ? specialFolderOptions.Value.ComicInfoSeriesFormat : preferences.ComicInfoSeriesTemplate,
            DefaultComicInfoTitleTemplate = string.IsNullOrWhiteSpace(preferences.ComicInfoTitleTemplate) ? specialFolderOptions.Value.ComicInfoTitleFormat : preferences.ComicInfoTitleTemplate,
            DefaultFilePathTemplate = string.IsNullOrWhiteSpace(preferences.FilePathTemplate) ? specialFolderOptions.Value.FilePathFormat : preferences.FilePathTemplate
        });
    }
}

public record MangaTemplateSelectorViewModel
{
    public string FilePathTemplateSelector { get; init; }
    public string ComicInfoTitleTemplateSelector { get; init; }
    public string ComicInfoSeriesTemplateSelector { get; set; }
    public string DefaultFilePathTemplate { get; init; }
    public string DefaultComicInfoTitleTemplate { get; init; }
    public string DefaultComicInfoSeriesTemplate { get; set; }
    public IEnumerable<Library> Libraries { get; init; }
}

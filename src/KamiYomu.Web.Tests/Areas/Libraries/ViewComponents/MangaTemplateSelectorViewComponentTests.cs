using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Areas.Libraries.ViewComponents;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Tests.Areas.Libraries.ViewComponents;

public class MangaTemplateSelectorViewComponentTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");
    private readonly IOptions<SpecialFolderOptions> _specialFolderOptions = Options.Create(new SpecialFolderOptions
    {
        MangaDir = "manga",
        AgentsDir = "agents",
        DbDir = "db",
        LogDir = "logs",
        FilePathFormat = "default-file-path",
        ComicInfoTitleFormat = "default-title",
        ComicInfoSeriesFormat = "default-series"
    });

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private MangaTemplateSelectorViewComponent CreateComponent()
    {
        return new MangaTemplateSelectorViewComponent(_specialFolderOptions, _dbContext);
    }

    [Fact]
    public void Invoke_WhenUserPreferenceTemplatesAreBlank_FallsBackToSpecialFolderDefaults()
    {
        _ = _dbContext.UserPreferences.Insert(new UserPreference(System.Globalization.CultureInfo.GetCultureInfo("en-US")));
        Library library = ServiceTestHelpers.CreateLibrary("Alpha");
        _ = _dbContext.Libraries.Insert(library);

        MangaTemplateSelectorViewComponent component = CreateComponent();

        IViewComponentResult result = component.Invoke("file-selector", "title-selector", "series-selector");

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        MangaTemplateSelectorViewModel model = Assert.IsType<MangaTemplateSelectorViewModel>(viewResult.ViewData.Model);
        Assert.Equal("default-file-path", model.DefaultFilePathTemplate);
        Assert.Equal("default-title", model.DefaultComicInfoTitleTemplate);
        Assert.Equal("default-series", model.DefaultComicInfoSeriesTemplate);
        Assert.Equal("file-selector", model.FilePathTemplateSelector);
        Assert.Equal("title-selector", model.ComicInfoTitleTemplateSelector);
        Assert.Equal("series-selector", model.ComicInfoSeriesTemplateSelector);
        _ = Assert.Single(model.Libraries);
    }

    [Fact]
    public void Invoke_WhenUserPreferenceHasTemplates_UsesUserPreferenceValues()
    {
        UserPreference preference = new(System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        preference.SetFilePathTemplate("custom-file-path");
        preference.SetComicInfoTitleTemplate("custom-title");
        preference.SetComicInfoSeriesTemplate("custom-series");
        _ = _dbContext.UserPreferences.Insert(preference);

        MangaTemplateSelectorViewComponent component = CreateComponent();

        IViewComponentResult result = component.Invoke("f", "t", "s");

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        MangaTemplateSelectorViewModel model = Assert.IsType<MangaTemplateSelectorViewModel>(viewResult.ViewData.Model);
        Assert.Equal("custom-file-path", model.DefaultFilePathTemplate);
        Assert.Equal("custom-title", model.DefaultComicInfoTitleTemplate);
        Assert.Equal("custom-series", model.DefaultComicInfoSeriesTemplate);
    }

    [Fact]
    public void Invoke_FiltersLibrariesByFamilySafeMode()
    {
        UserPreference preference = new(System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        preference.SetFamilySafeMode(true);
        _ = _dbContext.UserPreferences.Insert(preference);

        Library safeLibrary = ServiceTestHelpers.CreateLibrary("SafeManga");
        _ = _dbContext.Libraries.Insert(safeLibrary);

        MangaTemplateSelectorViewComponent component = CreateComponent();

        IViewComponentResult result = component.Invoke("f", "t", "s");

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        MangaTemplateSelectorViewModel model = Assert.IsType<MangaTemplateSelectorViewModel>(viewResult.ViewData.Model);
        _ = Assert.Single(model.Libraries);
    }
}

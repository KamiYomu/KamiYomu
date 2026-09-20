using KamiYomu.Web.Areas.Settings.Models;
using KamiYomu.Web.Areas.Settings.Pages.Add_ons.Dialogs;
using KamiYomu.Web.Infrastructure.Contexts;

namespace KamiYomu.Web.Tests.Areas.Settings.Pages.Add_ons.Dialogs;

public class ListSourceDialogModelTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private ListSourceDialogModel CreateModel()
    {
        return new ListSourceDialogModel(_dbContext);
    }

    [Fact]
    public void OnGet_WithNoSources_PopulatesEmptyList()
    {
        ListSourceDialogModel model = CreateModel();

        model.OnGet();

        Assert.Empty(model.Sources);
    }

    [Fact]
    public void OnGet_WithSources_PopulatesAllSources()
    {
        NugetSource sourceA = new("Source A", new Uri("https://a.example.com/index.json"), null, null);
        NugetSource sourceB = new("Source B", new Uri("https://b.example.com/index.json"), null, null);
        _ = _dbContext.NugetSources.Insert(sourceA);
        _ = _dbContext.NugetSources.Insert(sourceB);

        ListSourceDialogModel model = CreateModel();

        model.OnGet();

        Assert.Equal(2, model.Sources.Count);
        Assert.Contains(model.Sources, s => s.Id == sourceA.Id);
        Assert.Contains(model.Sources, s => s.Id == sourceB.Id);
    }
}

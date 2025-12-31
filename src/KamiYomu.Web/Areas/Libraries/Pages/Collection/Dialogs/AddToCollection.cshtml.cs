using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.CrawlerAgents.Core.Catalog.Builders;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Areas.Libraries.Pages.Collection.Dialogs;

public class AddToCollectionModel(
    IOptions<SpecialFolderOptions> specialFolderOptions,
    DbContext dbContext,
    ICrawlerAgentRepository crawlerAgentRepository) : PageModel
{

    public TemplateVariablesViewModel Variables { get; private set; } = new();
    public Manga? Manga { get; private set; }
    public SpecialFolderOptions SpecialFolderOptions { get; private set; } = specialFolderOptions.Value;

    [BindProperty]
    public string MangaId { get; set; } = string.Empty;
    [BindProperty]
    public required string RefreshElementId { get; set; }
    [BindProperty]
    public Guid CrawlerAgentId { get; set; } = Guid.Empty;
    [BindProperty]
    public required string FilePathTemplate { get; set; }
    [BindProperty]
    public required string ComicInfoTitleTemplate { get; set; }

    [BindProperty]
    public required string ComicInfoSeriesTemplate { get; set; }
    public string[] TemplateResults { get; private set; } = [];
    public string ComicInfoTitleTemplateResult { get; private set; } = "";
    public string ComicInfoSeriesTemplateResult { get; private set; } = "";
    public ComicInfoTemplateViewModel ComicInfoTemplateViewModel { get; set; }
    public async Task OnGetAsync(Guid crawlerAgentId, string mangaId, string refreshElementId, CancellationToken cancellationToken)
    {
        if (crawlerAgentId == Guid.Empty || string.IsNullOrWhiteSpace(mangaId))
        {
            return;
        }
        Entities.UserPreference preferences = dbContext.UserPreferences.FindOne(p => true);

        RefreshElementId = refreshElementId;
        CrawlerAgentId = crawlerAgentId;
        MangaId = mangaId;
        FilePathTemplate = string.IsNullOrWhiteSpace(preferences.FilePathTemplate) ? SpecialFolderOptions.FilePathFormat : preferences.FilePathTemplate;
        ComicInfoTitleTemplate = string.IsNullOrWhiteSpace(preferences.ComicInfoTitleTemplate) ? SpecialFolderOptions.ComicInfoTitleFormat : preferences.ComicInfoTitleTemplate;
        ComicInfoSeriesTemplate = string.IsNullOrWhiteSpace(preferences.ComicInfoSeriesTemplate) ? SpecialFolderOptions.ComicInfoSeriesFormat : preferences.ComicInfoSeriesTemplate;
        Manga = await crawlerAgentRepository.GetMangaAsync(crawlerAgentId, mangaId, cancellationToken);
        TemplateResults = [.. GetTemplateResults(FilePathTemplate, Manga)];
        ComicInfoTemplateViewModel = new ComicInfoTemplateViewModel
        {
            Manga = Manga,
            SeriesTemplatePreview = GetComicInfoTemplateResults(ComicInfoSeriesTemplate, Manga),
            TitleTemplatePreview = GetComicInfoTemplateResults(ComicInfoTitleTemplate, Manga)
        };
        Chapter chapter = ChapterBuilder.Create()
            .WithNumber(1)
            .WithTitle(I18n.ChapterFunnyTemplate1)
            .WithVolume(1)
            .Build();

        Variables = new TemplateVariablesViewModel
        {
            Manga = TemplateResolver.GetMangaVariables(Manga),
            Chapter = TemplateResolver.GetChapterVariables(chapter),
            DateTime = TemplateResolver.GetDateTimeVariables(DateTime.Now)
        };
    }

    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken cancellationToken)
    {
        Manga manga = await crawlerAgentRepository.GetMangaAsync(CrawlerAgentId, MangaId, cancellationToken);

        TemplateResults = [.. GetTemplateResults(FilePathTemplate, manga)];

        return Partial("_PathTemplatePreview", TemplateResults);
    }

    public async Task<IActionResult> OnPostComicInfoPreviewAsync(CancellationToken cancellationToken)
    {
        Manga manga = await crawlerAgentRepository.GetMangaAsync(CrawlerAgentId, MangaId, cancellationToken);

        ComicInfoTitleTemplateResult = GetComicInfoTemplateResults(ComicInfoTitleTemplate, manga);
        ComicInfoSeriesTemplateResult = GetComicInfoTemplateResults(ComicInfoSeriesTemplate, manga);
        return Partial("_ComicInfoTemplatePreview", new ComicInfoTemplateViewModel
        {
            Manga = manga,
            TitleTemplatePreview = ComicInfoTitleTemplateResult,
            SeriesTemplatePreview = ComicInfoSeriesTemplateResult,
        });
    }

    private string GetComicInfoTemplateResults(string template, Manga manga)
    {
        Chapter chapter1 = ChapterBuilder.Create()
                                .WithNumber(1)
                                .WithTitle(I18n.ChapterFunnyTemplate1)
                                .WithVolume(1)
                                .Build();

        return TemplateResolver.Resolve(template, manga, chapter1);
    }

    private List<string> GetTemplateResults(string template, Manga manga)
    {
        Chapter chapter1 = ChapterBuilder.Create()
                                .WithNumber(1)
                                .WithTitle(I18n.ChapterFunnyTemplate1)
                                .WithVolume(1)
                                .Build();

        Chapter chapter2 = ChapterBuilder.Create()
                        .WithNumber(2)
                        .WithTitle(I18n.ChapterFunnyTemplate2)
                        .WithVolume(1)
                        .Build();

        Chapter chapter3 = ChapterBuilder.Create()
                        .WithNumber(3)
                        .WithTitle(I18n.ChapterFunnyTemplate3)
                        .WithVolume(2)
                        .Build();

        Chapter chapter4 = ChapterBuilder.Create()
                        .WithNumber(4)
                        .WithTitle(I18n.ChapterFunnyTemplate4)
                        .WithVolume(2)
                        .Build();

        List<string> results =
        [
                TemplateResolver.Resolve(template, manga, chapter1),

                TemplateResolver.Resolve(
                    template,
                    manga,
                    chapter2,
                    DateTime.Now.AddMinutes(1)
                ),

                TemplateResolver.Resolve(
                    template,
                    manga,
                    chapter3,
                    DateTime.Now.AddMinutes(2)
                ),

                TemplateResolver.Resolve(
                    template,
                    manga,
                    chapter4,
                    DateTime.Now.AddMinutes(3)
                ),
            ];

        if (results.All(string.IsNullOrWhiteSpace))
        {
            ModelState.AddModelError(nameof(FilePathTemplate), I18n.TheTemplatePathIsMissingOrInvalid);
        }
        else
        {
            int distinctCount = results.Where(x => !string.IsNullOrWhiteSpace(x))
                                       .Distinct()
                                       .Count();

            int totalCount = results.Count(x => !string.IsNullOrWhiteSpace(x));

            bool allDifferent = distinctCount == totalCount;

            if (!allDifferent)
            {
                ModelState.AddModelError(nameof(FilePathTemplate), I18n.TheTemplateMustProduceUniqueFileNames);
            }
        }

        return results;
    }
}



public class TemplateVariablesViewModel
{
    public Dictionary<string, string> Manga { get; set; } = [];
    public Dictionary<string, string> Chapter { get; set; } = [];
    public Dictionary<string, string> DateTime { get; set; } = [];
}


public class ComicInfoTemplateViewModel
{
    public string TitleTemplatePreview { get; set; }
    public Manga Manga { get; set; }
    public string SeriesTemplatePreview { get; set; }
}

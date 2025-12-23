using KamiYomu.CrawlerAgents.Core.Catalog.Builders;
using KamiYomu.Web.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Settings.Pages.Preferences
{
    public class IndexModel : PageModel
    {

        public TemplateVariablesViewModel Variables { get; private set; } = new();

        public void OnGet()
        {
            var manga = MangaBuilder.Create()
                .WithTitle("One Piece")
                .WithIsFamilySafe(true)
                .Build();

            var chapter = ChapterBuilder.Create()
                .WithNumber(1)
                .WithTitle("Romance Dawn")
                .WithVolume(1)
                .Build();

            Variables = new TemplateVariablesViewModel
            {
                Manga = TemplateResolver.GetMangaVariables(manga),
                Chapter = TemplateResolver.GetChapterVariables(chapter),
                DateTime = TemplateResolver.GetDateTimeVariables()
            };
        }

        public IActionResult OnPostPreview(string template)
        {
            // Sample objects for preview
            var manga = MangaBuilder.Create()
                .WithTitle("One Piece")
                .WithIsFamilySafe(true)
                .Build();

            var chapter = ChapterBuilder.Create()
                .WithNumber(1)
                .WithTitle("Romance Dawn")
                .WithVolume(1)
                .Build();

            var page = PageBuilder.Create()
                .WithPageNumber(5)
                .Build();

            string result = TemplateResolver.Resolve(template, manga, chapter, page);

            return Partial("_PathTemplatePreview", result);
        }

    }
}


public class TemplateVariablesViewModel
{
    public Dictionary<string, string> Manga { get; set; } = new();
    public Dictionary<string, string> Chapter { get; set; } = new();
    public Dictionary<string, string> DateTime { get; set; } = new();
}
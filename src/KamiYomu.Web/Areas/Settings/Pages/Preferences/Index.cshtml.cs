using KamiYomu.CrawlerAgents.Core.Catalog.Builders;
using KamiYomu.Web.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Settings.Pages.Preferences
{
    public class IndexModel : PageModel
    {
        public void OnGet()
        {
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

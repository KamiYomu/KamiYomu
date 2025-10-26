using KamiYomu.Web.Infrastructure.Contexts;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Libraries.Pages.Mangas
{
    public class IndexModel(DbContext dbContext) : PageModel
    {

        public void OnGet()
        {
        }
    }
}

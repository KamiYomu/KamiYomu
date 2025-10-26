using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Pages.Toast
{
    public class ClearModel : PageModel
    {
        public IActionResult OnGet()
        {
            return new EmptyResult();
        }
    }
}

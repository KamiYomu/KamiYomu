using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Pages
{
    public class LogoutModel : PageModel
    {
        public async Task<IActionResult> OnGetAsync()
        {
            Response.Headers["WWW-Authenticate"] = "Basic realm=\"KamiYomu\"";
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return new EmptyResult();

        }
    }
}

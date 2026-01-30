using KamiYomu.Web.Entities.Addons;
using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using System.ComponentModel.DataAnnotations;

namespace KamiYomu.Web.Areas.Settings.Pages.Add_ons;

public class EditModel(ILogger<EditModel> logger, DbContext dbContext) : PageModel
{
    [BindProperty]
    public NugetSourceInput Input { get; set; } = new();

    public bool IsEditMode => Input.Id != Guid.Empty;

    public IActionResult OnGet(Guid? id)
    {
        if (id.HasValue)
        {
            NugetSource source = dbContext.NugetSources.FindById(id.Value);
            if (source == null)
            {
                return NotFound();
            }

            Input = new NugetSourceInput
            {
                Id = source.Id,
                DisplayName = source.DisplayName.Trim(),
                Url = source.Url.ToString(),
                UserName = source.UserName?.Trim(),
                Password = source.Password?.Trim()
            };
        }

        return Page();
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        NugetSource source = dbContext.NugetSources.FindById(Input.Id);
        if (source != null)
        {
            source.Update(
                Input.DisplayName,
                new Uri(Input.Url),
                Input.UserName?.Trim() ?? "",
                Input.Password?.Trim() ?? ""
            );
            _ = dbContext.NugetSources.Update(source);
        }
        else
        {
            source = new NugetSource(
                Input.DisplayName,
                new Uri(Input.Url),
                Input.UserName?.Trim() ?? "",
                Input.Password?.Trim() ?? ""
            );
            _ = dbContext.NugetSources.Insert(source);
        }

        return RedirectToPage("Index");
    }

    public class NugetSourceInput
    {
        public Guid Id { get; set; }
        [Required]
        public string DisplayName { get; set; } = "";
        [Required]
        [Url]
        [RegularExpression(@".*/index\.json$")]
        public string Url { get; set; } = "";
        public string? UserName { get; set; }
        public string? Password { get; set; }
    }
}

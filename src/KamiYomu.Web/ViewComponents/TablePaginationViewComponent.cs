using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.ViewComponents;

public class TablePaginationViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(
        int currentPage,
        int pageSize,
        int totalItems,
        string pageUrlTemplate,
        string target,
        string swap,
        bool pushUrl = false)
    {
        return View(new TablePaginationViewModel
        {
            CurrentPage = currentPage,
            PageSize = pageSize,
            TotalItems = totalItems,
            PageUrlTemplate = pageUrlTemplate,
            Target = target,
            Swap = swap,
            PushUrl = pushUrl,
            Window = 3,
            EdgeCount = 3
        });
    }
}

public record TablePaginationViewModel
{
    public required int CurrentPage { get; init; }
    public required int TotalItems { get; init; }
    public required string PageUrlTemplate { get; init; }
    public int Window { get; internal set; }
    public int EdgeCount { get; internal set; }
    public string Target { get; internal set; }
    public string Swap { get; internal set; }
    public int PageSize { get; internal set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
    public bool PushUrl { get; internal set; }

    public bool ShouldShow(int page)
    {
        return page <= EdgeCount || page > TotalPages - EdgeCount || Math.Abs(page - CurrentPage) <= Window;
    }

    public string GetPageUrl(int page)
    {
        string pattern = @"\bCurrentPage=[^&]*";
        string replacement = $"CurrentPage={page}";

        return Regex.Replace(PageUrlTemplate, pattern, replacement, RegexOptions.IgnoreCase);
    }
}

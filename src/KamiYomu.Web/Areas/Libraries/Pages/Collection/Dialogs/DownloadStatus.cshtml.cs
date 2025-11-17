using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Infrastructure.Contexts;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Libraries.Pages.Mangas.Dialogs
{
    public class DownloadStatusModel(DbContext dbContext) : PageModel
    {
        public Entities.Library Library { get; set; }
        public decimal Completed { get; set; } = 0;
        public decimal Total { get; set; } = 0;
        public decimal Progress { get; set; } = 0;

        public void OnGet(Guid libraryId)
        {
            Library = dbContext.Libraries.FindOne(p => p.Id == libraryId);
            using var libDbContext = Library.GetDbContext();

            var downloadManga = libDbContext.MangaDownloadRecords.FindOne(p => p.Library.Id == Library.Id);

            if (downloadManga == null) return;

            var downloadChapters = libDbContext.ChapterDownloadRecords.Find(p => p.MangaDownload.Id == downloadManga.Id).OrderBy(p => p.Chapter.Number).ToList();
            Completed = downloadChapters.Count(p => p.DownloadStatus == DownloadStatus.Completed);
            Total = downloadChapters.Count;
            if(Total > 0)
            {
                Progress = (Completed / Total) * 100;
            }
            else
            {
                Progress = 0;
            }

        }
    }

}

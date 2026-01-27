using System.Text.Json.Serialization;

using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;

namespace KamiYomu.Web.Areas.Reader.ViewModels;

public class WeeklyChapterViewModel
{
    public IEnumerable<WeeklyChapterItemViewModel> Items { get; set; }

    public Guid LibraryId { get; set; }
    public string MangaId { get; set; }
    public string MangaTitle { get; set; }
    public Uri MangaCoverUrl { get; set; }

}

public class WeeklyChapterItemViewModel
{
    public Guid ChapterDownloadId { get; set; }
    public DownloadStatus DownloadStatus { get; set; }
    public decimal ChapterNumber { get; set; }
    public DateTimeOffset? StatusUpdateAt { get; internal set; }
}

using KamiYomu.Web.Areas.Reader.Data;
using KamiYomu.Web.Areas.Reader.Models;
using KamiYomu.Web.Areas.Reader.Repositories.Interfaces;
using KamiYomu.Web.Areas.Reader.ViewModels;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Areas.Reader.Repositories;

public class ChapterProgressRepository([FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext,
                                       [FromKeyedServices(ServiceLocator.ReadOnlyReadingDbContext)] ReadingDbContext readingDbContext,
                                       CacheContext cacheContext,
                                       IUserClockManager userClockService) : IChapterProgressRepository
{
    public IEnumerable<IGrouping<DateTime, ChapterViewModel>> FetchHistory(int offset, int limit)
    {
        UserPreference userPreference = dbContext.UserPreferences.Query().FirstOrDefault();

        List<ChapterProgress> chapterProgresses = readingDbContext.ChapterProgress.Query()
            .OrderByDescending(p => p.LastReadAt)
            .Skip(offset)
            .Limit(limit)
            .ToList();

        List<Guid> libraryIds = [.. chapterProgresses.Select(cp => cp.LibraryId).Distinct()];
        Dictionary<Guid, Library> libraries = dbContext.Libraries.Query()
            .Where(l => libraryIds.Contains(l.Id) && (l.Manga.IsFamilySafe || l.Manga.IsFamilySafe == userPreference.FamilySafeMode))
            .ToList()
            .ToDictionary(l => l.Id);

        return chapterProgresses
            .Where(p => libraries.GetValueOrDefault(p.LibraryId) != null)
            .Select(cp => new ChapterViewModel
            {
                ChapterProgress = cp,
                Library = libraries.GetValueOrDefault(cp.LibraryId)
            })
            .GroupBy(cm => cm.ChapterProgress.LastReadAt.Date)
            .OrderByDescending(g => g.Key);
    }


    public IEnumerable<WeeklyChapterViewModel> FetchWeeklyChapters()
    {
        UserPreference userPreference = dbContext.UserPreferences.Query().FirstOrDefault();

        return cacheContext.GetOrSet($"weekly-chapters-{userPreference.FamilySafeMode}", () =>
        {
            DateTimeOffset startDate = DateTimeOffset.UtcNow.Date.AddDays(-7);

            List<WeeklyChapterViewModel> result = [];

            foreach (Library library in dbContext.Libraries.Query().ToList())
            {
                if (!library.Manga.IsFamilySafe && userPreference.FamilySafeMode)
                {
                    continue;
                }

                using LibraryDbContext libraryDbContext = library.GetReadOnlyDbContext();

                IEnumerable<ChapterDownloadRecord> chapterRecords = libraryDbContext.ChapterDownloadRecords
                                                                                    .Query()
                                                                                    .Where(p => (int)(object)p.DownloadStatus == (int)(object)DownloadStatus.Completed
                                                                                           && p.StatusUpdateAt > startDate)
                                                                                    .OrderBy(p => p.Chapter.Number)
                                                                                    .ToList();

                if (chapterRecords.Any())
                {
                    IEnumerable<ChapterDownloadRecord> chapterDownloads = chapterRecords.Where(p =>
                                                                                        readingDbContext.ChapterProgress
                                                                                                        .Query()
                                                                                                        .Where(q =>
                                                                                                               q.ChapterDownloadId == p.Id &&
                                                                                                               q.IsCompleted).Count() == 0);

                    List<WeeklyChapterItemViewModel> items = [.. chapterDownloads.Select(p => new WeeklyChapterItemViewModel
                    {
                        ChapterDownloadId = p.Id,
                        ChapterNumber = p.Chapter.Number,
                        DownloadStatus = p.DownloadStatus,
                        StatusUpdateAt = userClockService.ConvertToUserTime(p.StatusUpdateAt.GetValueOrDefault()).DateTime
                    })];

                    result.Add(new WeeklyChapterViewModel
                    {
                        MangaCoverUrl = library.Manga.CoverUrl,
                        LibraryId = library.Id,
                        MangaId = library.Manga.Id,
                        MangaTitle = library.Manga.Title,
                        Items = items.OrderByDescending(p => p.StatusUpdateAt)
                    });
                }
            }

            return result;
        }, TimeSpan.FromMinutes(5));

    }
}


using KamiYomu.Web.Areas.Reader.Data;
using KamiYomu.Web.Areas.Reader.Models;
using KamiYomu.Web.Areas.Reader.Repositories.Interfaces;
using KamiYomu.Web.Areas.Reader.ViewModels;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Areas.Reader.Repositories;

public class ChapterProgressRepository([FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext,
                                       [FromKeyedServices(ServiceLocator.ReadOnlyReadingDbContext)] ReadingDbContext readingDbContext) : IChapterProgressRepository
{
    public IEnumerable<IGrouping<DateTime, ChapterViewModels>> FetchHistory(int offset, int length)
    {
        UserPreference userPreference = dbContext.UserPreferences.Query().FirstOrDefault();

        List<ChapterProgress> chapterProgresses = readingDbContext.ChapterProgress.Query()
            .OrderByDescending(p => p.LastReadAt)
            .Skip(offset)
            .Limit(length)
            .ToList();

        List<Guid> libraryIds = [.. chapterProgresses.Select(cp => cp.LibraryId).Distinct()];
        Dictionary<Guid, Library> libraries = dbContext.Libraries.Query()
            .Where(l => libraryIds.Contains(l.Id) && (l.Manga.IsFamilySafe || !userPreference.FamilySafeMode))
            .ToList()
            .ToDictionary(l => l.Id);

        return chapterProgresses
            .Where(p => libraries.GetValueOrDefault(p.LibraryId) != null)
            .Select(cp => new ChapterViewModels
            {
                ChapterProgress = cp,
                Library = libraries.GetValueOrDefault(cp.LibraryId)
            })
            .GroupBy(cm => cm.ChapterProgress.LastReadAt.Date)
            .OrderByDescending(g => g.Key);
    }
}

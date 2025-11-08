using Hangfire.Server;
using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Worker.Interfaces;
using System.Globalization;

namespace KamiYomu.Web.Worker
{
    public class ChapterDiscoveryJob : IChapterDiscoveryJob
    {
        private readonly ILogger<ChapterDiscoveryJob> _logger;
        private readonly DbContext _dbContext;

        public ChapterDiscoveryJob(
            ILogger<ChapterDiscoveryJob> logger,
            DbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task DispatchAsync(PerformContext context, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Dispatch cancelled {BackgroundJobId}", context.BackgroundJob.Id);
                return;
            }

            var userPreference = _dbContext.UserPreferences.FindOne(p => true);

            Thread.CurrentThread.CurrentCulture =
            Thread.CurrentThread.CurrentUICulture =
            CultureInfo.CurrentCulture =
            CultureInfo.CurrentUICulture = userPreference?.GetCulture() ?? CultureInfo.GetCultureInfo("en-US");

            var libraries = _dbContext.Libraries.FindAll();

            foreach (var library in libraries) { 
                using var libDbContext = library.GetDbContext();
                var files = Directory.GetFiles(library!.Manga!.GetDirectory(), "*.cbz", SearchOption.AllDirectories);
                var chapters = await library.AgentCrawler.GetCrawlerInstance().GetChaptersAsync(library.Manga, new PaginationOptions(0, 100), cancellationToken);
                
            }

        }
    }
}

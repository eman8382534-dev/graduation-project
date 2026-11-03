using GrawiaaApp.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GrawiaaApp.API.Services
{
    public class BadgeRecommendationSyncService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BadgeRecommendationSyncService> _logger;
        private readonly IConfiguration _config;

        public BadgeRecommendationSyncService(
            IServiceProvider serviceProvider,
            ILogger<BadgeRecommendationSyncService> logger,
            IConfiguration config)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalMinutes = _config.GetValue<int?>("AiTeam:BadgeSyncIntervalMinutes") ?? 5;
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await CheckNewBadgeRecommendationsAsync(stoppingToken); }
                catch (Exception ex) { _logger.LogError(ex, "[BadgeSync] Error"); }

                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
        }

        private async Task CheckNewBadgeRecommendationsAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            // Pick up children where AI wrote RecommendedBadgeId + set Synced=false
            var children = await context.ChildProfiles
                .Where(c => c.RecommendedBadgeId != null
                         && c.RecommendedBadgeSetAt != null
                         && !c.RecommendedBadgeSynced)
                .ToListAsync(ct);

            if (children.Count == 0) return;

            _logger.LogInformation("[BadgeSync] Found {Count} new badge recommendations.", children.Count);

            foreach (var child in children)
            {
                child.RecommendedBadgeSynced = true;
                cache.RemoveByPrefix(CacheKeys.ChildPrefix(child.UserId));
                if (child.ParentId != null)
                    cache.RemoveByPrefix(CacheKeys.ParentPrefix(child.ParentId.Value));

                _logger.LogInformation(
                    "[BadgeSync] Processed badge {BadgeId} for ChildId={ChildId}",
                    child.RecommendedBadgeId, child.UserId);
            }

            await context.SaveChangesAsync(ct);
        }
    }
}
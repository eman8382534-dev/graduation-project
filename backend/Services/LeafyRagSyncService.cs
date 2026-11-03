using GrawiaaApp.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GrawiaaApp.API.Services
{
    public class LeafyRagSyncService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LeafyRagSyncService> _logger;
        private readonly IConfiguration _config;

        public LeafyRagSyncService(
            IServiceProvider serviceProvider,
            ILogger<LeafyRagSyncService> logger,
            IConfiguration config)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalMinutes = _config.GetValue<int?>("AiTeam:LeafySyncIntervalMinutes") ?? 2;
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await SyncLeafyContentAsync(stoppingToken); }
                catch (Exception ex) { _logger.LogError(ex, "[LeafySync] Error"); }

                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
        }

        private async Task SyncLeafyContentAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            var recentWindow = DateTime.UtcNow.Date.AddDays(-7);
            var activeChildIds = await context.DailyLogEntries
                .Where(l => l.LogDate.Date >= recentWindow && l.IsJournalSaved)
                .Select(l => l.ChildId)
                .Distinct()
                .ToListAsync(ct);

            if (activeChildIds.Count == 0) return;

            var profiles = await context.ChildProfiles
                .Where(c => activeChildIds.Contains(c.UserId))
                .ToListAsync(ct);

            int aiProcessed = 0;
            int fallbackApplied = 0;

            foreach (var child in profiles)
            {
                // AI content written — preserve and mark synced
                if (child.LeafyTextSetAt != null && !child.LeafySynced)
                {
                    child.LeafySynced = true;
                    InvalidateCache(cache, child);
                    aiProcessed++;
                    _logger.LogInformation("[LeafySync] Processed AI Leafy content for ChildId={ChildId}", child.UserId);
                    continue;
                }

                if (LeafyRagFallbackHelper.HasAiLeafyRecommendation(child))
                {
                    if (!child.LeafySynced)
                    {
                        child.LeafySynced = true;
                        InvalidateCache(cache, child);
                        aiProcessed++;
                    }
                    continue;
                }

                // No AI recommendation — backend fallback
                if (!child.LeafySynced || string.IsNullOrWhiteSpace(child.Tip))
                {
                    await LeafyRagFallbackHelper.ApplyFallbackAsync(context, child, ct);
                    InvalidateCache(cache, child);
                    fallbackApplied++;
                    _logger.LogInformation("[LeafySync] Applied fallback Leafy content for ChildId={ChildId}", child.UserId);
                }
            }

            if (aiProcessed > 0 || fallbackApplied > 0)
            {
                await context.SaveChangesAsync(ct);
                _logger.LogInformation("[LeafySync] AI={Ai} Fallback={Fallback}", aiProcessed, fallbackApplied);
            }
        }

        private static void InvalidateCache(ICacheService cache, Models.ChildProfile child)
        {
            cache.RemoveByPrefix(CacheKeys.ChildPrefix(child.UserId));
            if (child.ParentId != null)
                cache.RemoveByPrefix(CacheKeys.ParentPrefix(child.ParentId.Value));
        }
    }
}

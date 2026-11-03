// ============================================================
//  WeeklyBadgeRecommenderOrchestrationService.cs — Leafy AI System (Phase 12+16)
//
//  Primary: AI team writes RecommendedBadgeId via POST /api/ai/badge-recommendation.
//  Fallback (Phase 16): when RecommendedBadgeId is missing, pick the highest
//  badge matching the child's strongest Final Hybrid pillar, excluding unlocked badges.
// ============================================================

using GrawiaaApp.API.Data;
using GrawiaaApp.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GrawiaaApp.API.Services
{
    public class WeeklyBadgeRecommenderOrchestrationService : BackgroundService
    {
        private readonly ILogger<WeeklyBadgeRecommenderOrchestrationService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private DateTime? _lastRunDate;

        private static readonly (string PillarKey, Func<Badge, decimal> PctSelector)[] PillarBadgeMap =
        {
            ("Spiritual",  b => b.SpirPct),
            ("Social",     b => b.SocPct),
            ("Academic",   b => b.AcadPct),
            ("Home",       b => b.EnvPct),
            ("Nutrition",  b => b.PhysPct),
            ("Sport",      b => b.ResPct),
            ("Creativity", b => b.CreaPct),
            ("Mental",     b => b.MentPct),
        };

        public WeeklyBadgeRecommenderOrchestrationService(
            ILogger<WeeklyBadgeRecommenderOrchestrationService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("WeeklyBadgeRecommenderOrchestrationService started at {Time}", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);

                    var now = DateTime.UtcNow;
                    var alreadyRanToday = _lastRunDate.HasValue && _lastRunDate.Value.Date == now.Date;

                    if (now.DayOfWeek == DayOfWeek.Monday && now.Hour == 3 && !alreadyRanToday)
                    {
                        await RunWeeklyValidationAsync(stoppingToken);
                        _lastRunDate = now.Date;
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in WeeklyBadgeRecommenderOrchestrationService at {Time}", DateTime.UtcNow);
                }
            }
        }

        private async Task RunWeeklyValidationAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            var recentWindow = DateTime.UtcNow.Date.AddDays(-7);
            var activeChildIds = await context.DailyLogEntries
                .Where(l => l.LogDate.Date >= recentWindow && l.IsJournalSaved)
                .Select(l => l.ChildId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (activeChildIds.Count == 0)
            {
                _logger.LogInformation("[AiOrchestration:Weekly] No active children in the last 7 days — nothing to validate.");
                return;
            }

            var activeProfiles = await context.ChildProfiles
                .Where(c => activeChildIds.Contains(c.UserId))
                .ToListAsync(cancellationToken);

            var allBadges = await context.Badges.ToListAsync(cancellationToken);
            int fallbackCount = 0;

            foreach (var child in activeProfiles)
            {
                if (child.RecommendedBadgeId != null)
                {
                    var alreadyUnlocked = await context.ChildBadges
                        .AnyAsync(cb => cb.ChildId == child.UserId
                                     && cb.BadgeId == child.RecommendedBadgeId
                                     && cb.IsActive, cancellationToken);

                    if (alreadyUnlocked)
                    {
                        _logger.LogWarning(
                            "[AiOrchestration:Weekly] Child {ChildId}'s RecommendedBadgeId={BadgeId} is already unlocked — AI refresh overdue.",
                            child.UserId, child.RecommendedBadgeId);
                    }

                    continue;
                }

                var unlockedIds = await context.ChildBadges
                    .Where(cb => cb.ChildId == child.UserId && cb.IsActive)
                    .Select(cb => cb.BadgeId)
                    .ToListAsync(cancellationToken);

                var badgeId = PickFallbackBadge(child, allBadges, unlockedIds);
                if (badgeId.HasValue)
                {
                    child.RecommendedBadgeId = badgeId.Value;
                    child.RecommendedBadgeSetAt = DateTime.UtcNow;
                    child.RecommendedBadgeSynced = false;
                    fallbackCount++;

                    cache.RemoveByPrefix(CacheKeys.ChildPrefix(child.UserId));
                    if (child.ParentId != null)
                        cache.RemoveByPrefix(CacheKeys.ParentPrefix(child.ParentId.Value));

                    _logger.LogInformation(
                        "[AiOrchestration:Weekly] Fallback RecommendedBadgeId={BadgeId} for ChildId={ChildId}",
                        badgeId.Value, child.UserId);
                }
            }

            if (fallbackCount > 0)
                await context.SaveChangesAsync(cancellationToken);

            var withRecommendation = activeProfiles.Count(c => c.RecommendedBadgeId != null);
            var withoutRecommendation = activeProfiles.Count - withRecommendation;

            _logger.LogInformation(
                "[AiOrchestration:Weekly] Badge Recommender — {With}/{Total} have RecommendedBadgeId; {Fallback} fallback(s) applied this run.",
                withRecommendation, activeProfiles.Count, fallbackCount);

            var staleCutoff = DateTime.UtcNow.AddDays(-9);
            var staleRecommendations = activeProfiles
                .Where(c => c.RecommendedBadgeId != null &&
                            (c.RecommendedBadgeSetAt == null || c.RecommendedBadgeSetAt < staleCutoff))
                .Select(c => c.UserId)
                .ToList();

            if (staleRecommendations.Count > 0)
            {
                _logger.LogInformation(
                    "[AiOrchestration:Weekly] {Count} stale recommendations (>9 days): {ChildIds}",
                    staleRecommendations.Count, string.Join(",", staleRecommendations));
            }
        }

        internal static int? PickFallbackBadge(ChildProfile child, List<Badge> allBadges, List<int> unlockedIds)
        {
            var pillarScores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["Spiritual"]  = child.FinalSpiritualConnection_Pct  > 0 ? child.FinalSpiritualConnection_Pct  : child.SpiritualConnection_Pct,
                ["Social"]     = child.FinalSocialInteraction_Pct    > 0 ? child.FinalSocialInteraction_Pct    : child.SocialInteraction_Pct,
                ["Academic"]   = child.FinalAcademicProgress_Pct     > 0 ? child.FinalAcademicProgress_Pct     : child.AcademicProgress_Pct,
                ["Home"]       = child.FinalHomeResponsibility_Pct   > 0 ? child.FinalHomeResponsibility_Pct   : child.HomeResponsibility_Pct,
                ["Nutrition"]  = child.FinalPhysicalHealth_Pct       > 0 ? child.FinalPhysicalHealth_Pct       : child.PhysicalHealth_Pct,
                ["Sport"]      = child.FinalPhysicalActivity_Pct     > 0 ? child.FinalPhysicalActivity_Pct     : child.PhysicalActivity_Pct,
                ["Creativity"] = child.FinalCreativityEngagement_Pct > 0 ? child.FinalCreativityEngagement_Pct : child.CreativityEngagement_Pct,
                ["Mental"]     = child.FinalMentalWellbeing_Pct      > 0 ? child.FinalMentalWellbeing_Pct      : child.MentalWellbeing_Pct,
            };

            var strongestPillar = pillarScores.OrderByDescending(p => p.Value).First().Key;
            var mapEntry = PillarBadgeMap.First(m => m.PillarKey.Equals(strongestPillar, StringComparison.OrdinalIgnoreCase));

            var candidates = allBadges
                .Where(b => !unlockedIds.Contains(b.Id) && mapEntry.PctSelector(b) > 0)
                .OrderByDescending(b => mapEntry.PctSelector(b))
                .ThenBy(b => b.Id)
                .ToList();

            return candidates.FirstOrDefault()?.Id;
        }
    }
}

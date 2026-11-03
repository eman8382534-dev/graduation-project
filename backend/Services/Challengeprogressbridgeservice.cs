// ============================================================
//  ChallengeProgressBridgeService.cs — Phase 14 Bug Fix
//
//  PROBLEM SOLVED:
//  ───────────────
//  Confirmed with the AI team (same pattern as NlpPillarSyncService,
//  2026-06-21): the AI's NLP pipeline writes "journal_text" challenge
//  completions DIRECTLY to the child_challenge_progress table via raw
//  SQL (hashed_child_id + challenge_id + is_completed). It does NOT
//  call POST /api/ai/challenge-progress/{hashedChildId} — that endpoint
//  exists as a secondary path only, same as the AiController log-result
//  endpoints.
//
//  Because EF Core / the app layer never sees these writes, NEITHER of
//  the two steps that normally follow a challenge-progress update ever
//  run on their own:
//    • NewBadgeEngineService.EvaluateAndUnlockBadgesAsync — unlocks any
//      badge whose ALL challenges are now complete (writes ChildBadges).
//    • NewLevelEngineService.EvaluateLevelUpAsync — re-checks level-up
//      eligibility (badge_count + min_trust_score against the next level).
//
//  Every badge in the AI Team Badge Library (Ids 28-40 — Morning Prayer,
//  Storyteller, etc.) is gated ENTIRELY by MetricType="journal_text"
//  challenges, so without this service those badges can be 100%
//  complete in child_challenge_progress and STILL never appear unlocked
//  to the child, because nothing ever runs the unlock step. The badge
//  only "catches up" by coincidence, if/when the child later saves a
//  new journal entry or completes a task (which calls
//  NewProgressionService.RunAsync for unrelated reasons).
//
//  FIX: a lightweight, fast-interval background service that scans for
//  children with any IsCompleted=true ChildChallengeProgress row whose
//  badge is not yet in ChildBadges, and runs the same evaluation engines
//  the rest of the app already uses (no new business logic — this is
//  purely a trigger/bridge, same role NlpPillarSyncService plays for
//  the pillar pipeline).
//
//  CONFIGURATION (appsettings.json):
//    "AiTeam": {
//      "ChallengeBridgeIntervalMinutes": 5,   // how often to run (default 5)
//      "ChallengeBridgeBatchSize":       200  // max children per tick (default 200)
//    }
//
//  Interval is intentionally much shorter than NlpPillarSyncService's
//  60 minutes — an unlocked badge is a moment of delight for the child
//  and should show up promptly, not sit hidden for up to an hour.
// ============================================================

using GrawiaaApp.API.Data;
using Microsoft.EntityFrameworkCore;

namespace GrawiaaApp.API.Services
{
    public class ChallengeProgressBridgeService : BackgroundService
    {
        private readonly ILogger<ChallengeProgressBridgeService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;

        public ChallengeProgressBridgeService(
            ILogger<ChallengeProgressBridgeService> logger,
            IServiceProvider serviceProvider,
            IConfiguration config)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[ChallengeBridge] Service started at {Time}", DateTime.UtcNow);

            // AUDIT FIX (real-time tuning): ChallengeBridgeIntervalSeconds takes
            // priority when present, so this service can run sub-minute (e.g.
            // every 5-10 seconds for live demos/testing). Falls back to the
            // original ChallengeBridgeIntervalMinutes (minimum 1 minute) when the
            // seconds key is absent, so existing appsettings.json files keep
            // working unchanged.
            var intervalSeconds = _config.GetValue<int?>("AiTeam:ChallengeBridgeIntervalSeconds");
            TimeSpan interval;
            if (intervalSeconds.HasValue)
            {
                interval = TimeSpan.FromSeconds(Math.Max(1, intervalSeconds.Value));
            }
            else
            {
                var intervalMinutes = _config.GetValue<int?>("AiTeam:ChallengeBridgeIntervalMinutes") ?? 5;
                interval = TimeSpan.FromMinutes(Math.Max(1, intervalMinutes));
            }

            // Initial delay: let the app finish startup before first run.
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await BridgeCompletedChallengesAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Normal during shutdown.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ChallengeBridge] Error during bridge run at {Time}", DateTime.UtcNow);
                }

                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("[ChallengeBridge] Service stopped at {Time}", DateTime.UtcNow);
        }

        private async Task BridgeCompletedChallengesAsync(CancellationToken cancellationToken)
        {
            int batchSize = _config.GetValue<int?>("AiTeam:ChallengeBridgeBatchSize") ?? 200;

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var badgeEngine = scope.ServiceProvider.GetRequiredService<INewBadgeEngineService>();
            var levelEngine = scope.ServiceProvider.GetRequiredService<INewLevelEngineService>();
            var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

            // ── Candidate set ───────────────────────────────────────────
            // Children who have at least one completed challenge progress
            // row for a badge they do NOT yet hold in ChildBadges. This
            // catches the exact gap left by direct-SQL writes from the AI
            // pipeline (journal_text challenges), but is intentionally
            // written generically — it would equally catch a missed
            // evaluation from any other path too.
            var unlockedBadgeIdsByChild = await context.ChildBadges
                .Where(cb => cb.IsActive)
                .GroupBy(cb => cb.ChildId)
                .Select(g => new { ChildId = g.Key, BadgeIds = g.Select(cb => cb.BadgeId).ToList() })
                .ToListAsync(cancellationToken);

            var unlockedLookup = unlockedBadgeIdsByChild
                .ToDictionary(x => x.ChildId, x => x.BadgeIds.ToHashSet());

            // Completed progress rows joined to their badge, grouped by child.
            var completedByChild = await context.ChildChallengeProgresses
                .Where(cp => cp.IsCompleted)
                .Select(cp => new { cp.ChildId, cp.ChallengeId, BadgeId = cp.Challenge.BadgeId })
                .ToListAsync(cancellationToken);

            var candidateChildIds = completedByChild
                .GroupBy(cp => cp.ChildId)
                .Where(g =>
                {
                    var alreadyUnlocked = unlockedLookup.TryGetValue(g.Key, out var set)
                        ? set
                        : new HashSet<int>();
                    // Candidate if this child has a completed challenge whose
                    // badge isn't unlocked yet — EvaluateAndUnlockBadgesAsync
                    // will independently re-verify ALL challenges for that
                    // badge are done before actually unlocking it, so a
                    // false-positive candidate here is harmless (just a
                    // no-op scoped DB call).
                    return g.Select(cp => cp.BadgeId).Any(bid => !alreadyUnlocked.Contains(bid));
                })
                .Select(g => g.Key)
                .Take(batchSize)
                .ToList();

            if (candidateChildIds.Count == 0)
            {
                _logger.LogDebug("[ChallengeBridge] No pending badge/level evaluations found at {Time}", DateTime.UtcNow);
                return;
            }

            _logger.LogInformation(
                "[ChallengeBridge] Evaluating badges/levels for {Count} children with completed-but-unlinked challenge progress.",
                candidateChildIds.Count);

            int unlockedTotal = 0;
            int leveledUpTotal = 0;
            int failed = 0;

            foreach (var childId in candidateChildIds)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    // Re-use the SAME engines the rest of the app calls after
                    // a journal save / task completion — no duplicated logic.
                    var newBadgeIds = await badgeEngine.EvaluateAndUnlockBadgesAsync(childId);
                    if (newBadgeIds.Count > 0)
                    {
                        unlockedTotal += newBadgeIds.Count;
                        _logger.LogInformation(
                            "[ChallengeBridge] ChildId={ChildId} — unlocked {Count} badge(s) via AI-written challenge completions: {BadgeIds}",
                            childId, newBadgeIds.Count, string.Join(",", newBadgeIds));
                    }

                    var levelResult = await levelEngine.EvaluateLevelUpAsync(childId);
                    if (levelResult.LeveledUp)
                    {
                        leveledUpTotal++;
                        _logger.LogInformation(
                            "[ChallengeBridge] ChildId={ChildId} — leveled up {Old}→{New} ({Name}) via AI-written challenge completions.",
                            childId, levelResult.OldLevel, levelResult.CurrentLevel, levelResult.LevelName);
                    }

                    // AUDIT FIX (cache-invalidation gap): badgeEngine and
                    // levelEngine write directly to ChildBadges/ChildProfile/
                    // UserLevels via EF Core, but ChildController.GetProfile
                    // and similar reads cache their results for several
                    // minutes via ICacheService. Those caches are normally
                    // invalidated by the app's own write endpoints — this
                    // background bridge never goes through them, so without
                    // this explicit invalidation a child could see a stale
                    // (not-yet-unlocked / not-yet-leveled-up) profile for
                    // up to 3 minutes after the AI-driven unlock/level-up
                    // already happened in the database. Only invalidate when
                    // something actually changed, to keep this cheap.
                    if (newBadgeIds.Count > 0 || levelResult.LeveledUp)
                    {
                        cacheService.RemoveByPrefix(CacheKeys.ChildPrefix(childId));

                        var childProfile = await context.ChildProfiles
                            .AsNoTracking()
                            .FirstOrDefaultAsync(cp => cp.UserId == childId, cancellationToken);
                        if (childProfile?.ParentId != null)
                            cacheService.RemoveByPrefix(CacheKeys.ParentPrefix(childProfile.ParentId.Value));
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex,
                        "[ChallengeBridge] Failed to evaluate badges/levels for ChildId={ChildId}", childId);
                }
            }

            _logger.LogInformation(
                "[ChallengeBridge] Bridge run complete — {Children} children checked, {Badges} badge(s) unlocked, {Levels} level-up(s), {Failed} failed.",
                candidateChildIds.Count, unlockedTotal, leveledUpTotal, failed);
        }
    }
}
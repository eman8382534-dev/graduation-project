// ============================================================
//  NewLevelEngineService.cs — NEW Level System (Phase 12)
//
//  REPLACES: LevelRequirementsEngine (static class)
//            Any direct writes to ChildProfile.Level
//
//  Rules:
//  1. Levels are stored in the DB (Levels table).
//  2. UserLevels table is the ONLY authoritative record of current level.
//  3. Level-up logic matches the AI team's check_for_level_up spec
//     LITERALLY (Phase 13 alignment):
//         badge_count   = total unlocked ChildBadges (simple count)
//         current_score = ChildProfile.FinalMeanScore (70% Structured + 30% NLP)
//                         Falls back to MeanScore if FinalMeanScore = 0
//                         (AI team has not yet written NLP pillars for this child).
//         next_level requirements = Levels row for (currentLevel + 1)
//         level up IF badge_count >= required_badges
//                  AND current_score >= min_trust_score
//     No challenge-completion or TrustScore gating is layered on top —
//     the AI team's FinalMeanScore is the canonical progress signal so
//     their NLP pipeline writes immediately and predictably affect
//     level-up eligibility.
//  4. This service is called by NewProgressionService.
//  5. On level-up, this service writes to UserLevel and awards
//     the LevelUpBadge if configured (and marks that badge's own
//     challenges, if any, as completed for DB consistency — this is
//     bookkeeping for the awarded badge, not a level-up gate).
// ============================================================

using GrawiaaApp.API.Data;
using GrawiaaApp.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrawiaaApp.API.Services
{
    public interface INewLevelEngineService
    {
        /// <summary>
        /// Evaluate whether the child qualifies for a level-up.
        /// Returns true if the child leveled up, false otherwise.
        ///
        /// Requirements (per AI team spec — check_for_level_up, Phase 13):
        ///   1. badge_count: total unlocked ChildBadges (simple count,
        ///      no challenge-completion gating) must meet or exceed the
        ///      NEXT level's RequiredBadges.
        ///   2. current_score: ChildProfile.FinalMeanScore (70% Structured
        ///      + 30% NLP hybrid mean, 0-100 scale) must meet or exceed
        ///      the NEXT level's MinTrustScore × 100. Falls back to
        ///      MeanScore if FinalMeanScore = 0 (AI pipeline not yet run).
        /// Only when BOTH conditions hold does this update UserLevels,
        /// advance the level, and persist the changes.
        /// </summary>
        Task<LevelUpResult> EvaluateLevelUpAsync(int childId);

        /// <summary>
        /// PHASE 10: check_for_level_up(hashed_child_id) entry point.
        /// Thin alias for <see cref="EvaluateLevelUpAsync"/> so callers can
        /// invoke level-up evaluation using the name specified by the system
        /// requirements. Identifier here is the integer ChildId (the
        /// AspNetUsers primary key) used throughout this backend.
        /// </summary>
        Task<LevelUpResult> CheckForLevelUpAsync(int childId);

        /// <summary>
        /// Get the current level data for a child from user_levels.
        /// Creates a UserLevel record at Level 1 if none exists.
        /// </summary>
        Task<UserLevel> GetOrCreateUserLevelAsync(int childId);

        /// <summary>
        /// Add EXP to the child's UserLevel record.
        /// Does NOT trigger level-up evaluation — call EvaluateLevelUpAsync for that.
        /// </summary>
        Task AddExpAsync(int childId, int expPoints);

        /// <summary>
        /// Build journey-progress data for the child (replaces LevelRequirementsEngine.GetHints).
        /// </summary>
        Task<JourneyProgressData> GetJourneyProgressAsync(int childId);
    }

    public class NewLevelEngineService : INewLevelEngineService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NewLevelEngineService> _logger;
        private readonly ILevelRulesProvider _levelRules;
        private readonly IChildIdentityService _childIdentity;
        private readonly ITrustScoreEngine _trustScoreEngine;
        // it would silently block progression. Now we query the actual max from the DB.
        // Cached per request in EvaluateLevelUpAsync to avoid repeated queries.

        public NewLevelEngineService(
            ApplicationDbContext context,
            ILogger<NewLevelEngineService> logger,
            ILevelRulesProvider levelRules,
            IChildIdentityService childIdentity,
            ITrustScoreEngine trustScoreEngine)
        {
            _context = context;
            _logger = logger;
            _levelRules = levelRules;
            _childIdentity = childIdentity;
            _trustScoreEngine = trustScoreEngine;
        }

        public async Task<UserLevel> GetOrCreateUserLevelAsync(int childId)
        {
            var userLevel = await _context.UserLevels
                .Include(ul => ul.Level)
                .FirstOrDefaultAsync(ul => ul.ChildId == childId);

            if (userLevel == null)
            {
                var levelOne = await _context.Levels
                    .FirstOrDefaultAsync(l => l.LevelNumber == 1);

                if (levelOne == null)
                    throw new InvalidOperationException("Level 1 not found in Levels table. Run seed migration.");

                userLevel = new UserLevel
                {
                    ChildId = childId,
                    HashedChildId = _childIdentity.GetSafeChildKey(childId),
                    LevelId = levelOne.Id,
                    LevelNumber = 1,
                    CurrentExp = 0,
                    TotalExp = 0,
                    LastUpdatedAt = DateTime.UtcNow
                };
                _context.UserLevels.Add(userLevel);
                await _context.SaveChangesAsync();

                // Reload with navigation
                userLevel = await _context.UserLevels
                    .Include(ul => ul.Level)
                    .FirstAsync(ul => ul.ChildId == childId);
            }
            else if (string.IsNullOrEmpty(userLevel.HashedChildId))
            {
                // Backfill HashedChildId on existing records
                userLevel.HashedChildId = _childIdentity.GetSafeChildKey(childId);
                await _context.SaveChangesAsync();
            }

            return userLevel;
        }

        public async Task AddExpAsync(int childId, int expPoints)
        {
            var userLevel = await GetOrCreateUserLevelAsync(childId);
            userLevel.CurrentExp += expPoints;
            userLevel.TotalExp += expPoints;
            userLevel.LastUpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<LevelUpResult> EvaluateLevelUpAsync(int childId)
        {
            var userLevel = await GetOrCreateUserLevelAsync(childId);
            int currentLevelNum = userLevel.LevelNumber;

            // FIX (Bug #4): Query actual max level from DB instead of hardcoded 6.
            int maxLevel = await _context.Levels.MaxAsync(l => (int?)l.LevelNumber) ?? 6;
            if (currentLevelNum >= maxLevel)
                return new LevelUpResult { LeveledUp = false, CurrentLevel = currentLevelNum };

            // ── Next level's requirements (spec: check_for_level_up) ────────
            // The AI team's check_for_level_up(hashed_child_id) function
            // evaluates the CURRENT child's stats against the NEXT level's
            // requirements:
            //     next_level = current_level + 1
            //     req = get_level_requirements(next_level)
            //     if badge_count >= req['required_badges']
            //        and current_score >= req['min_trust_score']:
            //         level up
            int nextLevelNum = currentLevelNum + 1;
            var nextLevelEntity = await _context.Levels
                .FirstOrDefaultAsync(l => l.LevelNumber == nextLevelNum);

            if (nextLevelEntity == null)
                return new LevelUpResult { LeveledUp = false, CurrentLevel = currentLevelNum };

            // ── badge_count: total unlocked badges, spec-literal count ──────
            // Matches the AI team's get_user_unlocked_badge_count(hashed_child_id):
            // a simple COUNT of every badge this child has unlocked, with no
            // extra challenge-completion gating layered on top.
            int badgeCount = await _context.ChildBadges
                .CountAsync(cb => cb.ChildId == childId && cb.IsActive);

            bool hasEnoughBadges = badgeCount >= nextLevelEntity.RequiredBadges;

            // ── current_score: FinalMeanScore (avg of the 8 Final Hybrid Pillars) ─
            // Per Phase 13 spec: all downstream systems use Final Pillars.
            // FinalMeanScore = mean of FinalXxx_Pct (70% structured + 30% NLP).
            // MinTrustScore on Levels is stored 0.0–1.0 (spec scale); FinalMeanScore
            // is stored 0–100, so compare on the same 0–100 scale here.
            // MeanScore is retained for backward compat / AI team reads.
            var profile = await _context.ChildProfiles.FirstOrDefaultAsync(cp => cp.UserId == childId);
            double currentScore = profile != null
                ? (profile.FinalMeanScore > 0 ? profile.FinalMeanScore : profile.MeanScore)
                : 0;
            double requiredScore = nextLevelEntity.MinTrustScore * 100.0;

            bool hasEnoughScore = currentScore >= requiredScore;

            // Presentation spec: level-up also requires low anomaly (honest journaling).
            var cutoff = DateTime.UtcNow.Date.AddDays(-30);
            var recentLogs = await _context.DailyLogEntries
                .Where(l => l.ChildId == childId && l.IsJournalSaved && l.LogDate >= cutoff)
                .ToListAsync();
            double avgAnomaly = recentLogs.Any()
                ? recentLogs.Average(l => (double)l.AnomalyScore)
                : 0;
            bool hasLowAnomaly = avgAnomaly <= nextLevelEntity.MaxAnomalyScore;

            if (!hasEnoughBadges || !hasEnoughScore || !hasLowAnomaly)
            {
                _logger.LogInformation(
                    "Level-up check failed: ChildId={ChildId} CurrentLevel={Level} BadgeCount={BadgeCount}/{ReqBadges} MeanScore={Score}/{ReqScore} AvgAnomaly={Anomaly}/{MaxAnomaly}",
                    childId, currentLevelNum, badgeCount, nextLevelEntity.RequiredBadges, currentScore, requiredScore, avgAnomaly, nextLevelEntity.MaxAnomalyScore);

                return new LevelUpResult { LeveledUp = false, CurrentLevel = currentLevelNum };
            }

            int newLevelNum = nextLevelNum;
            var newLevelEntity = nextLevelEntity;

            userLevel.LevelId = newLevelEntity.Id;
            userLevel.LevelNumber = newLevelNum;
            userLevel.HashedChildId = _childIdentity.GetSafeChildKey(childId); // keep in sync
            userLevel.CurrentExp += newLevelEntity.ExpReward;
            userLevel.TotalExp += newLevelEntity.ExpReward;
            userLevel.LastLevelUpAt = DateTime.UtcNow;
            userLevel.LastUpdatedAt = DateTime.UtcNow;

            if (profile != null)
            {
                profile.Level = newLevelNum;
                profile.CurrentExp += newLevelEntity.ExpReward;
            }


            if (newLevelEntity.LevelUpBadgeId.HasValue)
            {
                bool alreadyHas = await _context.ChildBadges
                    .AnyAsync(cb => cb.ChildId == childId
                               && cb.BadgeId == newLevelEntity.LevelUpBadgeId.Value
                               && cb.IsActive);

                if (!alreadyHas)
                {
                    _context.ChildBadges.Add(new ChildBadge
                    {
                        ChildId = childId,
                        BadgeId = newLevelEntity.LevelUpBadgeId.Value,
                        UnlockedAt = DateTime.UtcNow,
                        IsActive = true
                    });

                    // BUG FIX (Phase 11): Auto-awarded LevelUpBadges bypassed their own
                    // challenge requirements, causing R1/R4 integrity violations (badge
                    // in ChildBadges but challenges not completed in ChildChallengeProgress).
                    // Fix: when auto-awarding, also mark all the badge's challenges as
                    // completed in ChildChallengeProgress so the DB stays consistent.
                    var badgeChallenges = await _context.BadgeChallenges
                        .Where(bc => bc.BadgeId == newLevelEntity.LevelUpBadgeId.Value)
                        .ToListAsync();

                    if (badgeChallenges.Any())
                    {
                        var hashedChildId = _childIdentity.GetSafeChildKey(childId);
                        var existingProgressIds = await _context.ChildChallengeProgresses
                            .Where(cp => cp.ChildId == childId
                                      && badgeChallenges.Select(bc => bc.Id).Contains(cp.ChallengeId))
                            .ToDictionaryAsync(cp => cp.ChallengeId);

                        foreach (var challenge in badgeChallenges)
                        {
                            if (existingProgressIds.TryGetValue(challenge.Id, out var prog))
                            {
                                if (!prog.IsCompleted)
                                {
                                    prog.IsCompleted = true;
                                    prog.CurrentValue = Math.Max(prog.CurrentValue, challenge.TargetValue);
                                    prog.UnlockedAt = DateTime.UtcNow;
                                    prog.LastUpdatedAt = DateTime.UtcNow;
                                    prog.HashedChildId = hashedChildId;
                                }
                            }
                            else
                            {
                                _context.ChildChallengeProgresses.Add(new ChildChallengeProgress
                                {
                                    ChildId = childId,
                                    HashedChildId = hashedChildId,
                                    ChallengeId = challenge.Id,
                                    CurrentValue = challenge.TargetValue,
                                    IsCompleted = true,
                                    UnlockedAt = DateTime.UtcNow,
                                    LastUpdatedAt = DateTime.UtcNow
                                });
                            }
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "🎉 Level up! ChildId={ChildId} {OldLevel}→{NewLevel}",
                childId, currentLevelNum, newLevelNum);

            return new LevelUpResult
            {
                LeveledUp = true,
                OldLevel = currentLevelNum,
                CurrentLevel = newLevelNum,
                LevelName = newLevelEntity.Name,
                ExpAwarded = newLevelEntity.ExpReward
            };
        }

        public async Task<LevelUpResult> CheckForLevelUpAsync(int childId)
            => await EvaluateLevelUpAsync(childId);

        // NOTE (Phase 10 Refactor): the previous hardcoded
        // MinTrustScoreForLevel(int) switch statement has been REMOVED.
        // Its values were moved verbatim into
        // LevelRulesProvider.DefaultMinTrustScoreByLevel as a
        // backward-compatible fallback, with an optional configuration
        // override at "LevelRules:MinTrustScoreByLevel". See
        // Services/LevelRulesProvider.cs for the single source of truth.

        public async Task<JourneyProgressData> GetJourneyProgressAsync(int childId)
        {
            var userLevel = await GetOrCreateUserLevelAsync(childId);

            var allLevels = await _context.Levels
                .OrderBy(l => l.LevelNumber)
                .ToListAsync();

            var childProgressMap = await _context.ChildChallengeProgresses
                .Where(cp => cp.ChildId == childId)
                .ToDictionaryAsync(cp => cp.ChallengeId);

            // Fetch child profile ONCE before the loop so we can compute
            // scoreMet for each level without repeated DB round-trips.
            // Uses the same FinalMeanScore fallback logic as EvaluateLevelUpAsync
            // so the journey UI and the actual gate always agree.
            var childProfile = await _context.ChildProfiles
                .FirstOrDefaultAsync(cp => cp.UserId == childId);
            double childCurrentScore = childProfile != null
                ? (childProfile.FinalMeanScore > 0
                    ? childProfile.FinalMeanScore
                    : childProfile.MeanScore)
                : 0;

            int childTrustScore = childProfile?.TrustScore ?? 0;
            double childMeanScore = childProfile?.MeanScore ?? 0;

            var levelJourneyCards = new List<JourneyLevelCard>();

            foreach (var lvl in allLevels)
            {
                Badge? levelBadge = null;
                if (lvl.LevelUpBadgeId.HasValue)
                {
                    levelBadge = await _context.Badges
                        .Include(b => b.Challenges.OrderBy(c => c.SortOrder))
                        .FirstOrDefaultAsync(b => b.Id == lvl.LevelUpBadgeId.Value);
                }

                var challengeCards = new List<JourneyChallengeCard>();
                if (levelBadge != null)
                {
                    foreach (var ch in levelBadge.Challenges)
                    {
                        childProgressMap.TryGetValue(ch.Id, out var prog);
                        challengeCards.Add(new JourneyChallengeCard
                        {
                            ChallengeId = ch.Id,
                            Emoji = ch.Emoji,
                            Text = ch.Description,
                            Current = prog?.CurrentValue ?? 0,
                            Target = ch.TargetValue,
                            Completed = prog?.IsCompleted ?? false
                        });
                    }
                }

                bool isDone = userLevel.LevelNumber > lvl.LevelNumber;
                bool isActive = userLevel.LevelNumber == lvl.LevelNumber;
                int completedCount = challengeCards.Count(c => c.Completed);
                int totalCount = challengeCards.Count;
                int progressPct = totalCount > 0
                    ? isDone ? 100 : (int)Math.Round((double)completedCount / totalCount * 100)
                    : isDone ? 100 : 0;

                int requiredBadges = await _levelRules.GetRequiredBadgeCountAsync(lvl);
                int minTrustScore = _levelRules.GetMinTrustScore(lvl.LevelNumber);

                // Spec-literal check (matches EvaluateLevelUpAsync): simple
                // total unlocked badge count >= this level's RequiredBadges.
                // Kept consistent with the actual level-up gate above instead
                // of the old challenge-completion-based check, so the
                // journey UI never shows a different "met" state than what
                // EvaluateLevelUpAsync will actually decide.
                int totalBadgeCount = await _context.ChildBadges
                    .CountAsync(cb => cb.ChildId == childId && cb.IsActive);
                bool requiredBadgesMet = isDone || totalBadgeCount >= lvl.RequiredBadges;

                // Score gate check — mirrors EvaluateLevelUpAsync exactly:
                // childCurrentScore (FinalMeanScore or MeanScore fallback, 0-100)
                // vs lvl.MinTrustScore × 100. isDone levels are always scoreMet.
                double requiredScoreForDisplay = lvl.MinTrustScore * 100.0;
                bool scoreMet = isDone || childCurrentScore >= requiredScoreForDisplay;

                levelJourneyCards.Add(new JourneyLevelCard
                {
                    LevelNumber = lvl.LevelNumber,
                    Name = lvl.Name,
                    Title = !string.IsNullOrEmpty(lvl.Title) ? lvl.Title : lvl.Name,
                    Emoji = lvl.Emoji,
                    Quote = lvl.Quote,
                    BgColor = lvl.CardBgColor,
                    TitleColor = lvl.CardTitleColor,
                    BarColor = lvl.BarColor,
                    IsDone = isDone,
                    IsActive = isActive,
                    IsLocked = !isDone && !isActive,
                    ProgressPct = progressPct,
                    Challenges = challengeCards,
                    RequiredBadges = requiredBadges,
                    MinTrustScore = minTrustScore,
                    MinTrustScoreNormalized = lvl.MinTrustScore > 0 ? lvl.MinTrustScore : minTrustScore / 100.0,
                    RequiredBadgesMet = requiredBadgesMet,
                    ScoreMet = scoreMet,
                    CurrentScore = Math.Round(childCurrentScore, 1)
                });
            }

            var activeLevelCard = levelJourneyCards.FirstOrDefault(l => l.IsActive);
            var nextLevelRequirements = activeLevelCard?.Challenges ?? new List<JourneyChallengeCard>();

            return new JourneyProgressData
            {
                CurrentLevel = userLevel.LevelNumber,
                CurrentLevelName = userLevel.Level?.Name ?? "The Sprout",
                ProgressPercentage = activeLevelCard?.ProgressPct ?? 0,
                CurrentExp = userLevel.CurrentExp,
                TotalExp = userLevel.TotalExp,
                TrustScore = childTrustScore,
                MeanScore = Math.Round(childMeanScore, 1),
                FinalMeanScore = Math.Round(childCurrentScore, 1),
                NextLevelRequirements = nextLevelRequirements,
                LevelDefinitions = levelJourneyCards
            };
        }
    }

    public class LevelUpResult
    {
        public bool LeveledUp { get; set; }
        public int OldLevel { get; set; }
        public int CurrentLevel { get; set; }
        public string LevelName { get; set; } = string.Empty;
        public int ExpAwarded { get; set; }
    }

    public class JourneyProgressData
    {
        public int CurrentLevel { get; set; }
        public string CurrentLevelName { get; set; } = string.Empty;
        public int ProgressPercentage { get; set; }
        public int CurrentExp { get; set; }
        public int TotalExp { get; set; }
        /// <summary>Computed or AI-set TrustScore (0–100).</summary>
        public int TrustScore { get; set; }
        /// <summary>Structured 8-pillar mean (MeanScore).</summary>
        public double MeanScore { get; set; }
        /// <summary>Hybrid final mean used for level-up gate.</summary>
        public double FinalMeanScore { get; set; }
        public List<JourneyChallengeCard> NextLevelRequirements { get; set; } = new();
        public List<JourneyLevelCard> LevelDefinitions { get; set; } = new();
    }

    public class JourneyLevelCard
    {
        public int LevelNumber { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>AI spec "title" field alias for Name.</summary>
        public string Title { get; set; } = string.Empty;

        public string Emoji { get; set; } = "⭐";
        public string Quote { get; set; } = string.Empty;
        public string BgColor { get; set; } = "#ffffff";
        public string TitleColor { get; set; } = "#333";
        public string BarColor { get; set; } = "#4caf50";
        public bool IsDone { get; set; }
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public int ProgressPct { get; set; }
        public List<JourneyChallengeCard> Challenges { get; set; } = new();

        // ── Spec-required fields (required_badges + min_trust_score) ──────
        public int RequiredBadges { get; set; }
        public int MinTrustScore { get; set; }

        /// <summary>
        /// AI spec: min_trust_score on 0.0-1.0 scale (Level.MinTrustScore).
        /// Exposed separately for AI team compatibility.
        /// </summary>
        public double MinTrustScoreNormalized { get; set; }

        public bool RequiredBadgesMet { get; set; }

        /// <summary>
        /// True when the child's current FinalMeanScore (or MeanScore fallback)
        /// meets or exceeds this level's MinTrustScore threshold.
        /// Mirrors the exact check in EvaluateLevelUpAsync so the UI and
        /// the actual gate are always in sync.
        /// Always true for already-completed levels (IsDone = true).
        /// </summary>
        public bool ScoreMet { get; set; }

        /// <summary>
        /// The child's current progress score (FinalMeanScore or MeanScore fallback),
        /// rounded to 1 decimal. Used by the frontend to show e.g. "28.5 / 40 needed".
        /// </summary>
        public double CurrentScore { get; set; }
    }

    public class JourneyChallengeCard
    {
        public int ChallengeId { get; set; }
        public string Emoji { get; set; } = "⭐";
        public string Text { get; set; } = string.Empty;
        public double Current { get; set; }
        public double Target { get; set; }
        public bool Completed { get; set; }
    }
}
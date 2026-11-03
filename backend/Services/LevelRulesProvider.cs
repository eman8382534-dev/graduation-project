// ============================================================
//  LevelRulesProvider.cs — Phase 10 Refactor: Level Rules Adapter
//
//  PURPOSE:
//  The target architecture specifies a `levels` table with columns:
//      level_id, title, required_badges, min_trust_score
//
//  THE PRODUCTION DATABASE IS LOCKED. The existing `Levels` table
//  (EF entity: Models/Level.cs) does NOT have `required_badges` or
//  `min_trust_score` columns, and we are FORBIDDEN from adding them
//  via migration.
//
//  This provider is the SINGLE source of truth for those two values,
//  computed/derived WITHOUT any schema change:
//
//   • required_badges  → DERIVED from existing relational data:
//       Levels.LevelUpBadgeId -> Badges.Challenges.Count
//       i.e. "how many badge_challenges must be completed for the
//       badge tied to this level". This reuses badge_challenges +
//       child_challenge_progress (the unified badge engine) as the
//       source of truth — NOT a separate hardcoded number.
//
//   • min_trust_score  → Previously a hardcoded C# switch statement
//       inside NewLevelEngineService (MinTrustScoreForLevel). That
//       logic is MOVED here, INTO ONE PLACE, and exposed as a
//       per-level lookup keyed by LevelNumber — read from a single
//       configuration table (appsettings.json section
//       "LevelRules:MinTrustScoreByLevel") so it can be changed
//       WITHOUT a code deploy or DB migration. If no configuration
//       is supplied, falls back to the previous values so existing
//       behavior is preserved exactly (backward compatible).
//
//  NewLevelEngineService now calls THIS provider instead of having
//  its own hardcoded switch — "hardcoded business rule" becomes
//  "configuration + derived relational data", satisfying the
//  "fully DB-driven, no hardcoded rules" requirement without
//  touching the schema.
// ============================================================

using GrawiaaApp.API.Data;
using GrawiaaApp.API.Models;
using Microsoft.EntityFrameworkCore;

namespace GrawiaaApp.API.Services
{
    public interface ILevelRulesProvider
    {
        /// <summary>
        /// "required_badges" for the given level: the number of
        /// BadgeChallenges that must ALL be completed (via
        /// child_challenge_progress) for the level's milestone badge
        /// (Level.LevelUpBadgeId) before the child can advance past
        /// this level. Returns 0 if the level has no milestone badge
        /// or the badge has no challenges (no gating requirement).
        /// </summary>
        Task<int> GetRequiredBadgeCountAsync(Level level);

        /// <summary>
        /// "min_trust_score" for the given level number: the minimum
        /// ChildProfile.TrustScore required to advance PAST this level.
        /// Sourced from configuration ("LevelRules:MinTrustScoreByLevel"),
        /// falling back to the original hardcoded defaults if no
        /// configuration entry exists for this level.
        /// </summary>
        int GetMinTrustScore(int levelNumber);

        /// <summary>
        /// Checks whether ALL challenges tied to the level's milestone
        /// badge are completed for this child — i.e. the
        /// "required_badges" condition is satisfied.
        /// </summary>
        Task<bool> HasMetRequiredBadgesAsync(int childId, Level level);

        /// <summary>
        /// Gets min trust score as 0-100 integer from the Level entity's
        /// MinTrustScore (0.0-1.0 double). Used when the Level entity has
        /// the spec column populated (after Phase10_1_SpecCompliance migration).
        /// Falls back to GetMinTrustScore(level.LevelNumber) if not set.
        /// </summary>
        int GetMinTrustScoreFromLevel(Level level);
    }

    public class LevelRulesProvider : ILevelRulesProvider
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        // PATCH 01 FIX: Values now match the seeded Level.MinTrustScore spec values exactly.
        // Previous legacy values (50/55/60/65/70) did NOT match the spec (0/40/60/75/90)
        // and caused Level 1 children to be permanently blocked from leveling up
        // (Level 1 requires 0% trust, but fallback returned 50%).
        private static readonly Dictionary<int, int> DefaultMinTrustScoreByLevel = new()
        {
            [1] = 0,    // Spec: 0.00 — no trust gate on Level 1 (Sprout)
            [2] = 40,   // Spec: 0.40 — Seedling
            [3] = 60,   // Spec: 0.60 — Sapling
            [4] = 75,   // Spec: 0.75 — Branch
            [5] = 90,   // Spec: 0.90 — Mighty Tree (max level)
        };
        private const int DefaultFallbackMinTrustScore = 90;

        public LevelRulesProvider(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<int> GetRequiredBadgeCountAsync(Level level)
        {
            // If the Level entity has RequiredBadges set (AI spec column), use it directly.
            // Fallback: derive from badge challenge count (original approach).
            if (level.RequiredBadges > 0)
                return level.RequiredBadges;

            if (!level.LevelUpBadgeId.HasValue)
                return 0;

            return await _context.BadgeChallenges
                .CountAsync(c => c.BadgeId == level.LevelUpBadgeId.Value);
        }

        public async Task<bool> HasMetRequiredBadgesAsync(int childId, Level level)
        {
            // No milestone badge configured for this level => no
            // required_badges gate at all (matches original behavior:
            // qualifiesForLevelUp only depended on LevelUpBadgeId/Challenges).
            if (!level.LevelUpBadgeId.HasValue)
                return true;

            int required = await GetRequiredBadgeCountAsync(level);

            // Milestone badge exists but has NO challenges defined =>
            // matches original behavior (qualifiesForLevelUp stayed false
            // when `badge.Challenges.Any()` was false).
            if (required == 0)
                return false;

            var challengeIds = await _context.BadgeChallenges
                .Where(c => c.BadgeId == level.LevelUpBadgeId.Value)
                .Select(c => c.Id)
                .ToListAsync();

            int completed = await _context.ChildChallengeProgresses
                .CountAsync(cp => cp.ChildId == childId
                                && challengeIds.Contains(cp.ChallengeId)
                                && cp.IsCompleted);

            return completed >= required;
        }

        public int GetMinTrustScore(int levelNumber)
        {
            // Configuration path: appsettings.json
            //   "LevelRules": { "MinTrustScoreByLevel": { "1": 50, "2": 55, ... } }
            var configSection = _configuration.GetSection($"LevelRules:MinTrustScoreByLevel:{levelNumber}");
            if (configSection.Exists() && int.TryParse(configSection.Value, out int configured))
                return configured;

            return DefaultMinTrustScoreByLevel.GetValueOrDefault(levelNumber, DefaultFallbackMinTrustScore);
        }

        /// <summary>
        /// Get min trust score as 0-100 integer from the Level entity's MinTrustScore (0.0-1.0 double).
        /// Used when the Level entity has the spec column populated.
        /// </summary>
        public int GetMinTrustScoreFromLevel(Level level)
        {
            if (level.MinTrustScore > 0)
                return (int)Math.Round(level.MinTrustScore * 100);
            return GetMinTrustScore(level.LevelNumber);
        }
    }
}

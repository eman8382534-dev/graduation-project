// ============================================================
//  BadgeLevelController.cs — Badge + Level System API (Phase 10)
//
//  Provides the spec-required API endpoints that were missing
//  from the existing ChildController badge/level routes.
//
//  New endpoints (project convention: /api/BadgeLevel/...):
//
//  GET  /api/BadgeLevel/badges            → all badges (list)
//  GET  /api/BadgeLevel/badges/my         → child's earned badges
//  GET  /api/BadgeLevel/badges/{id}       → single badge with challenges
//  GET  /api/BadgeLevel/levels            → all levels with requirements
//  GET  /api/BadgeLevel/levels/me         → child's current level
//  GET  /api/BadgeLevel/challenges        → all badge challenges
//  GET  /api/BadgeLevel/progress          → child's challenge progress
//  POST /api/BadgeLevel/badge/evaluate    → force re-evaluate badges
//  POST /api/BadgeLevel/badge/recalculate → full recalculation
//  POST /api/BadgeLevel/level/check       → check + trigger level-up
//
//  All child-facing endpoints require [Authorize(Roles="Child")].
//  badge/evaluate and badge/recalculate also accept the AI API key.
// ============================================================

using GrawiaaApp.API.Data;
using GrawiaaApp.API.DTOs;
using GrawiaaApp.API.Models;
using GrawiaaApp.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GrawiaaApp.API.Controllers
{
    [Route("api/BadgeLevel")]
    [ApiController]
    public class BadgeLevelController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly INewProgressionService _progression;
        private readonly INewLevelEngineService _levelEngine;
        private readonly INewBadgeEngineService _badgeEngine;
        private readonly IChildIdentityService _childIdentity;
        private readonly IConfiguration _config;
        private readonly ILogger<BadgeLevelController> _logger;

        public BadgeLevelController(
            ApplicationDbContext context,
            INewProgressionService progression,
            INewLevelEngineService levelEngine,
            INewBadgeEngineService badgeEngine,
            IChildIdentityService childIdentity,
            IConfiguration config,
            ILogger<BadgeLevelController> logger)
        {
            _context = context;
            _progression = progression;
            _levelEngine = levelEngine;
            _badgeEngine = badgeEngine;
            _childIdentity = childIdentity;
            _config = config;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/BadgeLevel/badges
        // All badges with their challenges (no auth — catalog view)
        // ─────────────────────────────────────────────────────────────
        [HttpGet("badges")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllBadges()
        {
            var badges = await _context.Badges
                .Include(b => b.Challenges.OrderBy(c => c.SortOrder))
                .OrderBy(b => b.Id)
                .ToListAsync();

            var result = badges.Select(b => new
            {
                badge_id = b.Id,
                badge_name = b.Name,
                description = b.Description,
                icon_url = b.IconUrl,
                category = b.Category.ToString(),
                personality_side = b.PersonalitySide,
                // Spec-required pillar percentage fields — now read directly from Badge columns
                // (acad_pct, soc_pct, ment_pct, phys_pct, spir_pct, env_pct, crea_pct, res_pct).
                // For pillar badges (IDs 28-51) exactly one will be non-zero.
                // For non-pillar badges (streak, task, level) all remain 0.
                acad_pct = b.AcadPct,
                soc_pct = b.SocPct,
                ment_pct = b.MentPct,
                phys_pct = b.PhysPct,
                spir_pct = b.SpirPct,
                env_pct = b.EnvPct,
                crea_pct = b.CreaPct,
                res_pct = b.ResPct,
                challenges = b.Challenges.Select(c => new
                {
                    challenge_id = c.Id,
                    challenge_text = c.Description,
                    emoji = c.Emoji,
                    metric_type = c.MetricType,
                    metric_sub_type = c.MetricSubType,
                    target_value = c.TargetValue,
                    sort_order = c.SortOrder
                }).ToList()
            }).ToList();

            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/BadgeLevel/badges/my
        // Child's earned badges (requires auth)
        // ─────────────────────────────────────────────────────────────
        [HttpGet("badges/my")]
        [Authorize(Roles = "Child")]
        public async Task<IActionResult> GetMyBadges()
        {
            if (!TryGetChildId(out int childId))
                return Unauthorized("Invalid token claims");

            var earned = await _context.ChildBadges
                .Include(cb => cb.Badge)
                    .ThenInclude(b => b.Challenges.OrderBy(c => c.SortOrder))
                .Where(cb => cb.ChildId == childId && cb.IsActive)
                .OrderByDescending(cb => cb.UnlockedAt)
                .ToListAsync();

            var result = earned.Select(cb => new
            {
                badge_id = cb.BadgeId,
                badge_name = cb.Badge.Name,
                description = cb.Badge.Description,
                icon_url = cb.Badge.IconUrl,
                category = cb.Badge.Category.ToString(),
                personality_side = cb.Badge.PersonalitySide,
                unlocked_at = cb.UnlockedAt,
                is_active = cb.IsActive,
                challenges = cb.Badge.Challenges.Select(c => new
                {
                    challenge_id = c.Id,
                    challenge_text = c.Description,
                    emoji = c.Emoji,
                    metric_type = c.MetricType,
                    target_value = c.TargetValue
                }).ToList()
            }).ToList();

            return Ok(new
            {
                count = result.Count,
                hashed_child_id = _childIdentity.GetSafeChildKey(childId),
                badges = result
            });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/BadgeLevel/badges/recommended
        // Phase 13 — Returns the AI-recommended badge for this child.
        // IMPORTANT: This is a RECOMMENDATION only — it does NOT auto-
        // unlock. The badge is earned only when all challenges complete.
        // Distinct from earned badges; frontend must show it differently.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("badges/recommended")]
        [Authorize(Roles = "Child")]
        public async Task<IActionResult> GetRecommendedBadge()
        {
            if (!TryGetChildId(out int childId))
                return Unauthorized("Invalid token claims");

            var profile = await _context.ChildProfiles
                .Include(c => c.RecommendedBadge)
                    .ThenInclude(b => b!.Challenges.OrderBy(c => c.SortOrder))
                .FirstOrDefaultAsync(c => c.UserId == childId);

            if (profile?.RecommendedBadge is null)
                return Ok(new { hasRecommendation = false });

            // Check if the child has already earned this badge
            bool alreadyUnlocked = await _context.ChildBadges
                .AnyAsync(cb => cb.ChildId == childId
                             && cb.BadgeId == profile.RecommendedBadgeId
                             && cb.IsActive);

            // Get child's challenge progress for this badge
            var challengeIds = profile.RecommendedBadge.Challenges.Select(c => c.Id).ToList();
            var progressMap = await _context.ChildChallengeProgresses
                .Where(cp => cp.ChildId == childId && challengeIds.Contains(cp.ChallengeId))
                .ToDictionaryAsync(cp => cp.ChallengeId);

            return Ok(new
            {
                hasRecommendation = true,
                setAt = profile.RecommendedBadgeSetAt,
                alreadyUnlocked,
                badge = new
                {
                    badge_id = profile.RecommendedBadge.Id,
                    badge_name = profile.RecommendedBadge.Name,
                    description = profile.RecommendedBadge.Description,
                    icon_url = profile.RecommendedBadge.IconUrl,
                    category = profile.RecommendedBadge.Category.ToString(),
                    // Pillar targets (AI team spec columns)
                    acad_pct = profile.RecommendedBadge.AcadPct,
                    soc_pct = profile.RecommendedBadge.SocPct,
                    ment_pct = profile.RecommendedBadge.MentPct,
                    phys_pct = profile.RecommendedBadge.PhysPct,
                    spir_pct = profile.RecommendedBadge.SpirPct,
                    env_pct = profile.RecommendedBadge.EnvPct,
                    crea_pct = profile.RecommendedBadge.CreaPct,
                    res_pct = profile.RecommendedBadge.ResPct,
                    challenges = profile.RecommendedBadge.Challenges.Select(c =>
                    {
                        progressMap.TryGetValue(c.Id, out var prog);
                        return new
                        {
                            challenge_id = c.Id,
                            challenge_text = c.Description,
                            emoji = c.Emoji,
                            metric_type = c.MetricType,
                            target_value = c.TargetValue,
                            current_value = prog?.CurrentValue ?? 0,
                            is_completed = prog?.IsCompleted ?? false
                        };
                    }).ToList()
                }
            });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/BadgeLevel/badges/{id}
        // Single badge detail with challenges and child progress
        // ─────────────────────────────────────────────────────────────
        [HttpGet("badges/{id:int}")]
        [Authorize(Roles = "Child")]
        public async Task<IActionResult> GetBadgeById(int id)
        {
            if (!TryGetChildId(out int childId))
                return Unauthorized("Invalid token claims");

            var badge = await _context.Badges
                .Include(b => b.Challenges.OrderBy(c => c.SortOrder))
                .FirstOrDefaultAsync(b => b.Id == id);

            if (badge == null)
                return NotFound(new { error = $"Badge with id={id} not found." });

            // Check if child has earned this badge
            var childBadge = await _context.ChildBadges
                .FirstOrDefaultAsync(cb => cb.ChildId == childId && cb.BadgeId == id && cb.IsActive);

            bool isUnlocked = childBadge != null;

            // Get child's progress on each challenge
            var challengeIds = badge.Challenges.Select(c => c.Id).ToList();
            var progressMap = await _context.ChildChallengeProgresses
                .Where(cp => cp.ChildId == childId && challengeIds.Contains(cp.ChallengeId))
                .ToDictionaryAsync(cp => cp.ChallengeId);

            var challenges = badge.Challenges.Select(c =>
            {
                progressMap.TryGetValue(c.Id, out var prog);
                double current = prog?.CurrentValue ?? 0;
                bool done = prog?.IsCompleted ?? false;
                return new
                {
                    challenge_id = c.Id,
                    challenge_text = c.Description,
                    emoji = c.Emoji,
                    metric_type = c.MetricType,
                    metric_sub_type = c.MetricSubType,
                    target_value = c.TargetValue,
                    current_value = current,
                    is_completed = done,
                    unlocked_at = prog?.UnlockedAt,
                    progress_pct = c.TargetValue > 0
                        ? Math.Min(100, Math.Round(current / c.TargetValue * 100, 1))
                        : (done ? 100.0 : 0.0)
                };
            }).ToList();

            int completedCount = challenges.Count(c => c.is_completed);
            double overallProgress = badge.Challenges.Any()
                ? Math.Round((double)completedCount / badge.Challenges.Count * 100, 1)
                : (isUnlocked ? 100.0 : 0.0);

            return Ok(new
            {
                badge_id = badge.Id,
                badge_name = badge.Name,
                description = badge.Description,
                icon_url = badge.IconUrl,
                category = badge.Category.ToString(),
                personality_side = badge.PersonalitySide,
                is_unlocked = isUnlocked,
                unlocked_at = childBadge?.UnlockedAt,
                overall_progress_pct = overallProgress,
                challenges_completed = completedCount,
                challenges_total = badge.Challenges.Count,
                hashed_child_id = _childIdentity.GetSafeChildKey(childId),
                challenges = challenges
            });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/BadgeLevel/levels
        // All levels with their requirements (public catalog)
        // ─────────────────────────────────────────────────────────────
        [HttpGet("levels")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllLevels()
        {
            var levels = await _context.Levels
                .OrderBy(l => l.LevelNumber)
                .ToListAsync();

            var result = levels.Select(l => new
            {
                level_id = l.Id,
                level_number = l.LevelNumber,
                title = l.Title,
                name = l.Name,
                required_badges = l.RequiredBadges,
                min_trust_score = l.MinTrustScore,
                emoji = l.Emoji,
                quote = l.Quote,
                exp_reward = l.ExpReward,
                level_up_badge_id = l.LevelUpBadgeId,
                card_bg_color = l.CardBgColor,
                card_title_color = l.CardTitleColor,
                bar_color = l.BarColor
            }).ToList();

            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/BadgeLevel/levels/me
        // Child's current level with progress details
        // ─────────────────────────────────────────────────────────────
        [HttpGet("levels/me")]
        [Authorize(Roles = "Child")]
        public async Task<IActionResult> GetMyLevel()
        {
            if (!TryGetChildId(out int childId))
                return Unauthorized("Invalid token claims");

            var userLevel = await _levelEngine.GetOrCreateUserLevelAsync(childId);
            var journeyData = await _levelEngine.GetJourneyProgressAsync(childId);

            // Get child's trust score
            var profile = await _context.ChildProfiles
                .FirstOrDefaultAsync(cp => cp.UserId == childId);
            double trustScoreNormalized = profile != null ? profile.TrustScore / 100.0 : 1.0;
            // PATCH 08: Also expose integer trust score (0-100) for frontend display
            int trustScoreInt = profile?.TrustScore ?? 100;
            // Phase 13: FinalMeanScore is the canonical gate score (70% structured + 30% NLP)
            double finalMeanScore = profile != null
                ? (profile.FinalMeanScore > 0 ? profile.FinalMeanScore : profile.MeanScore)
                : 0;

            // Get the next level for requirements display
            var nextLevel = await _context.Levels
                .FirstOrDefaultAsync(l => l.LevelNumber == userLevel.LevelNumber + 1);

            return Ok(new
            {
                hashed_child_id = _childIdentity.GetSafeChildKey(childId),
                current_level_id = userLevel.LevelId,
                current_level_number = userLevel.LevelNumber,
                current_level_name = userLevel.Level?.Name ?? "The Sprout",
                current_level_title = userLevel.Level?.Title ?? "Sprout",
                current_exp = userLevel.CurrentExp,
                total_exp = userLevel.TotalExp,
                last_level_up_at = userLevel.LastLevelUpAt,
                trust_score = trustScoreNormalized,       // 0.0-1.0 for AI team spec
                trust_score_pct = trustScoreInt,          // PATCH 08: 0-100 for frontend display
                final_mean_score = Math.Round(finalMeanScore, 1), // Phase 13: gate score used for level-up
                progress_percentage = journeyData.ProgressPercentage,
                next_level = nextLevel == null ? null : new
                {
                    level_id = nextLevel.Id,
                    level_number = nextLevel.LevelNumber,
                    title = nextLevel.Title,
                    name = nextLevel.Name,
                    required_badges = nextLevel.RequiredBadges,
                    min_trust_score = nextLevel.MinTrustScore
                }
            });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/BadgeLevel/challenges
        // All badge challenges (public catalog)
        // ─────────────────────────────────────────────────────────────
        [HttpGet("challenges")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllChallenges()
        {
            var challenges = await _context.BadgeChallenges
                .Include(c => c.Badge)
                .OrderBy(c => c.BadgeId)
                .ThenBy(c => c.SortOrder)
                .ToListAsync();

            var result = challenges.Select(c => new
            {
                challenge_id = c.Id,
                badge_id = c.BadgeId,
                badge_name = c.Badge?.Name,
                challenge_text = c.Description,
                emoji = c.Emoji,
                metric_type = c.MetricType,
                metric_sub_type = c.MetricSubType,
                target_value = c.TargetValue,
                sort_order = c.SortOrder
            }).ToList();

            return Ok(new { count = result.Count, challenges = result });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/BadgeLevel/progress
        // Child's current progress on all challenges
        // ─────────────────────────────────────────────────────────────
        [HttpGet("progress")]
        [Authorize(Roles = "Child")]
        public async Task<IActionResult> GetMyProgress()
        {
            if (!TryGetChildId(out int childId))
                return Unauthorized("Invalid token claims");

            var progress = await _context.ChildChallengeProgresses
                .Include(cp => cp.Challenge)
                    .ThenInclude(c => c.Badge)
                .Where(cp => cp.ChildId == childId)
                .OrderBy(cp => cp.ChallengeId)
                .ToListAsync();

            var hashedChildId = _childIdentity.GetSafeChildKey(childId);

            var result = progress.Select(p => new
            {
                progress_id = p.Id,
                hashed_child_id = hashedChildId,
                challenge_id = p.ChallengeId,
                challenge_text = p.Challenge?.Description,
                badge_id = p.Challenge?.BadgeId,
                badge_name = p.Challenge?.Badge?.Name,
                current_value = p.CurrentValue,
                target_value = p.Challenge?.TargetValue ?? 0,
                is_completed = p.IsCompleted,
                unlocked_at = p.UnlockedAt,
                last_updated_at = p.LastUpdatedAt,
                progress_pct = p.Challenge?.TargetValue > 0
                    ? Math.Min(100, Math.Round(p.CurrentValue / p.Challenge.TargetValue * 100, 1))
                    : (p.IsCompleted ? 100.0 : 0.0)
            }).ToList();

            return Ok(new
            {
                hashed_child_id = hashedChildId,
                total_challenges = result.Count,
                completed_challenges = result.Count(r => r.is_completed),
                progress = result
            });
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/BadgeLevel/badge/evaluate
        // Trigger badge evaluation for the authenticated child.
        // Also accepts AI API key for server-to-server calls.
        // ─────────────────────────────────────────────────────────────
        [HttpPost("badge/evaluate")]
        [Authorize(Roles = "Child")]
        public async Task<IActionResult> EvaluateBadges()
        {
            if (!TryGetChildId(out int childId))
                return Unauthorized("Invalid token claims");

            _logger.LogInformation("[BadgeEval] Manual evaluation triggered for ChildId={ChildId}", childId);

            var result = await _progression.RunAsync(childId);

            return Ok(new
            {
                message = "Badge evaluation completed.",
                hashed_child_id = _childIdentity.GetSafeChildKey(childId),
                newly_unlocked_badge_ids = result.NewlyUnlockedBadgeIds,
                newly_unlocked_count = result.NewlyUnlockedBadgeIds.Count,
                leveled_up = result.LeveledUp,
                current_level = result.CurrentLevel,
                level_name = result.LevelName,
                exp_awarded = result.ExpAwarded,
                has_error = result.HasError,
                error_message = result.ErrorMessage
            });
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/BadgeLevel/badge/recalculate
        // Full recalculation of challenge progress + badge evaluation.
        // Useful when metrics may have changed outside the normal flow.
        // Accepts child JWT OR AI API key.
        // ─────────────────────────────────────────────────────────────
        [HttpPost("badge/recalculate")]
        [Authorize(Roles = "Child")]
        public async Task<IActionResult> RecalculateBadges()
        {
            if (!TryGetChildId(out int childId))
                return Unauthorized("Invalid token claims");

            _logger.LogInformation("[BadgeRecalc] Full recalculation for ChildId={ChildId}", childId);

            // BUG FIX (Phase 11): Previously only ChildChallengeProgresses were deleted,
            // leaving ChildBadges intact. After reruns where the child's current metrics
            // no longer satisfy completed challenges (e.g. streak broke), this created
            // R4/R10 integrity violations: badges in ChildBadges with incomplete challenges.
            //
            // Fix: also delete challenge-gated ChildBadges (those that have at least one
            // BadgeChallenge). LevelUpBadges (auto-awarded on level-up) are NOT deleted —
            // they are awarded by the level engine, not the challenge engine.
            var levelUpBadgeIds = await _context.Levels
                .Where(l => l.LevelUpBadgeId != null)
                .Select(l => l.LevelUpBadgeId!.Value)
                .ToListAsync();

            var challengeBasedBadgeIds = await _context.BadgeChallenges
                .Select(bc => bc.BadgeId)
                .Distinct()
                .ToListAsync();

            // Only remove badges that are challenge-gated AND not a LevelUpBadge
            var badgesToRemove = challengeBasedBadgeIds
                .Where(id => !levelUpBadgeIds.Contains(id))
                .ToList();

            var existingBadgeRecords = await _context.ChildBadges
                .Where(cb => cb.ChildId == childId && badgesToRemove.Contains(cb.BadgeId))
                .ToListAsync();

            // Delete existing progress records so RunAsync rebuilds from scratch
            var existingProgress = await _context.ChildChallengeProgresses
                .Where(cp => cp.ChildId == childId)
                .ToListAsync();

            _context.ChildBadges.RemoveRange(existingBadgeRecords);
            _context.ChildChallengeProgresses.RemoveRange(existingProgress);
            await _context.SaveChangesAsync();

            // Re-run full progression
            var result = await _progression.RunAsync(childId);

            return Ok(new
            {
                message = "Full badge recalculation completed.",
                hashed_child_id = _childIdentity.GetSafeChildKey(childId),
                challenge_records_rebuilt = existingProgress.Count,
                challenge_badges_reset = existingBadgeRecords.Count,
                newly_unlocked_badge_ids = result.NewlyUnlockedBadgeIds,
                newly_unlocked_count = result.NewlyUnlockedBadgeIds.Count,
                leveled_up = result.LeveledUp,
                current_level = result.CurrentLevel,
                level_name = result.LevelName,
                exp_awarded = result.ExpAwarded,
                has_error = result.HasError,
                error_message = result.ErrorMessage
            });
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/BadgeLevel/level/check
        // Explicitly check if the child qualifies for a level-up.
        // Spec: check_for_level_up(hashed_child_id)
        // ─────────────────────────────────────────────────────────────
        [HttpPost("level/check")]
        [Authorize(Roles = "Child")]
        public async Task<IActionResult> CheckLevelUp()
        {
            if (!TryGetChildId(out int childId))
                return Unauthorized("Invalid token claims");

            var levelResult = await _levelEngine.CheckForLevelUpAsync(childId);
            var userLevel = await _levelEngine.GetOrCreateUserLevelAsync(childId);

            // Get trust score for spec-required fields
            var profile = await _context.ChildProfiles
                .FirstOrDefaultAsync(cp => cp.UserId == childId);
            double trustScore = profile != null ? profile.TrustScore / 100.0 : 1.0;

            // Get badge count for spec fields
            int badgeCount = await _context.ChildBadges
                .CountAsync(cb => cb.ChildId == childId && cb.IsActive);

            return Ok(new
            {
                message = levelResult.LeveledUp ? $"Congrats! You are now a {levelResult.LevelName}! 🎉" : "Keep going! You're getting closer.",
                hashed_child_id = _childIdentity.GetSafeChildKey(childId),
                leveled_up = levelResult.LeveledUp,
                old_level = levelResult.OldLevel,
                current_level = levelResult.CurrentLevel,
                current_level_name = levelResult.LevelName,
                current_badge_count = badgeCount,
                current_trust_score = trustScore,
                exp_awarded = levelResult.ExpAwarded
            });
        }

        // ─────────────────────────────────────────────────────────────
        // AI Team: POST /api/BadgeLevel/badge/evaluate-by-hash
        // Called by AI team using hashed_child_id (no JWT required,
        // uses X-AI-Api-Key header instead).
        // ─────────────────────────────────────────────────────────────
        [HttpPost("badge/evaluate-by-hash")]
        [AllowAnonymous]
        public async Task<IActionResult> EvaluateBadgesByHash([FromBody] EvaluateByHashDto dto)
        {
            if (!ValidateAiApiKey())
                return Unauthorized(new { error = "Invalid or missing AI API key." });

            if (string.IsNullOrWhiteSpace(dto?.HashedChildId))
                return BadRequest(new { error = "hashed_child_id is required." });

            // Find ChildId by matching HashedChildId in UserLevels
            var userLevel = await _context.UserLevels
                .FirstOrDefaultAsync(ul => ul.HashedChildId == dto.HashedChildId);

            if (userLevel == null)
                return NotFound(new { error = $"No user found with hashed_child_id={dto.HashedChildId}" });

            int childId = userLevel.ChildId;
            var result = await _progression.RunAsync(childId);

            return Ok(new
            {
                message = "Badge evaluation completed via hashed_child_id.",
                hashed_child_id = dto.HashedChildId,
                newly_unlocked_badge_ids = result.NewlyUnlockedBadgeIds,
                leveled_up = result.LeveledUp,
                current_level = result.CurrentLevel,
                has_error = result.HasError
            });
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────
        private bool TryGetChildId(out int childId)
        {
            childId = 0;
            var str = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return !string.IsNullOrEmpty(str) && int.TryParse(str, out childId);
        }

        private bool ValidateAiApiKey()
        {
            if (!Request.Headers.TryGetValue("X-AI-Api-Key", out var key))
                return false;
            var expected = _config["AiTeam:ApiKey"];
            return !string.IsNullOrEmpty(expected) && key == expected;
        }
    }

    /// <summary>Request body for AI-team badge evaluation via hashed_child_id.</summary>
    public class EvaluateByHashDto
    {
        public string HashedChildId { get; set; } = string.Empty;
    }
}
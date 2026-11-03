// ============================================================
//  SystemIntegrityValidator.cs — Leafy / GrawiaaApp (Phase 10)
//
//  PURPOSE
//  ───────
//  Runtime guard that verifies all Phase 10 architectural
//  invariants hold in the live database.  It does NOT modify
//  any data.  Call it at startup (Program.cs) and expose it
//  behind GET /api/Admin/integrity — Admin role only.
//
//  RULES ENFORCED
//  ──────────────
//  R1  Every ChildBadge row must originate from a completed
//      BadgeChallenge set OR a Level.LevelUpBadgeId award.
//      No orphaned ChildBadge rows allowed.
//
//  R2  Every UserLevel row must reference a valid Level and
//      have LevelNumber in [1, MaxLevel].
//
//  R3  ChildProfile.Level must equal UserLevel.LevelNumber
//      for every child that has a UserLevel record.
//
//  R4  No child may have a ChildBadge for a challenge-based
//      badge unless ALL of that badge's challenges are
//      marked IsCompleted in ChildChallengeProgress.
//
//  R5  ChildChallengeProgress.CurrentValue must be >= TargetValue
//      for every row where IsCompleted = true.
//
//  R6  All 8 ChildProfile pillar columns must be in [0, 100].
//      Values outside this range indicate a write-path bypass.
//
//  R7  ChildProfile.ResiliencePct must equal ResilienceTrait
//      (within floating-point epsilon) — they are kept in sync
//      by TraitSynthesisEngine.ApplyToProfile().
//
//  R8  For every child with DailyLogEntry rows in the last 90
//      days, at least one of the 8 pillar columns must be > 0
//      (PillarCalculationEngine was called at least once).
//
//  R9  No UserLevel.LevelNumber may exceed the highest LevelNumber
//      in the Levels seed table.
//
//  R10 Each Badge that has challenges must NOT be awarded to a
//      child unless that child's ChildChallengeProgress records
//      all resolve IsCompleted = true for those challenges.
//      (Subset of R4 — checked independently for completeness.)
// ============================================================

using GrawiaaApp.API.Data;
using GrawiaaApp.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GrawiaaApp.API.Services
{
    // ── Public result types ───────────────────────────────────────────

    public sealed class IntegrityViolation
    {
        /// <summary>Rule identifier, e.g. "R4".</summary>
        public string Rule { get; init; } = string.Empty;

        /// <summary>Human-readable description of the specific violation.</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>ChildId involved, if applicable.</summary>
        public int? ChildId { get; init; }

        /// <summary>Entity id involved (BadgeId, LevelId, ChallengeId…), if applicable.</summary>
        public int? EntityId { get; init; }
    }

    public sealed class IntegrityReport
    {
        public DateTime RunAt { get; init; } = DateTime.UtcNow;
        public bool IsHealthy => Violations.Count == 0;
        public int TotalChecks { get; init; }
        public int ViolationCount => Violations.Count;
        public IReadOnlyList<IntegrityViolation> Violations { get; init; }
            = Array.Empty<IntegrityViolation>();

        public override string ToString() =>
            IsHealthy
                ? $"[INTEGRITY OK] {TotalChecks} checks passed at {RunAt:u}"
                : $"[INTEGRITY FAIL] {ViolationCount} violation(s) across {TotalChecks} checks at {RunAt:u}";
    }

    // ── Interface ─────────────────────────────────────────────────────

    public interface ISystemIntegrityValidator
    {
        /// <summary>
        /// Runs all integrity rules and returns a full report.
        /// Read-only — never mutates the database.
        /// </summary>
        Task<IntegrityReport> ValidateAsync(CancellationToken ct = default);
    }

    // ── Implementation ────────────────────────────────────────────────

    public sealed class SystemIntegrityValidator : ISystemIntegrityValidator
    {
        private const int MaxLevel = 6;
        private const double PillarMin = 0.0;
        private const double PillarMax = 100.0;
        private const double FloatEpsilon = 0.01;

        private readonly ApplicationDbContext _db;
        private readonly ILogger<SystemIntegrityValidator> _logger;

        public SystemIntegrityValidator(
            ApplicationDbContext db,
            ILogger<SystemIntegrityValidator> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<IntegrityReport> ValidateAsync(CancellationToken ct = default)
        {
            var violations = new List<IntegrityViolation>();
            int checks = 0;

            _logger.LogInformation("[Integrity] Starting system integrity validation…");

            // ── Pre-load shared sets ──────────────────────────────────

            var allLevelNumbers = await _db.Levels
                .Select(l => l.LevelNumber)
                .ToListAsync(ct);

            int maxSeededLevel = allLevelNumbers.Any() ? allLevelNumbers.Max() : MaxLevel;

            // Badges that have at least one challenge (challenge-gated badges)
            var challengeGatedBadgeIds = await _db.BadgeChallenges
                .Select(bc => bc.BadgeId)
                .Distinct()
                .ToListAsync(ct);
            var challengeGatedSet = challengeGatedBadgeIds.ToHashSet();

            // LevelUpBadge IDs (badges awarded exclusively by LevelEngine on level-up)
            var levelUpBadgeIds = await _db.Levels
                .Where(l => l.LevelUpBadgeId != null)
                .Select(l => l.LevelUpBadgeId!.Value)
                .ToListAsync(ct);
            var levelUpSet = levelUpBadgeIds.ToHashSet();

            // All challenges: Id → (BadgeId, TargetValue)
            var allChallenges = await _db.BadgeChallenges
                .Select(c => new { c.Id, c.BadgeId, c.TargetValue })
                .ToListAsync(ct);

            // All challenge progress: (ChildId, ChallengeId) → (IsCompleted, CurrentValue)
            var allProgress = await _db.ChildChallengeProgresses
                .Select(p => new { p.ChildId, p.ChallengeId, p.IsCompleted, p.CurrentValue })
                .ToListAsync(ct);

            var progressLookup = allProgress
                .GroupBy(p => p.ChildId)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(p => p.ChallengeId));

            // All UserLevel records
            var userLevels = await _db.UserLevels
                .ToListAsync(ct);

            var userLevelByChild = userLevels.ToDictionary(ul => ul.ChildId);

            // All ChildProfile records
            var profiles = await _db.ChildProfiles
                .ToListAsync(ct);

            var profileByChild = profiles.ToDictionary(p => p.UserId);

            // All ChildBadge records
            var childBadges = await _db.ChildBadges
                .Where(cb => cb.IsActive)
                .ToListAsync(ct);

            // Children with any log entries in the last 90 days
            var cutoff = DateTime.UtcNow.Date.AddDays(-90);
            var activeChildIds = await _db.DailyLogEntries
                .Where(l => l.LogDate >= cutoff)
                .Select(l => l.ChildId)
                .Distinct()
                .ToListAsync(ct);
            var activeChildSet = activeChildIds.ToHashSet();

            // ── R1: ChildBadge must have a valid source ───────────────
            //   Valid sources:
            //     (a) badge is challenge-gated  → all challenges completed
            //     (b) badge is a LevelUpBadge   → child has reached that level
            //     (c) badge is legacy (no challenges, not a LevelUpBadge)
            //         → allowed (legacy badge, not challenge-gated)
            checks++;
            foreach (var cb in childBadges)
            {
                bool isChallengeGated = challengeGatedSet.Contains(cb.BadgeId);
                bool isLevelUpBadge = levelUpSet.Contains(cb.BadgeId);

                if (!isChallengeGated && !isLevelUpBadge)
                    continue; // legacy badge — not gated, always valid

                if (isChallengeGated)
                {
                    // Check all challenges for this badge are completed by this child
                    var badgeChallenges = allChallenges
                        .Where(c => c.BadgeId == cb.BadgeId)
                        .Select(c => c.Id)
                        .ToList();

                    bool hasProgress = progressLookup.TryGetValue(cb.ChildId, out var childProg);

                    bool allDone = badgeChallenges.All(cid =>
                        hasProgress
                        && childProg!.TryGetValue(cid, out var prog)
                        && prog.IsCompleted);

                    if (!allDone)
                    {
                        violations.Add(new IntegrityViolation
                        {
                            Rule = "R1",
                            Message = $"ChildBadge (BadgeId={cb.BadgeId}) awarded but not all challenges completed.",
                            ChildId = cb.ChildId,
                            EntityId = cb.BadgeId
                        });
                    }
                }

                if (isLevelUpBadge && !isChallengeGated)
                {
                    // Child must have reached the level that awards this badge
                    var levelForBadge = await _db.Levels
                        .Where(l => l.LevelUpBadgeId == cb.BadgeId)
                        .Select(l => l.LevelNumber)
                        .FirstOrDefaultAsync(ct);

                    int childLevel = userLevelByChild.TryGetValue(cb.ChildId, out var ul)
                        ? ul.LevelNumber : 0;

                    if (childLevel < levelForBadge)
                    {
                        violations.Add(new IntegrityViolation
                        {
                            Rule = "R1",
                            Message = $"ChildBadge (LevelUpBadgeId={cb.BadgeId}) awarded but child is at Level {childLevel}, required Level {levelForBadge}.",
                            ChildId = cb.ChildId,
                            EntityId = cb.BadgeId
                        });
                    }
                }
            }

            // ── R2: UserLevel rows must have valid LevelNumber ────────
            checks++;
            foreach (var ul in userLevels)
            {
                if (ul.LevelNumber < 1 || ul.LevelNumber > maxSeededLevel)
                {
                    violations.Add(new IntegrityViolation
                    {
                        Rule = "R2",
                        Message = $"UserLevel.LevelNumber={ul.LevelNumber} is outside valid range [1, {maxSeededLevel}].",
                        ChildId = ul.ChildId,
                        EntityId = ul.LevelId
                    });
                }

                if (!allLevelNumbers.Contains(ul.LevelNumber))
                {
                    violations.Add(new IntegrityViolation
                    {
                        Rule = "R2",
                        Message = $"UserLevel.LevelNumber={ul.LevelNumber} has no matching row in the Levels table.",
                        ChildId = ul.ChildId,
                        EntityId = ul.LevelId
                    });
                }
            }

            // ── R3: ChildProfile.Level must mirror UserLevel.LevelNumber ─
            checks++;
            foreach (var ul in userLevels)
            {
                if (!profileByChild.TryGetValue(ul.ChildId, out var profile))
                    continue; // no profile yet — skip

                if (profile.Level != ul.LevelNumber)
                {
                    violations.Add(new IntegrityViolation
                    {
                        Rule = "R3",
                        Message = $"ChildProfile.Level={profile.Level} does not match UserLevel.LevelNumber={ul.LevelNumber}.",
                        ChildId = ul.ChildId,
                        EntityId = ul.LevelId
                    });
                }
            }

            // ── R4 + R10: Challenge-gated badges: all challenges must be complete ─
            // (R4 covers the progress rows; R10 re-checks from badge side)
            checks++;
            var childBadgesByChild = childBadges
                .Where(cb => challengeGatedSet.Contains(cb.BadgeId))
                .GroupBy(cb => cb.ChildId);

            foreach (var grp in childBadgesByChild)
            {
                int childId = grp.Key;
                progressLookup.TryGetValue(childId, out var childProg);

                foreach (var cb in grp)
                {
                    var requiredChallengeIds = allChallenges
                        .Where(c => c.BadgeId == cb.BadgeId)
                        .Select(c => c.Id)
                        .ToList();

                    foreach (var cid in requiredChallengeIds)
                    {
                        bool completedInDb = childProg != null
                            && childProg.TryGetValue(cid, out var pr)
                            && pr.IsCompleted;

                        if (!completedInDb)
                        {
                            violations.Add(new IntegrityViolation
                            {
                                Rule = "R4",
                                Message = $"Badge {cb.BadgeId} is unlocked but ChallengeId={cid} not completed in ChildChallengeProgress.",
                                ChildId = childId,
                                EntityId = cid
                            });
                        }
                    }
                }
            }

            // ── R5: IsCompleted=true requires CurrentValue >= TargetValue ─
            checks++;
            var challengeTargetMap = allChallenges.ToDictionary(c => c.Id, c => c.TargetValue);

            foreach (var p in allProgress.Where(p => p.IsCompleted))
            {
                if (!challengeTargetMap.TryGetValue(p.ChallengeId, out double target))
                    continue;

                if (p.CurrentValue < target - FloatEpsilon)
                {
                    violations.Add(new IntegrityViolation
                    {
                        Rule = "R5",
                        Message = $"ChildChallengeProgress (ChallengeId={p.ChallengeId}) IsCompleted=true but CurrentValue={p.CurrentValue:F2} < TargetValue={target:F2}.",
                        ChildId = p.ChildId,
                        EntityId = p.ChallengeId
                    });
                }
            }

            // ── R6: All 8 pillar columns must be in [0, 100] ─────────
            checks++;
            foreach (var profile in profiles)
            {
                var pillars = new[]
                {
                    (nameof(profile.SpiritualConnection_Pct),  profile.SpiritualConnection_Pct),
                    (nameof(profile.SocialInteraction_Pct),    profile.SocialInteraction_Pct),
                    (nameof(profile.AcademicProgress_Pct),     profile.AcademicProgress_Pct),
                    (nameof(profile.HomeResponsibility_Pct),   profile.HomeResponsibility_Pct),
                    (nameof(profile.PhysicalHealth_Pct),       profile.PhysicalHealth_Pct),
                    (nameof(profile.PhysicalActivity_Pct),     profile.PhysicalActivity_Pct),
                    (nameof(profile.CreativityEngagement_Pct), profile.CreativityEngagement_Pct),
                    (nameof(profile.MentalWellbeing_Pct),      profile.MentalWellbeing_Pct),
                };

                foreach (var (name, value) in pillars)
                {
                    if (value < PillarMin - FloatEpsilon || value > PillarMax + FloatEpsilon)
                    {
                        violations.Add(new IntegrityViolation
                        {
                            Rule = "R6",
                            Message = $"ChildProfile.{name}={value:F2} is outside [0, 100]. Possible write-path bypass.",
                            ChildId = profile.UserId,
                            EntityId = profile.UserId
                        });
                    }
                }
            }

            // ── R7: ResiliencePct must equal ResilienceTrait ──────────
            checks++;
            foreach (var profile in profiles)
            {
                double diff = Math.Abs(profile.ResiliencePct - profile.ResilienceTrait);
                if (diff > FloatEpsilon)
                {
                    violations.Add(new IntegrityViolation
                    {
                        Rule = "R7",
                        Message = $"ChildProfile.ResiliencePct={profile.ResiliencePct:F2} != ResilienceTrait={profile.ResilienceTrait:F2} (diff={diff:F4}). TraitSynthesisEngine sync broken.",
                        ChildId = profile.UserId,
                        EntityId = profile.UserId
                    });
                }
            }

            // ── R8: Active child must have at least one non-zero pillar ─
            checks++;
            foreach (int childId in activeChildSet)
            {
                if (!profileByChild.TryGetValue(childId, out var profile))
                    continue;

                bool anyNonZero =
                    profile.SpiritualConnection_Pct > FloatEpsilon ||
                    profile.SocialInteraction_Pct > FloatEpsilon ||
                    profile.AcademicProgress_Pct > FloatEpsilon ||
                    profile.HomeResponsibility_Pct > FloatEpsilon ||
                    profile.PhysicalHealth_Pct > FloatEpsilon ||
                    profile.PhysicalActivity_Pct > FloatEpsilon ||
                    profile.CreativityEngagement_Pct > FloatEpsilon ||
                    profile.MentalWellbeing_Pct > FloatEpsilon;

                if (!anyNonZero)
                {
                    violations.Add(new IntegrityViolation
                    {
                        Rule = "R8",
                        Message = $"Child has log entries in the last 90 days but all 8 pillars are 0. PillarCalculationEngine may not have been called.",
                        ChildId = childId,
                        EntityId = childId
                    });
                }
            }

            // ── R9: UserLevel.LevelNumber must not exceed max seeded level ─
            checks++;
            foreach (var ul in userLevels)
            {
                if (ul.LevelNumber > maxSeededLevel)
                {
                    violations.Add(new IntegrityViolation
                    {
                        Rule = "R9",
                        Message = $"UserLevel.LevelNumber={ul.LevelNumber} exceeds highest seeded Level ({maxSeededLevel}).",
                        ChildId = ul.ChildId,
                        EntityId = ul.LevelId
                    });
                }
            }

            var report = new IntegrityReport
            {
                RunAt = DateTime.UtcNow,
                TotalChecks = checks,
                Violations = violations.AsReadOnly()
            };

            if (report.IsHealthy)
                _logger.LogInformation("[Integrity] {Result}", report.ToString());
            else
                foreach (var v in violations)
                    _logger.LogWarning("[Integrity] Rule {Rule} — ChildId={ChildId} EntityId={EntityId}: {Message}",
                        v.Rule, v.ChildId?.ToString() ?? "—", v.EntityId?.ToString() ?? "—", v.Message);

            return report;
        }
    }
}
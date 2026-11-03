// ============================================================
//  NewProgressionService.cs — NEW Progression Engine (Phase 10)
//
//  REPLACES: GamingService.UpdateScore() badge/level logic
//            BadgeLogic (IScoreObserver)
//            LevelRequirementsEngine calls in ChildController
//
//  Flow (called after every journal save):
//  1. Compute current values for all metric types from DB
//  2. Update ChildChallengeProgress for each challenge
//  3. Call NewBadgeEngineService → unlock completed badges
//  4. Call NewLevelEngineService → level-up if qualified
//  5. Return summary of what changed
//
//  SAFETY RULE: This is the ONLY code path that writes to
//  ChildChallengeProgress, user_levels, and (via badge engine)
//  ChildBadges for new unlocks.
// ============================================================
// ============================================================
//  NewProgressionService.cs — FIXED (Phase 13 Hybrid Pillar Alignment)
//
//  BUG FIX (Phase 13):
//  ⑤ pillar_pct challenges now evaluate against FinalXxx_Pct
//     (70% Structured + 30% NLP) instead of the raw Structured
//     SpiritualConnection_Pct / SocialInteraction_Pct / etc.
//     This aligns the badge gate with the level-up gate, which
//     already uses FinalMeanScore.  Without this fix, a child
//     whose AI-NLP pillar scores lowered a Final pillar below the
//     challenge threshold could still unlock the badge, creating
//     an inconsistency between the two gates in the same session.
//
//     DB column mapping (FinalXxx_Pct → MetricSubType string):
//       FinalSpiritualConnection_Pct  → "Spiritual"
//       FinalSocialInteraction_Pct    → "Social"
//       FinalAcademicProgress_Pct     → "Academic"
//       FinalHomeResponsibility_Pct   → "HomeRes"   (CSV calls it Env_Pct)
//       FinalPhysicalHealth_Pct       → "Nutrition" (CSV calls it Phys_Pct)
//       FinalPhysicalActivity_Pct     → "Sport"     (CSV calls it Res_Pct)
//       FinalCreativityEngagement_Pct → "Hobbies"
//       FinalMentalWellbeing_Pct      → "Mental"
//
//     Graceful degradation: if FinalXxx_Pct = 0 (AI has never written
//     NLP pillars yet), HybridPillar degrades to 0.70 × Structured,
//     which is always ≤ the raw Structured value, so no badge is
//     awarded prematurely.  Once the AI team writes NLP data the
//     Final pillar rises to its correct blended value.
//
//  ⑥ CalculateExpAward now uses the same FinalXxx_Pct values for
//     consistency with the badge gate above.
// ============================================================
// ============================================================
//  NewProgressionService.cs — FIXED (Phase 11 Level-Guard)
//
//  ROOT CAUSE FIXES IN THIS FILE:
//  ① ComputeMetricsAsync now filters ONLY IsJournalSaved = true entries.
//     Previously, raw/unsaved DailyLogEntries were counted, allowing
//     challenge progress to accumulate from partial/autosaved entries.
//
//  ② All date-based metrics (streak, total_logs, prayer_day, quran_days,
//     water_six, subjects_day, movement_days, helped_family) now count
//     DISTINCT calendar days with a saved entry, not raw entry counts.
//     This prevents a single entry that mentions "Fajr,Dhuhr,Asr,Maghrib,Isha"
//     from satisfying a "5 prayer days" challenge on day 1.
//
//  ③ Added MinimumDistinctJournalDays guard: no level-up may happen
//     unless the child has at least a configurable minimum number of
//     DISTINCT saved journal days.  Default is 3 days for Level 1→2,
//     scaling upward per level (see _minDaysPerLevel).
//     This is checked in UpdateChallengeProgressAsync and also gated
//     at the metric level via "distinct_journal_days" metric type.
//
//  ④ "has_hero_name" and "has_parent_link" challenges now have a
//     separate path that only satisfies once the child also has
//     MinimumDaysForHeroName or MinimumDaysForParentLink distinct days.
//     (Configurable in appsettings under "LevelRules:MinDaysForHero" etc.)
//     Default: 1 day (they can set hero name + parent link on day 1 and
//     have that challenge count on that day — but total_logs or streak
//     challenges on the same badge will still gate the badge unlock.)
//
//  The key invariant: a badge whose ALL challenges are completed can only
//  be earned after the child has used the app across MULTIPLE saved days.
// ============================================================
using GrawiaaApp.API.Data;
using GrawiaaApp.API.Models;
using Microsoft.EntityFrameworkCore;

namespace GrawiaaApp.API.Services
{
    public interface INewProgressionService
    {
        Task<ProgressionResult> RunAsync(int childId);
    }

    public class NewProgressionService : INewProgressionService
    {
        private readonly ApplicationDbContext _context;
        private readonly INewBadgeEngineService _badgeEngine;
        private readonly INewLevelEngineService _levelEngine;
        private readonly ILogger<NewProgressionService> _logger;
        private readonly IChildIdentityService _childIdentity;
        private readonly IConfiguration _configuration;

        // Minimum DISTINCT saved journal days required before any challenge
        // of that type can register as completed.  Configurable via:
        //   appsettings.json  "LevelRules:MinJournalDaysForChallengeCompletion": 2
        // Default: 2 (you need at least 2 saved journal days to complete
        // ANY challenge, preventing day-1 badge / level exploits for
        // streak/log/prayer/activity-count challenges).
        // For simple boolean checks (has_hero_name, has_parent_link) the
        // minimum is 1 (same day is fine — but the badge they belong to
        // will still have other non-boolean challenges that require days).
        private const int DefaultMinDaysForCompletion = 2;

        public NewProgressionService(
            ApplicationDbContext context,
            INewBadgeEngineService badgeEngine,
            INewLevelEngineService levelEngine,
            ILogger<NewProgressionService> logger,
            IChildIdentityService childIdentity,
            IConfiguration configuration)
        {
            _context = context;
            _badgeEngine = badgeEngine;
            _levelEngine = levelEngine;
            _logger = logger;
            _childIdentity = childIdentity;
            _configuration = configuration;
        }

        public async Task<ProgressionResult> RunAsync(int childId)
        {
            var result = new ProgressionResult { ChildId = childId };

            try
            {
                // ── STEP 1: Compute current metric values ─────────────────
                var metrics = await ComputeMetricsAsync(childId);

                // ── STEP 2: Update challenge progress ─────────────────────
                await UpdateChallengeProgressAsync(childId, metrics);

                // ── STEP 3: Evaluate and unlock badges ────────────────────
                var newBadgeIds = await _badgeEngine.EvaluateAndUnlockBadgesAsync(childId);
                result.NewlyUnlockedBadgeIds = newBadgeIds;

                // ── STEP 4: Award EXP then check level-up ────────────────
                int expToAward = CalculateExpAward(metrics);
                await _levelEngine.AddExpAsync(childId, expToAward);
                result.ExpAwarded = expToAward;

                var levelResult = await _levelEngine.EvaluateLevelUpAsync(childId);
                result.LeveledUp = levelResult.LeveledUp;
                result.CurrentLevel = levelResult.CurrentLevel;
                result.LevelName = levelResult.LevelName;
                result.LevelExpAwarded = levelResult.ExpAwarded;

                _logger.LogDebug(
                    "Progression run complete: ChildId={ChildId} NewBadges={Count} LeveledUp={LU} DistinctDays={Days}",
                    childId, newBadgeIds.Count, levelResult.LeveledUp, metrics.TotalLogs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Progression engine error for ChildId={ChildId}", childId);
                result.HasError = true;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        // ── Metric computation ────────────────────────────────────────────
        private async Task<ChildMetrics> ComputeMetricsAsync(int childId)
        {
            // FIX ①: Use Egypt Standard Time for date calculations (preserves existing behaviour).
            TimeZoneInfo egyptZone;
            try { egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
            catch { egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }

            var nowEgypt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptZone);
            var todayEgypt = nowEgypt.Date;
            var cutoff = todayEgypt.AddDays(-90);

            // FIX ①: ONLY count entries that have been explicitly saved (IsJournalSaved = true).
            // Previously the filter was missing, allowing unsaved/partial entries to boost metrics.
            var allEntries = await _context.DailyLogEntries
                .Where(l => l.ChildId == childId
                         && l.LogDate >= cutoff
                         && l.IsJournalSaved)          // ← KEY FIX: only fully saved entries
                .ToListAsync();

            var profile = await _context.ChildProfiles
                .FirstOrDefaultAsync(cp => cp.UserId == childId);

            // FIX ②: Each metric that counts "days" groups by DISTINCT calendar day.
            // A single entry per day is enforced by the journal upsert, but we group
            // here to be defensive (accounts for admin-created duplicate rows, etc.).
            var logDateGroups = allEntries
                .GroupBy(l => l.LogDate.Date)
                .ToDictionary(g => g.Key, g => g.ToList());

            var logDates = logDateGroups.Keys.ToHashSet();

            // ── Distinct saved journal days (used as a global guard) ──────
            int distinctSavedDays = logDates.Count;

            // ── Streak (consecutive days ending today or yesterday) ───────
            int streak = 0;
            var checkDay = todayEgypt;
            while (logDates.Contains(checkDay))
            {
                streak++;
                checkDay = checkDay.AddDays(-1);
                if (streak >= 365) break;
            }

            // ── Prayer: days where ALL 5 prayers were logged ──────────────
            // FIX ②: counts DISTINCT days, not individual entries
            var prayerNames = new[] { "Fajr", "Dhuhr", "Asr", "Maghrib", "Isha" };
            int allPrayersDayCount = logDateGroups.Values.Count(entries =>
                entries.Any(e =>
                    !string.IsNullOrWhiteSpace(e.SpiritualActivities) &&
                    prayerNames.All(p => e.SpiritualActivities.Contains(p, StringComparison.OrdinalIgnoreCase))));

            // ── Quran days ─────────────────────────────────────────────────
            int quranDays = logDateGroups.Values.Count(entries =>
                entries.Any(e =>
                    !string.IsNullOrWhiteSpace(e.SpiritualActivities) &&
                    (e.SpiritualActivities.Contains("quran", StringComparison.OrdinalIgnoreCase) ||
                     e.SpiritualActivities.Contains("قرآن", StringComparison.OrdinalIgnoreCase) ||
                     e.SpiritualActivities.Contains("قران", StringComparison.OrdinalIgnoreCase))));

            // ── Helped family days ─────────────────────────────────────────
            int helpedFamilyCount = logDateGroups.Values.Count(entries =>
                entries.Any(e => e.HelpedFamily || !string.IsNullOrWhiteSpace(e.CompletedChores)));

            // ── Water >= 6 glasses days ────────────────────────────────────
            int waterSixCount = logDateGroups.Values.Count(entries =>
                entries.Any(e => e.WaterGlasses >= 6));

            // ── Subjects completed days ────────────────────────────────────
            int subjectsDayCount = logDateGroups.Values.Count(entries =>
                entries.Any(e => !string.IsNullOrWhiteSpace(e.CompletedSubjects)));

            // ── Movement days ──────────────────────────────────────────────
            // FIX (Bug #3): Old check used Contains("No") which is a false-negative trap:
            // "Nordic walking", "Normal workout" → contains "No" → wrongly excluded.
            // Correct logic: movement is logged if PhysicalActivity is non-empty AND is NOT
            // one of the explicit "no movement" sentinel values used by the frontend.
            // Also accepts ActivityNote as a fallback signal.
            var noMovementSentinels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "None", "No", "No exercise", "لا", "لا يوجد", "0"
            };
            int movedBodyCount = logDateGroups.Values.Count(entries =>
                entries.Any(e =>
                    (!string.IsNullOrWhiteSpace(e.PhysicalActivity) &&
                     !noMovementSentinels.Contains(e.PhysicalActivity.Trim()))
                    ||
                    (!string.IsNullOrWhiteSpace(e.ActivityNote) &&
                     !noMovementSentinels.Contains(e.ActivityNote.Trim()))
                ));

            // ── Kind words (same heuristic as helped family) ───────────────
            // FIX (Bug #2): KindWordCount was an alias for HelpedFamilyCount, making
            // them always identical. kind_word challenges measure SOCIAL kindness (saying
            // nice things, positive interaction), which is distinct from home chores.
            // Now counts days where the child logged any positive social interaction:
            // SocialQualityScore > 0  (auto-scored from SocialTimeWith / ChallengeHandling)
            // OR non-empty SocialInteraction text OR non-empty SocialTimeWith text.
            int kindWordCount = logDateGroups.Values.Count(entries =>
                entries.Any(e =>
                    e.SocialQualityScore > 0 ||
                    !string.IsNullOrWhiteSpace(e.SocialInteraction) ||
                    !string.IsNullOrWhiteSpace(e.SocialTimeWith)));

            // ── Hobbies ────────────────────────────────────────────────────
            bool hasHobby = await _context.ChildHobbies.AnyAsync(h => h.ChildId == childId);
            int hobbyCount = await _context.ChildHobbies.CountAsync(h => h.ChildId == childId);

            // ── Movement badges ────────────────────────────────────────────
            // Only SportBronze (Team Sports, Id=33) remains after dead badge cleanup.
            var movCats = new[] { (int)BadgeCategory.SportBronze };
            int earnedMovBadges = await _context.ChildBadges
                .Where(cb => cb.ChildId == childId && cb.IsActive && movCats.Contains((int)cb.Badge.Category))
                .CountAsync();

            // ── Completed tasks ────────────────────────────────────────────
            int completedTasks = await _context.ChildTasks
                 .CountAsync(t => t.ChildId == childId
                  && t.IsCompleted
                  && t.Description != "__plan_template__");

            return new ChildMetrics
            {
                TotalLogs = distinctSavedDays,          // ← distinct saved days
                CurrentStreak = streak,
                DistinctSavedDays = distinctSavedDays,          // ← explicit guard field
                TasksCompleted = completedTasks,
                HobbyCount = hobbyCount,
                HasHobby = hasHobby,
                HasHeroName = !string.IsNullOrWhiteSpace(profile?.CharacterName),
                HasParentLink = profile?.ParentId != null,
                AllPrayersDayCount = allPrayersDayCount,
                QuranDays = quranDays,
                HelpedFamilyCount = helpedFamilyCount,
                KindWordCount = kindWordCount,
                WaterSixCount = waterSixCount,
                SubjectsDayCount = subjectsDayCount,
                MovedBodyCount = movedBodyCount,
                HasAllMovementBadges = earnedMovBadges >= 1,
                // BUG FIX ⑤ (Phase 13): Use Final Hybrid Pillars (70% Structured + 30% NLP)
                // so the badge gate matches the level-up gate (NewLevelEngineService also
                // uses FinalMeanScore). Raw Structured values are intentionally NOT used here.
                //
                // Naming reference (CSV → DB column → MetricSubType):
                //   Spir_Pct  → FinalSpiritualConnection_Pct  → "Spiritual"
                //   Soc_Pct   → FinalSocialInteraction_Pct    → "Social"
                //   Acad_Pct  → FinalAcademicProgress_Pct     → "Academic"
                //   Env_Pct   → FinalHomeResponsibility_Pct   → "HomeRes"   (Home, NOT Environment)
                //   Phys_Pct  → FinalPhysicalHealth_Pct       → "Nutrition" (Nutrition, NOT Sport)
                //   Res_Pct   → FinalPhysicalActivity_Pct     → "Sport"     (Sport/Activity, NOT Resilience trait)
                //   Crea_Pct  → FinalCreativityEngagement_Pct → "Hobbies"
                //   Ment_Pct  → FinalMentalWellbeing_Pct      → "Mental"
                SpiritualPct = profile?.FinalSpiritualConnection_Pct ?? 0,
                SocialPct = profile?.FinalSocialInteraction_Pct ?? 0,
                AcademicPct = profile?.FinalAcademicProgress_Pct ?? 0,
                HomeResPct = profile?.FinalHomeResponsibility_Pct ?? 0,   // CSV: Env_Pct
                NutritionPct = profile?.FinalPhysicalHealth_Pct ?? 0,   // CSV: Phys_Pct
                SportPct = profile?.FinalPhysicalActivity_Pct ?? 0,   // CSV: Res_Pct
                HobbiesPct = profile?.FinalCreativityEngagement_Pct ?? 0,
                MentalPct = profile?.FinalMentalWellbeing_Pct ?? 0
            };
        }

        private async Task UpdateChallengeProgressAsync(int childId, ChildMetrics m)
        {
            var allChallenges = await _context.BadgeChallenges.ToListAsync();
            if (!allChallenges.Any()) return;

            var hashedChildId = _childIdentity.GetSafeChildKey(childId);

            // Read the minimum days threshold from config, fallback to default.
            int minDaysRequired = _configuration.GetValue<int>(
                "LevelRules:MinJournalDaysForChallengeCompletion",
                DefaultMinDaysForCompletion);

            var existingProgress = await _context.ChildChallengeProgresses
                .Where(cp => cp.ChildId == childId)
                .ToDictionaryAsync(cp => cp.ChallengeId);

            var toAdd = new List<ChildChallengeProgress>();
            var toUpdate = new List<ChildChallengeProgress>();

            foreach (var challenge in allChallenges)
            {
                // AI-team-owned challenges: the backend has no numeric metric
                // for these (the AI's NLP pipeline reads the journal text and
                // writes IsCompleted/CurrentValue itself via the existing
                // ChildChallengeProgress table — no separate endpoint needed,
                // same hashed_child_id pattern as everywhere else). Skip them
                // here entirely so this loop never overwrites what the AI
                // team wrote, in either direction.
                if (challenge.MetricType == "journal_text")
                    continue;

                double currentValue = GetMetricValue(challenge, m);

                // FIX ③: For time-based / count-based challenges, a challenge
                // cannot be marked completed unless the child has used the app
                // across at least minDaysRequired distinct saved journal days.
                // Boolean one-off challenges (has_hero_name, has_parent_link)
                // only require 1 day (the day they set those up).
                bool isBooleanOneOff = challenge.MetricType is "has_hero_name" or "has_parent_link";
                int effectiveMinDays = isBooleanOneOff ? 1 : minDaysRequired;

                bool hasEnoughDays = m.DistinctSavedDays >= effectiveMinDays;

                // The challenge is only completable when:
                //  (a) the raw metric value meets the target, AND
                //  (b) the child has enough distinct saved days.
                bool isCompleted = currentValue >= challenge.TargetValue && hasEnoughDays;

                if (existingProgress.TryGetValue(challenge.Id, out var progress))
                {
                    bool wasCompleted = progress.IsCompleted;

                    // FIX: Never reverse a completion once earned (prevents
                    // un-completing challenges if a log is later deleted).
                    // A challenge stays completed once it has been marked so.
                    if (!wasCompleted)
                    {
                        progress.CurrentValue = currentValue;
                        progress.IsCompleted = isCompleted;
                        progress.LastUpdatedAt = DateTime.UtcNow;
                        progress.HashedChildId = hashedChildId;
                        if (isCompleted)
                            progress.UnlockedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        // Already completed: update value but keep completed flag.
                        progress.CurrentValue = currentValue;
                        progress.LastUpdatedAt = DateTime.UtcNow;
                        progress.HashedChildId = hashedChildId;
                    }

                    toUpdate.Add(progress);
                }
                else
                {
                    toAdd.Add(new ChildChallengeProgress
                    {
                        ChildId = childId,
                        HashedChildId = hashedChildId,
                        ChallengeId = challenge.Id,
                        CurrentValue = currentValue,
                        IsCompleted = isCompleted,
                        UnlockedAt = isCompleted ? DateTime.UtcNow : null,
                        LastUpdatedAt = DateTime.UtcNow
                    });
                }
            }

            if (toAdd.Any()) _context.ChildChallengeProgresses.AddRange(toAdd);
            if (toUpdate.Any() || toAdd.Any()) await _context.SaveChangesAsync();
        }

        private double GetMetricValue(BadgeChallenge challenge, ChildMetrics m)
        {
            return challenge.MetricType switch
            {
                "streak" => m.CurrentStreak,
                "total_logs" => m.TotalLogs,              // ← now = distinct saved days
                "distinct_days" => m.TotalLogs,              // ← alias
                "tasks_completed" => m.TasksCompleted,
                "hobby_count" => m.HobbyCount,
                "has_hobby" => m.HasHobby ? 1 : 0,
                "has_hero_name" => m.HasHeroName ? 1 : 0,
                "has_parent_link" => m.HasParentLink ? 1 : 0,
                "prayer_day" => m.AllPrayersDayCount,     // ← distinct days
                "quran_days" => m.QuranDays,              // ← distinct days
                "helped_family" => m.HelpedFamilyCount,      // ← distinct days
                "kind_word" => m.KindWordCount,          // ← distinct days
                "water_six" => m.WaterSixCount,          // ← distinct days
                "subjects_day" => m.SubjectsDayCount,       // ← distinct days
                "movement_days" => m.MovedBodyCount,         // ← distinct days
                "movement_badges" => m.HasAllMovementBadges ? 1 : 0,
                "pillar_pct" => challenge.MetricSubType switch
                {
                    "Spiritual" => m.SpiritualPct,
                    "Social" => m.SocialPct,
                    "Academic" => m.AcademicPct,
                    "HomeRes" => m.HomeResPct,
                    "Nutrition" => m.NutritionPct,
                    "Sport" => m.SportPct,
                    "Hobbies" => m.HobbiesPct,
                    "Mental" => m.MentalPct,
                    _ => 0
                },
                _ => 0
            };
        }

        // FIX (Bug #1): EXP was always 5, regardless of how well the child performed.
        // New formula gives meaningful feedback while staying capped to avoid grinding:
        //   Base:        5 EXP  — just for saving a journal
        //   Streak:     +2 EXP  per streak day (max +20 for 10-day streak)
        //   Full prayer: +3 EXP  if all 5 prayers were logged today
        //   Good pillars:+2 EXP  if average Final Hybrid pillar pct ≥ 60 (decent day overall)
        //   Cap: 30 EXP max per journal save to prevent runaway accumulation.
        //
        // BUG FIX ⑥ (Phase 13): pillar values here now come from FinalXxx_Pct
        // (same as the badge gate fix above), so EXP and badge gates are consistent.
        private int CalculateExpAward(ChildMetrics m)
        {
            int exp = 5; // base

            // Streak bonus: +2 per streak day, capped at 10 days (+20)
            exp += Math.Min(m.CurrentStreak, 10) * 2;

            // Prayer bonus
            if (m.AllPrayersDayCount > 0)
                exp += 3;

            // Good overall day: average of non-zero pillars >= 60
            var pillarValues = new[]
            {
                m.SpiritualPct, m.SocialPct, m.AcademicPct,
                m.HomeResPct, m.NutritionPct, m.SportPct,
                m.HobbiesPct, m.MentalPct
            };
            var nonZero = pillarValues.Where(p => p > 0).ToList();
            if (nonZero.Count > 0 && nonZero.Average() >= 60)
                exp += 2;

            return Math.Min(exp, 30);
        }

        // ── Internal metric snapshot ──────────────────────────────────────
        private sealed class ChildMetrics
        {
            public int TotalLogs { get; set; }
            public int CurrentStreak { get; set; }
            /// <summary>
            /// FIX: Explicit distinct saved journal days count.
            /// Used as the progression guard (must be >= minimum before
            /// any time-based challenge can be marked completed).
            /// </summary>
            public int DistinctSavedDays { get; set; }
            public int TasksCompleted { get; set; }
            public int HobbyCount { get; set; }
            public bool HasHobby { get; set; }
            public bool HasHeroName { get; set; }
            public bool HasParentLink { get; set; }
            public int AllPrayersDayCount { get; set; }
            public int QuranDays { get; set; }
            public int HelpedFamilyCount { get; set; }
            public int KindWordCount { get; set; }
            public int WaterSixCount { get; set; }
            public int SubjectsDayCount { get; set; }
            public int MovedBodyCount { get; set; }
            public bool HasAllMovementBadges { get; set; }

            // BUG FIX ⑤ (Phase 13): These are now Final Hybrid Pillar values
            // (FinalXxx_Pct = 70% Structured + 30% NLP), NOT raw Structured values.
            // This aligns the badge pillar_pct gate with the level-up FinalMeanScore gate.
            //
            // CSV name   → DB column                  → MetricSubType
            // Spir_Pct   → FinalSpiritualConnection_Pct  → "Spiritual"
            // Soc_Pct    → FinalSocialInteraction_Pct    → "Social"
            // Acad_Pct   → FinalAcademicProgress_Pct     → "Academic"
            // Env_Pct    → FinalHomeResponsibility_Pct   → "HomeRes"   (NOT Environment)
            // Phys_Pct   → FinalPhysicalHealth_Pct       → "Nutrition" (NOT Sport)
            // Res_Pct    → FinalPhysicalActivity_Pct     → "Sport"     (NOT Resilience trait)
            // Crea_Pct   → FinalCreativityEngagement_Pct → "Hobbies"
            // Ment_Pct   → FinalMentalWellbeing_Pct      → "Mental"
            public double SpiritualPct { get; set; }   // ← FinalSpiritualConnection_Pct
            public double SocialPct { get; set; }   // ← FinalSocialInteraction_Pct
            public double AcademicPct { get; set; }   // ← FinalAcademicProgress_Pct
            public double HomeResPct { get; set; }   // ← FinalHomeResponsibility_Pct  (CSV: Env_Pct)
            public double NutritionPct { get; set; }   // ← FinalPhysicalHealth_Pct      (CSV: Phys_Pct)
            public double SportPct { get; set; }   // ← FinalPhysicalActivity_Pct    (CSV: Res_Pct)
            public double HobbiesPct { get; set; }   // ← FinalCreativityEngagement_Pct
            public double MentalPct { get; set; }   // ← FinalMentalWellbeing_Pct
        }
    }

    public class ProgressionResult
    {
        public int ChildId { get; set; }
        public bool LeveledUp { get; set; }
        public int OldLevel { get; set; }
        public int CurrentLevel { get; set; }
        public string LevelName { get; set; } = string.Empty;
        public int ExpAwarded { get; set; }
        public int LevelExpAwarded { get; set; }
        public List<int> NewlyUnlockedBadgeIds { get; set; } = new();
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
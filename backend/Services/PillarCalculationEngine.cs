// ============================================================
//  PillarCalculationEngine.cs — Leafy AI System
//
//  Deterministic, weighted calculation of the 8 ChildProfile
//  pillars from one or more DailyLogEntry records.
//
//  RULES:
//  ──────
//  • All outputs are clamped to [0, 100].
//  • No random values — results are fully deterministic.
//  • logDuration participates in final normalization:
//      entries with longer active time carry more weight.
//  • MentalWellbeing_Pct has no direct Pct input field;
//      it is derived from emotional/mood signals embedded
//      in the entry (SentimentScore, MoodEmoji, MoodLevel).
//  • All other pillars follow the formula:
//      raw = primaryWeight * primaryField
//            + secondaryWeight * secondaryField (where applicable)
//      normalized = Clamp(raw * DurationFactor, 0, 100)
//  ──────
//  USAGE — single entry (inline after journal save):
//      var result = PillarCalculationEngine.FromSingleEntry(entry);
//
//  USAGE — weekly aggregation (WeeklyAggregationService):
//      var result = PillarCalculationEngine.FromWeeklyEntries(entries);
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using GrawiaaApp.API.Models;

namespace GrawiaaApp.API.Services
{
    // ── Output record ────────────────────────────────────────────────
    public sealed class PillarResult
    {
        /// <summary>Pillar 1 — Connection with Allah &amp; spiritual habits (0–100).</summary>
        public double SpiritualConnection_Pct { get; init; }

        /// <summary>Pillar 2 — Social behavior &amp; peer interaction (0–100).</summary>
        public double SocialInteraction_Pct { get; init; }

        /// <summary>Pillar 3 — Homework, study &amp; learning performance (0–100).</summary>
        public double AcademicProgress_Pct { get; init; }

        /// <summary>Pillar 4 — Family chores &amp; home responsibilities (0–100).</summary>
        public double HomeResponsibility_Pct { get; init; }

        /// <summary>Pillar 5 — Nutrition, eating habits &amp; body wellness (0–100).</summary>
        public double PhysicalHealth_Pct { get; init; }

        /// <summary>Pillar 6 — Sports, movement &amp; energy level (0–100).</summary>
        public double PhysicalActivity_Pct { get; init; }

        /// <summary>Pillar 7 — Hobbies, creativity &amp; self-expression (0–100).</summary>
        public double CreativityEngagement_Pct { get; init; }

        /// <summary>Pillar 8 — Emotions, stress &amp; mental health state (0–100).</summary>
        public double MentalWellbeing_Pct { get; init; }
    }

    // ── Per-child plan limits ───────────────────────────────────────────
    //
    //  BUG FIX (parent-plan-aware denominators): CalcChores / CalcAcademic /
    //  CalcMoving used to score CompletedChores / CompletedSubjects /
    //  PhysicalActivity counts against a GLOBAL hardcoded maxCount (5 / 6 / 5)
    //  regardless of how many chores/subjects/sports the parent actually
    //  assigned to THIS child via DailyManagementJson (see
    //  ChildController.GetMyManagement). Two children with different-sized
    //  plans were never comparable, and a child who completed 100% of a
    //  SHORT plan (e.g. 2 of 2 assigned chores) could never reach 100%,
    //  because the formula assumed everyone has up to 5 items.
    //
    //  ChildPlanLimits carries the REAL per-child denominators (the actual
    //  count of chores/subjects/sports in that child's parent-assigned
    //  plan). PillarPipelineService builds this from ChildProfile.
    //  DailyManagementJson before calling FromSingleEntry. When a child has
    //  no parent-assigned plan yet for a given category (count == 0), the
    //  matching Default* constant is used instead, preserving the original
    //  behaviour for children whose parents haven't set up a plan.
    public sealed class ChildPlanLimits
    {
        public const int DefaultChoresMax = 5;
        public const int DefaultSubjectsMax = 6;
        public const int DefaultSportsMax = 5;

        public int ChoresMax { get; init; } = DefaultChoresMax;
        public int SubjectsMax { get; init; } = DefaultSubjectsMax;
        public int SportsMax { get; init; } = DefaultSportsMax;

        /// <summary>No parent plan available — falls back to the original
        /// global constants (legacy behaviour, used when planLimits isn't
        /// passed to FromSingleEntry at all).</summary>
        public static readonly ChildPlanLimits Default = new();

        /// <summary>
        /// Builds plan limits from the raw chores/subjects/sports counts of
        /// a specific child's parent-assigned plan. Any category the parent
        /// hasn't configured (count == 0) falls back to the global default
        /// for that category instead of becoming a 0-item (always-100%-or-
        /// always-0%) denominator.
        /// </summary>
        public static ChildPlanLimits FromCounts(int choresCount, int subjectsCount, int sportsCount) => new()
        {
            ChoresMax = choresCount > 0 ? choresCount : DefaultChoresMax,
            SubjectsMax = subjectsCount > 0 ? subjectsCount : DefaultSubjectsMax,
            SportsMax = sportsCount > 0 ? sportsCount : DefaultSportsMax,
        };
    }

    // ── Engine ───────────────────────────────────────────────────────
    public static class PillarCalculationEngine
    {
        // ─────────────────────────────────────────────────────────────
        //  Constants
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Reference session duration (minutes). A child who logs
        /// exactly this many active minutes receives a duration factor
        /// of 1.0 (no bonus, no penalty). Shorter sessions are down-
        /// weighted; longer sessions are capped at MaxDurationFactor.
        /// </summary>
        private const double ReferenceDurationMinutes = 60.0;

        /// <summary>Maximum multiplier from a very long session (≥ 120 min).</summary>
        private const double MaxDurationFactor = 1.20;

        /// <summary>Minimum multiplier from a very short session (≤ 5 min).</summary>
        private const double MinDurationFactor = 0.50;

        // MentalWellbeing weight split (3-way: sentiment + mood emoji + challenge handling).
        // FIX (stale formula cleanup): this used to be a 2-way split (Sentiment 70% /
        // MoodEmoji 30%) before ChallengeHandling was added as a third signal. The old
        // SentimentWeight=0.70 / MoodEmojiWeight=0.30 constants were never updated and
        // were never actually referenced by CalcMental (which already used the correct
        // 60/25/15 split as magic numbers) — they were dead, misleading code. Replaced
        // with the constants below, which match CalcMental's real formula exactly.
        private const double MentalSentimentWeight = 0.60;   // SentimentScore (NLP, –1 → +1)
        private const double MentalMoodWeight = 0.25;   // MoodEmoji categorical signal
        private const double MentalChallengeWeight = 0.15;   // ChallengeHandling resilience bonus

        // Minimum entries required to produce a meaningful weekly average.
        // If fewer entries exist the missing days score 0 (conservative).
        private const int WeekDays = 7;

        // ─────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Computes pillars from a single DailyLogEntry.
        /// Used inline after journal save so the ChildProfile is
        /// refreshed immediately (delta application path).
        /// </summary>
        ///
        /// <param name="entry">The daily log entry to score.</param>
        /// <param name="planLimits">
        /// The calling child's REAL parent-assigned plan size (chores /
        /// subjects / sports counts). When omitted, falls back to
        /// ChildPlanLimits.Default (the original global 5/6/5 constants) —
        /// existing callers that don't pass plan data keep their old
        /// behaviour exactly.
        /// </param>
        public static PillarResult FromSingleEntry(DailyLogEntry entry, ChildPlanLimits? planLimits = null)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));

            var limits = planLimits ?? ChildPlanLimits.Default;
            double df = DurationFactor(entry.LogDuration);

            return new PillarResult
            {
                SpiritualConnection_Pct = CalcSpiritual(entry, df),
                SocialInteraction_Pct = CalcSocial(entry, df),
                AcademicProgress_Pct = CalcAcademic(entry, df, limits.SubjectsMax),
                HomeResponsibility_Pct = CalcChores(entry, df, limits.ChoresMax),
                PhysicalHealth_Pct = CalcFood(entry, df),
                PhysicalActivity_Pct = CalcMoving(entry, df, limits.SportsMax),
                CreativityEngagement_Pct = CalcHobbies(entry, df),
                MentalWellbeing_Pct = CalcMental(entry, df),
            };
        }

        /// <summary>
        /// Computes weekly pillars by duration-weighted averaging of
        /// multiple DailyLogEntry records (typically the past 7 days).
        ///
        /// Missing days are counted as zero-contribution days, so a
        /// child who only logged 3 of 7 days is penalised proportionally.
        /// </summary>
        ///
        /// <param name="planLimits">
        /// Same per-child plan denominators as <see cref="FromSingleEntry"/>
        /// — see ChildPlanLimits remarks above. Defaults to the legacy
        /// global 5/6/5 constants when omitted, matching this method's
        /// original behaviour for callers that don't pass plan data.
        /// </param>
        public static PillarResult FromWeeklyEntries(IEnumerable<DailyLogEntry> entries, ChildPlanLimits? planLimits = null)
        {
            if (entries is null) throw new ArgumentNullException(nameof(entries));

            var list = entries.ToList();
            if (list.Count == 0)
                return ZeroResult();

            var limits = planLimits ?? ChildPlanLimits.Default;

            // Duration-weighted sum for each pillar
            double totalWeight = 0;
            double sumSpiritual = 0, sumSocial = 0, sumAcademic = 0,
                   sumChores = 0, sumFood = 0, sumMoving = 0,
                   sumHobbies = 0, sumMental = 0;

            foreach (var entry in list)
            {
                double df = DurationFactor(entry.LogDuration);
                double weight = Math.Max(entry.LogDuration, 1); // weight = raw minutes

                totalWeight += weight;
                sumSpiritual += CalcSpiritual(entry, df) * weight;
                sumSocial += CalcSocial(entry, df) * weight;
                sumAcademic += CalcAcademic(entry, df, limits.SubjectsMax) * weight;
                sumChores += CalcChores(entry, df, limits.ChoresMax) * weight;
                sumFood += CalcFood(entry, df) * weight;
                sumMoving += CalcMoving(entry, df, limits.SportsMax) * weight;
                sumHobbies += CalcHobbies(entry, df) * weight;
                sumMental += CalcMental(entry, df) * weight;
            }

            // Normalise by totalWeight; then apply weekly-coverage penalty
            // (entries logged / 7 days expected). This ensures a child who
            // skips days does not score as high as one who logs every day.
            double coverageFactor = Math.Min((double)list.Count / WeekDays, 1.0);

            return new PillarResult
            {
                SpiritualConnection_Pct = Clamp(sumSpiritual / totalWeight * coverageFactor),
                SocialInteraction_Pct = Clamp(sumSocial / totalWeight * coverageFactor),
                AcademicProgress_Pct = Clamp(sumAcademic / totalWeight * coverageFactor),
                HomeResponsibility_Pct = Clamp(sumChores / totalWeight * coverageFactor),
                PhysicalHealth_Pct = Clamp(sumFood / totalWeight * coverageFactor),
                PhysicalActivity_Pct = Clamp(sumMoving / totalWeight * coverageFactor),
                CreativityEngagement_Pct = Clamp(sumHobbies / totalWeight * coverageFactor),
                MentalWellbeing_Pct = Clamp(sumMental / totalWeight * coverageFactor),
            };
        }

        /// <summary>
        /// Computes ChildProfile's 8 aggregated pillars as a SIMPLE
        /// ARITHMETIC MEAN of the pre-computed DailyXxx_Pct fields on
        /// each DailyLogEntry, across a rolling 30-day window.
        ///
        /// This is the SOLE aggregation rule for ChildProfile (Phase 11):
        ///   • No duration weighting.
        ///   • No 7-day "coverage" penalty for missing days.
        ///   • No 90-day lifetime window.
        ///   • Average is taken only over days that HAVE an entry within
        ///     the last 30 days (missing days are simply not counted).
        ///
        /// If the child has zero entries in the window, all pillars = 0
        /// (same "no data yet" convention as ZeroResult()).
        ///
        /// Callers must pass entries whose DailyXxx_Pct fields were
        /// already populated via ApplyDailyPillars/FromSingleEntry —
        /// this method does NOT compute per-entry pillars itself.
        /// </summary>
        public static PillarResult From30DayDailyPillars(IEnumerable<DailyLogEntry> entries)
        {
            if (entries is null) throw new ArgumentNullException(nameof(entries));

            var list = entries.ToList();
            if (list.Count == 0)
                return ZeroResult();

            return new PillarResult
            {
                SpiritualConnection_Pct = Clamp(list.Average(e => e.DailySpiritualConnection_Pct)),
                SocialInteraction_Pct = Clamp(list.Average(e => e.DailySocialInteraction_Pct)),
                AcademicProgress_Pct = Clamp(list.Average(e => e.DailyAcademicProgress_Pct)),
                HomeResponsibility_Pct = Clamp(list.Average(e => e.DailyHomeResponsibility_Pct)),
                PhysicalHealth_Pct = Clamp(list.Average(e => e.DailyPhysicalHealth_Pct)),
                PhysicalActivity_Pct = Clamp(list.Average(e => e.DailyPhysicalActivity_Pct)),
                CreativityEngagement_Pct = Clamp(list.Average(e => e.DailyCreativityEngagement_Pct)),
                MentalWellbeing_Pct = Clamp(list.Average(e => e.DailyMentalWellbeing_Pct)),
            };
        }

        /// <summary>
        /// Applies a PillarResult onto a ChildProfile in-place.
        /// Call this after FromSingleEntry (delta path) or
        /// FromWeeklyEntries (full recompute path).
        /// </summary>
        public static void ApplyToProfile(ChildProfile profile, PillarResult result)
        {
            if (profile is null) throw new ArgumentNullException(nameof(profile));
            if (result is null) throw new ArgumentNullException(nameof(result));

            profile.SpiritualConnection_Pct = result.SpiritualConnection_Pct;
            profile.SocialInteraction_Pct = result.SocialInteraction_Pct;
            profile.AcademicProgress_Pct = result.AcademicProgress_Pct;
            profile.HomeResponsibility_Pct = result.HomeResponsibility_Pct;
            profile.PhysicalHealth_Pct = result.PhysicalHealth_Pct;
            profile.PhysicalActivity_Pct = result.PhysicalActivity_Pct;
            profile.CreativityEngagement_Pct = result.CreativityEngagement_Pct;
            profile.MentalWellbeing_Pct = result.MentalWellbeing_Pct;
        }

        // ─────────────────────────────────────────────────────────────
        //  Per-pillar calculation methods
        // ─────────────────────────────────────────────────────────────

        // ── Pillar 1: SpiritualConnection ────────────────────────────
        // Primary:   SpiritualActivities count (auto-calculated, 90%)
        // Secondary: SpiritualActivities count bonus (10%)
        //
        //   Auto-calculates SpiritualPct from activity tags when slider is 0.
        //   A child who logs activities gets a small completeness bonus
        //   even if their spiritualPct was modest.
        //
        //   BUG FIX (Phase 14 Audit): maxCount raised 4 -> 8. journal.html's
        //   spiritual section used to offer 4 buttons (Praying, Reading
        //   Quran, Athkar, Doaa). It now offers 8 (Fajr, Dhuhr, Asr,
        //   Maghrib, Isha, Reading Quran, Athkar, Doaa) — the single
        //   generic "Praying" button was split into 5 individual prayers
        //   so NewProgressionService's prayer_day metric (which checks
        //   SpiritualActivities for all 5 literal prayer names on the same
        //   day) can actually be satisfied; see journal.html's spiritual
        //   section comment for the full explanation. Leaving maxCount at
        //   4 here would have silently inflated this pillar for any child
        //   who only prayed (5 items selectable against an old 4-item
        //   denominator); 8 keeps the auto-calc curve calibrated to the
        //   real number of selectable items.
        private static double CalcSpiritual(DailyLogEntry e, double df)
        {
            // Auto-calculate if slider was 0 (sliders removed from journal)
            double effectiveSpiritualPct = e.SpiritualPct > 0
                ? e.SpiritualPct
                : AutoCalcPctFromCount(e.SpiritualActivities, maxCount: 8);

            double activityBonus = CountBonus(e.SpiritualActivities, maxCount: 8);
            double raw = (effectiveSpiritualPct * 0.90) + (activityBonus * 0.10);
            return Clamp(raw * df);
        }

        // ── Pillar 2: SocialInteraction ──────────────────────────────
        // Primary:   SocialQualityScore (auto-calculated, 70%)
        // Secondary: SocialTimeWith presence bonus (20%)
        // Tertiary:  ChallengeHandling prosocial signals (10%)
        //
        //   SocialQualityScore is auto-calculated from SocialTimeWith,
        //   ChallengeHandling (cooperative responses), and social context.
        //   Logging who they spent time with signals genuine engagement.
        private static double CalcSocial(DailyLogEntry e, double df)
        {
            double effectiveSocialPct;

            if (e.SocialQualityScore > 0)
            {
                effectiveSocialPct = e.SocialQualityScore;
            }
            else if (e.SocialPct > 0)
            {
                // Legacy slider value still present
                effectiveSocialPct = e.SocialPct;
            }
            else
            {
                // Auto-calculate from social context signals
                bool hasSocialContact = !string.IsNullOrWhiteSpace(e.SocialTimeWith);
                bool spentAlone = e.SocialTimeWith?.Contains("Alone", StringComparison.OrdinalIgnoreCase) == true;

                if (!hasSocialContact)
                {
                    effectiveSocialPct = 30.0; // No social data → conservative estimate
                }
                else if (spentAlone)
                {
                    effectiveSocialPct = 40.0; // Chose to be alone → lower social interaction
                }
                else
                {
                    // Had social contact — boost based on challenge handling quality
                    double challengeBoost = ChallengeHandlingToSocialBonus(e.ChallengeHandling);
                    effectiveSocialPct = 65.0 + (challengeBoost * 0.20); // 65–85 range
                }
            }

            double withBonus = string.IsNullOrWhiteSpace(e.SocialTimeWith) ? 0.0 : 100.0;
            double challengeSocialBonus = ChallengeHandlingToSocialBonus(e.ChallengeHandling);

            double raw = (effectiveSocialPct * 0.70)
                       + (withBonus * 0.20)
                       + (challengeSocialBonus * 0.10);
            return Clamp(raw * df);
        }

        // ── Pillar 3: AcademicProgress ───────────────────────────────
        // Primary:   StudyingPct (auto-calculated, 80%)
        // Secondary: CompletedSubjects count bonus (20%)
        //
        //   Completing subjects provides evidence of actual academic work.
        //
        //   BUG FIX: maxCount used to be hardcoded to 6 for every child,
        //   regardless of how many subjects the parent actually assigned
        //   in that child's plan (DailyManagementJson). A child whose
        //   parent only assigned 2 subjects could never reach 100% even
        //   by completing both. subjectsMax now defaults to 6 (legacy
        //   behaviour for callers that don't pass plan data) but
        //   FromSingleEntry passes the child's REAL assigned-subject
        //   count when a parent plan exists.
        private static double CalcAcademic(DailyLogEntry e, double df, int subjectsMax = 6)
        {
            double effectiveStudyingPct = e.StudyingPct > 0
                ? e.StudyingPct
                : AutoCalcPctFromCount(e.CompletedSubjects, maxCount: subjectsMax);

            double subjectBonus = CountBonus(e.CompletedSubjects, maxCount: subjectsMax);
            double raw = (effectiveStudyingPct * 0.80) + (subjectBonus * 0.20);
            return Clamp(raw * df);
        }

        // ── Pillar 4: HomeResponsibility ─────────────────────────────
        // Primary:   ChorsPct (auto-calculated, 75%)
        // Secondary: HelpedFamily binary (15%) + CompletedChores count (10%)
        //
        //   Explicitly helping family is a strong chores signal.
        //
        //   FIX: effectiveChorsPct used to hard-code 80.0 whenever
        //   HelpedFamily was true, regardless of how many chores were
        //   actually completed — so 1 chore and 5 chores scored almost
        //   identically (the only difference came from the 10%-weighted
        //   choreBonus). Now effectiveChorsPct itself scales with the
        //   completed-chores count via AutoCalcPctFromCount (same curve
        //   used by Academic/Spiritual/etc.), with HelpedFamily kept as
        //   a smaller floor/bonus rather than a value-erasing constant.
        //   Adding a new chore now visibly moves the score.
        //
        //   BUG FIX: maxCount used to be hardcoded to 5 for every child,
        //   regardless of how many chores the parent actually assigned in
        //   that child's plan (DailyManagementJson). Two children with a
        //   different number of assigned chores were never comparable,
        //   and a child who completed 100% of a short plan (e.g. 2 of 2
        //   assigned chores) could never reach 100%. choresMax now
        //   defaults to 5 (legacy behaviour for callers that don't pass
        //   plan data) but FromSingleEntry passes the child's REAL
        //   assigned-chore count when a parent plan exists.
        private static double CalcChores(DailyLogEntry e, double df, int choresMax = 5)
        {
            double effectiveChorsPct;
            if (e.ChorsPct > 0)
            {
                effectiveChorsPct = e.ChorsPct;
            }
            else
            {
                double countBasedPct = AutoCalcPctFromCount(e.CompletedChores, maxCount: choresMax);
                // HelpedFamily acts as a floor (a child who says "yes I helped" but
                // didn't list specific chores still gets meaningful credit — 40, not
                // 0) rather than a ceiling. The floor is kept low enough that the
                // count-based curve still drives visible movement between e.g. 1
                // and 2 completed chores instead of being masked by a flat value.
                effectiveChorsPct = e.HelpedFamily
                    ? Math.Max(40.0, countBasedPct)
                    : countBasedPct;
            }

            double helpBonus = e.HelpedFamily ? 100.0 : 0.0;
            double choreBonus = CountBonus(e.CompletedChores, maxCount: choresMax);
            double raw = (effectiveChorsPct * 0.75) + (helpBonus * 0.15) + (choreBonus * 0.10);
            return Clamp(raw * df);
        }


        // ── Pillar 5: PhysicalHealth ─────────────────────────────────
        // Primary:   FoodPct (auto-calculated, 80%)
        // Secondary: WaterGlasses adequacy (20%)
        //
        //   8 glasses/day = full 100 bonus; scales linearly below.
        private static double CalcFood(DailyLogEntry e, double df)
        {
            double effectiveFoodPct = e.FoodPct > 0
                ? e.FoodPct
                : AutoCalcPctFromCount(e.NutritionItems, maxCount: 3);

            double waterScore = Math.Min(e.WaterGlasses / 8.0 * 100.0, 100.0);
            double raw = (effectiveFoodPct * 0.80) + (waterScore * 0.20);
            return Clamp(raw * df);
        }

        // ── Pillar 6: PhysicalActivity ───────────────────────────────
        // Primary:   MovingPct (auto-calculated, 85%)
        // Secondary: PhysicalActivity description presence (15%)
        //
        //   Describing the activity (not just ticking a box) confirms quality.
        //
        //   FIX: effectiveMovingPct used to hard-code 90.0 for ANY non-"No"
        //   text, and actBonus was a flat 100.0 in that same case — so 1
        //   selected sport and 5 selected sports scored identically (91.5
        //   every time). The journal's sports section now shows the
        //   parent's named activities as a multi-select list (same pattern
        //   as Chores) when a parent plan exists, falling back to the
        //   literal Yes!/A little bit/No buttons otherwise. This mirrors
        //   CalcChores: count-based curve as the primary signal via
        //   AutoCalcPctFromCount, with the legacy "No"/"little bit" keyword
        //   checks kept only for the fallback (no-parent-plan) case where
        //   PhysicalActivity is literally one of those phrases, not a list
        //   of named activities.
        //
        //   BUG FIX: maxCount used to be hardcoded to 5 for every child,
        //   regardless of how many sports the parent actually assigned in
        //   that child's plan (DailyManagementJson). sportsMax now
        //   defaults to 5 (legacy behaviour for callers that don't pass
        //   plan data) but FromSingleEntry passes the child's REAL
        //   assigned-sport count when a parent plan exists.
        private static double CalcMoving(DailyLogEntry e, double df, int sportsMax = 5)
        {
            double effectiveMovingPct;
            if (e.MovingPct > 0)
            {
                effectiveMovingPct = e.MovingPct;
            }
            else if (string.IsNullOrWhiteSpace(e.PhysicalActivity))
            {
                effectiveMovingPct = 0.0;
            }
            else if (IsExplicitNo(e.PhysicalActivity))
            {
                effectiveMovingPct = 0.0;
            }
            else if (IsLiteralLittleBit(e.PhysicalActivity))
            {
                effectiveMovingPct = 50.0;
            }
            else
            {
                // Named activity (or activities) selected — scale with how many,
                // same curve used for Chores/Subjects/etc. A single "Yes!" fallback
                // tap still counts as 1 item and scores via the same curve.
                effectiveMovingPct = AutoCalcPctFromCount(e.PhysicalActivity, maxCount: sportsMax);
            }

            double actBonus = CountBonus(e.PhysicalActivity, maxCount: sportsMax);
            double raw = (effectiveMovingPct * 0.85) + (actBonus * 0.15);
            return Clamp(raw * df);
        }

        // ── Pillar 7: CreativityEngagement ───────────────────────────
        // Primary:   HobbiesPct (auto-calculated, 80%)
        // Secondary: FunActivities count bonus (20%)
        //
        //   Listing specific fun activities validates creative engagement.
        private static double CalcHobbies(DailyLogEntry e, double df)
        {
            double effectiveHobbiesPct = e.HobbiesPct > 0
                ? e.HobbiesPct
                : AutoCalcPctFromCount(e.FunActivities, maxCount: 4);

            double funBonus = CountBonus(e.FunActivities, maxCount: 4);
            double raw = (effectiveHobbiesPct * 0.80) + (funBonus * 0.20);
            return Clamp(raw * df);
        }

        // ── Pillar 8: MentalWellbeing ────────────────────────────────
        // No direct Pct field — derived from emotional/mood signals + challenge handling:
        //
        //   SentimentScore (NLP, –1 → +1)        — 60% weight
        //   MoodEmoji categorical score           — 25% weight
        //   ChallengeHandling resilience bonus    — 15% weight
        //
        // Duration factor still applied so a short, sad entry does not
        // drag down weekly averages as much as a long, sad one would.
        private static double CalcMental(DailyLogEntry e, double df)
        {
            double sentimentScore = SentimentToScore(e.SentimentScore);
            double moodScore = MoodEmojiToScore(e.MoodEmoji, e.MoodLevel);
            double challengeScore = ChallengeHandlingToMentalScore(e.ChallengeHandling);

            double raw = (sentimentScore * MentalSentimentWeight)
                       + (moodScore * MentalMoodWeight)
                       + (challengeScore * MentalChallengeWeight);

            return Clamp(raw * df);
        }

        // ─────────────────────────────────────────────────────────────
        //  Helper — Duration Factor
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Maps logDuration (minutes) to a normalisation multiplier:
        ///   • duration = 0      → MinDurationFactor (0.50)
        ///   • duration = 60     → 1.00  (reference point)
        ///   • duration ≥ 120    → MaxDurationFactor (1.20)  [capped]
        ///
        /// Uses piecewise linear interpolation between anchor points.
        /// </summary>
        private static double DurationFactor(int durationMinutes)
        {
            if (durationMinutes <= 0)
                return MinDurationFactor;

            double d = (double)durationMinutes;

            if (d <= ReferenceDurationMinutes)
            {
                // [0, 60] → [MinDurationFactor, 1.0]
                double t = d / ReferenceDurationMinutes;
                return MinDurationFactor + t * (1.0 - MinDurationFactor);
            }
            else
            {
                // [60, 120] → [1.0, MaxDurationFactor]
                double t = Math.Min((d - ReferenceDurationMinutes) / ReferenceDurationMinutes, 1.0);
                return 1.0 + t * (MaxDurationFactor - 1.0);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Helper — Sentiment Score → 0–100
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Converts an NLP SentimentScore (–1.0 → +1.0, or null) to a
        /// 0–100 scale.  Null (not analysed) assumes neutral (0.7 = 85).
        ///
        /// Mapping:
        ///   –1.0  →   0  (very negative)
        ///    0.0  →  50  (neutral)
        ///   +1.0  → 100  (very positive)
        /// </summary>
        private static double SentimentToScore(double? sentimentScore)
        {
            if (sentimentScore is null)
                return 70.0; // neutral-positive default (mirrors DailyLogClosureService)

            double s = Math.Clamp(sentimentScore.Value, -1.0, 1.0);
            return (s + 1.0) / 2.0 * 100.0;
        }

        // ─────────────────────────────────────────────────────────────
        //  Helper — MoodEmoji / MoodLevel → 0–100
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Maps mood signals to a 0–100 mental-wellbeing score.
        /// MoodEmoji is checked first; MoodLevel is used as fallback.
        /// Unknown / missing signals return 50 (neutral).
        /// </summary>
        private static double MoodEmojiToScore(string? moodEmoji, string? moodLevel)
        {
            // ── Emoji map ──────────────────────────────────────────
            // BUG FIX (Phase 14 Audit): 5 emojis sent by journal.html were
            // missing from this switch and fell to the _ => 50 default,
            // producing a neutral score regardless of the child's actual
            // feeling. Added below with calibrated values matching the
            // intended scoring range for each mood.
            if (!string.IsNullOrWhiteSpace(moodEmoji))
            {
                return moodEmoji.Trim() switch
                {
                    // Very positive
                    "😄" or "😁" or "🥳" or "🤩" or "😍" => 100,
                    // Positive — Excited (🎉) was previously unmapped, fell to 50 ❌ FIXED
                    "😊" or "🙂" or "😀" or "😃" or "🎉" => 85,
                    // Content / calm
                    "😌" or "🙏" or "😇" or "🌟" => 75,
                    // Neutral
                    "😐" or "😑" or "🤔" or "😶" => 50,
                    // Slightly low — Tired (😴) was previously unmapped, fell to 50 ❌ FIXED
                    "😕" or "🤨" or "😒" or "😴" => 35,
                    // Sad / stressed — Lonely (🥺) was previously unmapped, fell to 50 ❌ FIXED
                    "😢" or "😞" or "😔" or "😟" or "😥" or "🥺" => 20,
                    // Very negative — Scared (😨) and Hurt (💔) were previously unmapped, fell to 50 ❌ FIXED
                    "😭" or "😤" or "😠" or "🤯" or "😰" or "😨" or "💔" => 5,
                    // Unknown emoji
                    _ => 50,
                };
            }

            // ── MoodLevel text fallback ────────────────────────────
            // BUG FIX (Phase 14 Audit): the previous generic-English map
            // ("amazing", "great", "okay" …) never matched the actual
            // data-mood values sent by journal.html ("Good", "Happy",
            // "Excited", …). Every Growia mood name therefore fell to the
            // _ => 50 default. Updated to the exact 14 Growia mood strings
            // (case-insensitive). Generic words kept as a secondary
            // catch-all for any future callers that pass raw descriptors.
            if (!string.IsNullOrWhiteSpace(moodLevel))
            {
                return moodLevel.Trim().ToLowerInvariant() switch
                {
                    // ── Growia exact mood names (from journal.html data-mood) ──
                    // Very positive
                    "happy" or "excited" => 95,
                    // Positive
                    "good" or "proud" => 80,
                    // Slightly low
                    "tired" => 35,
                    // Lonely — between slightly-low and sad
                    "lonely" => 25,
                    // Sad / stressed
                    "sad" or "worried" => 20,
                    // Very negative
                    "overwhelmed" or "scared" or "angry" or "jealous"
                        or "confused" or "hurt" => 8,
                    // ── Generic English fallback (kept for non-Growia callers) ──
                    "amazing" or "fantastic" or "excellent" => 100,
                    "great" => 85,
                    "okay" or "fine" or "alright" => 60,
                    "neutral" => 50,
                    "low" or "not great" or "below par" => 30,
                    "bad" or "terrible" or "awful" => 10,
                    _ => 50,
                };
            }

            return 50.0; // neutral default when no mood data provided
        }

        // ─────────────────────────────────────────────────────────────
        //  Helper — Sports fallback-button detection (exact match only)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// True only when PhysicalActivity is exactly the legacy "No" fallback
        /// button (used when no parent-defined sports plan exists). Uses an
        /// exact, case-insensitive comparison per comma-separated token —
        /// NOT a substring check — so a parent-named activity that happens to
        /// contain "no" (e.g. a sport with "no" in its name) is never
        /// misread as a negative answer.
        /// </summary>
        private static bool IsExplicitNo(string? physicalActivity)
        {
            if (string.IsNullOrWhiteSpace(physicalActivity)) return false;
            var tokens = physicalActivity.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return tokens.Length > 0 && tokens.All(t => t.Equals("No", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// True only when PhysicalActivity is exactly the legacy "A little bit"
        /// fallback button. Exact match, not substring — see IsExplicitNo.
        /// </summary>
        private static bool IsLiteralLittleBit(string? physicalActivity)
        {
            if (string.IsNullOrWhiteSpace(physicalActivity)) return false;
            var tokens = physicalActivity.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return tokens.Length > 0 && tokens.All(t => t.Equals("A little bit", StringComparison.OrdinalIgnoreCase));
        }

        // ─────────────────────────────────────────────────────────────
        //  Helper — Comma-separated count → bonus score 0–100
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Counts non-empty items in a comma-separated string and scales
        /// to 0–100. E.g. maxCount=5: 0 items → 0, 5+ items → 100.
        /// </summary>
        private static double CountBonus(string? commaSeparated, int maxCount)
        {
            if (string.IsNullOrWhiteSpace(commaSeparated) || maxCount <= 0)
                return 0.0;

            int count = commaSeparated
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Length;

            return Math.Min((double)count / maxCount * 100.0, 100.0);
        }

        // ─────────────────────────────────────────────────────────────
        //  Helper — Auto-calculate Pct from activity count (0–100)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Auto-calculates a pillar Pct from the count of activity tags
        /// when the slider value is 0 (sliders removed from journal form).
        /// Uses a logarithmic-like curve: even 1 activity gives a meaningful
        /// score so children aren't penalised for having few options.
        ///   0 items → 0%
        ///   1 item  → ~55%
        ///   2 items → ~75%
        ///   3 items → ~88%
        ///   maxCount → 100%
        /// </summary>
        private static double AutoCalcPctFromCount(string? commaSeparated, int maxCount)
        {
            if (string.IsNullOrWhiteSpace(commaSeparated) || maxCount <= 0)
                return 0.0;

            int count = commaSeparated
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Length;

            if (count <= 0) return 0.0;

            // Soft-log curve: first item has high impact, diminishing returns
            double ratio = (double)count / maxCount;
            return Math.Min(100.0 * (1.0 - Math.Pow(1.0 - ratio, 1.6)), 100.0);
        }

        // ─────────────────────────────────────────────────────────────
        //  Helper — ChallengeHandling → MentalWellbeing score (0–100)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Maps the child's challenge-handling responses to a MentalWellbeing
        /// contribution score (0–100).
        ///
        /// Positive/resilient responses ("Stayed calm", "Tried again",
        /// "Didn't give up", "Asked for help") score higher.
        /// More positive responses = higher score (up to 100).
        /// No response = 50 (neutral, no penalty for not encountering challenges).
        /// </summary>
        private static double ChallengeHandlingToMentalScore(string? challengeHandling)
        {
            if (string.IsNullOrWhiteSpace(challengeHandling))
                return 50.0; // Neutral — no challenges logged today

            var items = challengeHandling
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            double totalScore = 0.0;
            int scoredItems = 0;

            foreach (var item in items)
            {
                double itemScore = item.ToLowerInvariant() switch
                {
                    var s when s.Contains("calm") => 90.0,
                    var s when s.Contains("tried again") => 85.0,
                    var s when s.Contains("didn't give up") => 88.0,
                    var s when s.Contains("asked for help") => 82.0,
                    var s when s.Contains("talked") => 80.0,
                    var s when s.Contains("breathed") => 85.0,
                    var s when s.Contains("gave up") => 25.0,
                    var s when s.Contains("upset") => 30.0,
                    var s when s.Contains("cried") => 35.0,
                    _ => 65.0, // Unknown but logged → positive signal
                };
                totalScore += itemScore;
                scoredItems++;
            }

            if (scoredItems == 0) return 50.0;

            // Average of all scored items, slightly boosted by quantity (up to +10)
            double avg = totalScore / scoredItems;
            double bonus = Math.Min(scoredItems * 3.0, 10.0);
            return Math.Min(avg + bonus, 100.0);
        }

        // ─────────────────────────────────────────────────────────────
        //  Helper — ChallengeHandling → Social quality bonus (0–100)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Maps challenge-handling responses to a social interaction bonus.
        /// Prosocial coping ("Asked for help", "Stayed calm", "Talked") signals
        /// healthy peer and family interaction, boosting SocialInteraction_Pct.
        /// </summary>
        private static double ChallengeHandlingToSocialBonus(string? challengeHandling)
        {
            if (string.IsNullOrWhiteSpace(challengeHandling))
                return 0.0;

            var items = challengeHandling
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            double score = 0.0;
            foreach (var item in items)
            {
                score += item.ToLowerInvariant() switch
                {
                    var s when s.Contains("asked for help") => 100.0, // Strongest social signal
                    var s when s.Contains("talked") => 90.0,
                    var s when s.Contains("calm") => 70.0,
                    var s when s.Contains("tried again") => 60.0,
                    var s when s.Contains("didn't give up") => 60.0,
                    _ => 30.0,
                };
            }

            return Math.Min(score / items.Length, 100.0);
        }

        // ─────────────────────────────────────────────────────────────
        //  Helper — Clamp & Round
        // ─────────────────────────────────────────────────────────────

        private static double Clamp(double value)
            => Math.Round(Math.Clamp(value, 0.0, 100.0), 2);

        private static PillarResult ZeroResult() => new()
        {
            SpiritualConnection_Pct = 0,
            SocialInteraction_Pct = 0,
            AcademicProgress_Pct = 0,
            HomeResponsibility_Pct = 0,
            PhysicalHealth_Pct = 0,
            PhysicalActivity_Pct = 0,
            CreativityEngagement_Pct = 0,
            MentalWellbeing_Pct = 0,
        };
    }
}
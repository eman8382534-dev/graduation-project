// ============================================================
//  PillarPipelineService.cs — Leafy AI System  (Phase 11)
//
//  THE SINGLE SOURCE OF TRUTH for Progress / Pillars / Traits.
//
//  Replaces ALL of the following competing calculation paths,
//  which are now REMOVED:
//    • ChildController.SaveJournal   — 90-day FromWeeklyEntries
//    • ChildController.QuickCheckIn  — ad-hoc additive increments
//                                       + 90-day FromWeeklyEntries
//    • WeeklyAggregationTrigger      — 7-day on-demand pipeline
//    • WeeklyTraitSynthesisService   — 7-day 24h background pipeline
//
//  PIPELINE (exactly one path, always run in this order):
//
//    Daily Journal Save (SaveJournal / QuickCheckIn / AI annotation)
//      → Step 1: ApplyDailyPillars(entry)
//                 PillarCalculationEngine.FromSingleEntry(entry)
//                 → entry.DailyXxx_Pct (8 fields) + PillarsCalculatedAt
//      → Step 2: RecalculateProfileAggregatesAsync(childId)
//                 Simple arithmetic mean of DailyXxx_Pct over the last
//                 30 days (IsJournalSaved entries only)
//                 → profile.Xxx_Pct (8 canonical pillar columns)
//      → Step 3: RecalculateTraits(profile)
//                 TraitSynthesisEngine.Synthesise(profile, neutral AI)
//                 → profile.{Kindness,Vitality,Curiosity,Resilience,
//                   Spiritual}Trait + profile.ResiliencePct (R7 sync)
//      → Save
//
//  AI INTEGRATION RULE (Step 5 of the refactor spec):
//    The external AI team writes ONLY DailyLogEntry.SentimentScore /
//    DetectedEmotion / AnomalyScore / NlpAnomalyScore / AnomalyFlag /
//    IsHonest (via AiController — unchanged). AiController then calls
//    THIS SAME pipeline on the annotated entry — not a separate
//    formula, not a "weekly aggregation". This lets
//    MentalWellbeing_Pct's SentimentScore term (60% weight in
//    PillarCalculationEngine.CalcMental) become accurate once AI
//    results arrive, without introducing any second calculation path.
//
//  TRAIT SOURCE RULE (Step 3 of the refactor spec):
//    Traits are derived from the 8 aggregated profile pillar columns
//    just written in Step 2, PLUS the real AI signals (SentimentScore,
//    AnomalyFlag) aggregated from the same 30-day window of
//    DailyLogEntry rows already loaded in Step 2. This is what lets
//    Kindness's sentimentBoost term and Resilience's sentiment/anomaly
//    terms (both AI-modulated, per TraitSynthesisEngine's design spec)
//    actually receive live data instead of a hardcoded neutral stub.
//
//    AI signal aggregation (BuildAiAnalyticsResult, below):
//      • SentimentScore → average of each entry's raw SentimentScore
//        (–1.0 → +1.0, or null) converted to 0–100 via the same
//        mapping PillarCalculationEngine.SentimentToScore uses
//        (–1.0 → 0, 0.0 → 50, +1.0 → 100; null entries default to 70,
//        i.e. neutral-positive — entries never analysed by the AI
//        team don't drag the average toward "very negative").
//      • AnomalyFlag → true if ANY entry in the 30-day window was
//        flagged by the AI team, mirroring how a single inconsistent
//        log already destabilises that week's other AI-derived
//        figures (ScoreVariance, NlpPillars, etc).
//      • If the window has no entries, signals fall back to neutral
//        (SentimentScore = 0 on the engine's input scale, AnomalyFlag
//        = false) — same behaviour as before for children with no
//        logged history yet.
// ============================================================

using GrawiaaApp.API.Data;
using GrawiaaApp.API.DTOs;
using GrawiaaApp.API.Models;
using Microsoft.EntityFrameworkCore;

namespace GrawiaaApp.API.Services
{
    public interface IPillarPipelineService
    {
        /// <summary>
        /// STEP 1 — Computes the 8 daily pillar percentages for this
        /// entry via PillarCalculationEngine.FromSingleEntry and stores
        /// them on the entry's DailyXxx_Pct fields. Does not save.
        /// Returns the PillarResult so callers can derive audit
        /// fields (DeltaAcademic, etc.) without recomputing.
        /// </summary>
        PillarResult ApplyDailyPillars(DailyLogEntry entry);

        /// <summary>
        /// STEP 2 — Recalculates ChildProfile's 8 pillar columns as the
        /// simple arithmetic mean of DailyXxx_Pct across the last 30
        /// days of IsJournalSaved entries for this child, then
        /// (STEP 3) re-derives the 5 traits from those aggregates.
        /// Loads/updates the tracked ChildProfile but does not call
        /// SaveChangesAsync — caller continues its own unit of work.
        /// Returns the updated profile (or null if no profile exists).
        /// </summary>
        Task<ChildProfile?> RecalculateProfileAndTraitsAsync(int childId);

        /// <summary>
        /// Convenience orchestrator: runs Step 1 on <paramref name="entry"/>
        /// then Steps 2–3 for entry.ChildId. Does not save — caller
        /// continues its own unit of work (matches existing controller
        /// patterns where SaveChangesAsync is called once at the end).
        /// </summary>
        Task<PillarResult> RunFullPipelineAsync(DailyLogEntry entry);

        /// <summary>
        /// Recomputes ChildProfile.TotalTasksCount and CompletedTasksCount
        /// for the given child from ChildTask rows. Called after:
        ///   (a) Journal save (SpiritualActivities sync → IsCompletedToday)
        ///   (b) Task completion (TasksController.CompleteTask)
        ///   (c) Nightly reset (DailyLogClosureService)
        /// Does NOT call SaveChangesAsync — caller is responsible.
        /// </summary>
        Task RecalculateTaskCountsAsync(int childId);
    }

    public sealed class PillarPipelineService : IPillarPipelineService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITrustScoreEngine _trustScoreEngine;

        // 30-day rolling window, per refactor spec STEP 2.
        private const int AggregationWindowDays = 30;

        public PillarPipelineService(ApplicationDbContext context, ITrustScoreEngine trustScoreEngine)
        {
            _context = context;
            _trustScoreEngine = trustScoreEngine;
        }

        public PillarResult ApplyDailyPillars(DailyLogEntry entry)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));

            // ── BUG FIX (per-child plan denominators never wired up) ──────
            // PillarCalculationEngine already supports a ChildPlanLimits
            // parameter (real chores/subjects/sports counts from this
            // child's parent-assigned plan) specifically so two children
            // with differently-sized plans are comparable, and a child who
            // completes 100% of a SHORT plan (e.g. 2 of 2 assigned chores)
            // can actually reach 100%. But this — the single source of
            // truth call site that runs on every journal save — was always
            // calling FromSingleEntry(entry) with NO plan limits, so EVERY
            // child was silently scored against the global defaults
            // (5 chores / 6 subjects / 5 sports) no matter what the parent
            // actually assigned. A child with only 2 assigned chores who
            // completed both could never show 100% Home Responsibility,
            // and a child who completed 1 of 2 assigned chores scored as
            // if they'd only done 1 of 5 — making good performance look
            // like a red flag. BuildPlanLimits below reads the real
            // counts from ChildProfile.DailyManagementJson and falls back
            // to the defaults only when no parent plan exists yet.
            var planLimits = BuildPlanLimits(entry.ChildId);
            var result = PillarCalculationEngine.FromSingleEntry(entry, planLimits);

            entry.DailySpiritualConnection_Pct = result.SpiritualConnection_Pct;
            entry.DailySocialInteraction_Pct = result.SocialInteraction_Pct;
            entry.DailyAcademicProgress_Pct = result.AcademicProgress_Pct;
            entry.DailyHomeResponsibility_Pct = result.HomeResponsibility_Pct;
            entry.DailyPhysicalHealth_Pct = result.PhysicalHealth_Pct;
            entry.DailyPhysicalActivity_Pct = result.PhysicalActivity_Pct;
            entry.DailyCreativityEngagement_Pct = result.CreativityEngagement_Pct;
            entry.DailyMentalWellbeing_Pct = result.MentalWellbeing_Pct;
            entry.PillarsCalculatedAt = DateTime.UtcNow;

            // ── AUDIT FIX (2026-06-21): Emotion Safety `Alert` bridge ──────
            // Confirmed with the AI team: the Emotion Safety Engine writes
            // its output DIRECTLY via raw SQL to DailyLogEntry.Alert (plain
            // string column, no flag). EmotionSafetyAlertFlag/Text are NOT
            // written by the AI directly — they're derived here from Alert
            // so the existing parent-dashboard filtering (ParentController)
            // and active-alert promotion logic (NlpPillarSyncService) keep
            // working unchanged. Alert is the single source of truth; this
            // derivation runs every time ApplyDailyPillars runs — on journal
            // save, on POST /api/ai/log-result, AND on NlpPillarSyncService's
            // periodic re-run (which is what actually picks up the AI's
            // direct-SQL write, same bridge role it already plays for the
            // NLP pillars and AnomalyFlag).
            bool hasAlertText = !string.IsNullOrWhiteSpace(entry.Alert);
            entry.EmotionSafetyAlertFlag = hasAlertText;
            entry.EmotionSafetyAlertText = hasAlertText ? entry.Alert : null;

            return result;
        }

        /// <summary>
        /// Builds the REAL ChildPlanLimits for <paramref name="childId"/> from
        /// ChildProfile.DailyManagementJson — the parent's actual assigned
        /// chores/subjects/sports list (see ParentController.UpdateHeroManagement
        /// / GetHeroManagement, which read/write the same JSON shape).
        ///
        /// A synchronous, no-tracking, single-column query — cheap enough to run
        /// on every journal save and avoids turning ApplyDailyPillars's public
        /// signature async (every existing caller invokes it synchronously,
        /// immediately followed by an awaited RecalculateProfileAndTraitsAsync
        /// call in the same method).
        ///
        /// Falls back to ChildPlanLimits.Default (the legacy global 5/6/5
        /// constants) when the child has no plan configured yet or the JSON
        /// fails to parse — identical to the pre-fix behaviour in that case,
        /// so children without a parent-assigned plan are unaffected.
        /// </summary>
        private ChildPlanLimits BuildPlanLimits(int childId)
        {
            var json = _context.ChildProfiles
                .AsNoTracking()
                .Where(p => p.UserId == childId)
                .Select(p => p.DailyManagementJson)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(json))
                return ChildPlanLimits.Default;

            try
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<HeroManagementDto>(
                    json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (data is null)
                    return ChildPlanLimits.Default;

                return ChildPlanLimits.FromCounts(
                    choresCount: data.Chores?.Count ?? 0,
                    subjectsCount: data.Subjects?.Count ?? 0,
                    sportsCount: data.Sports?.Count ?? 0);
            }
            catch (System.Text.Json.JsonException)
            {
                // Malformed JSON (shouldn't happen — only ParentController writes
                // this field — but never let a bad row crash a journal save).
                return ChildPlanLimits.Default;
            }
        }

        public async Task<ChildProfile?> RecalculateProfileAndTraitsAsync(int childId)
        {
            var profile = await _context.ChildProfiles
                .FirstOrDefaultAsync(p => p.UserId == childId);

            if (profile is null) return null;

            var windowStart = DateTime.UtcNow.Date.AddDays(-(AggregationWindowDays - 1));

            var windowEntries = await _context.DailyLogEntries
                .Where(l => l.ChildId == childId
                         && l.LogDate.Date >= windowStart
                         && l.IsJournalSaved)
                .ToListAsync();

            // ── STEP 2: 30-day arithmetic mean of daily STRUCTURED pillars ─
            var aggregate = PillarCalculationEngine.From30DayDailyPillars(windowEntries);
            PillarCalculationEngine.ApplyToProfile(profile, aggregate);

            profile.LastAggregatedAt = DateTime.UtcNow;
            profile.AggregationWeekStart = windowStart;

            // ── MeanScore (legacy / AI team spec backward compat) ───────────
            profile.MeanScore = (
                profile.SpiritualConnection_Pct +
                profile.SocialInteraction_Pct +
                profile.AcademicProgress_Pct +
                profile.HomeResponsibility_Pct +
                profile.PhysicalHealth_Pct +
                profile.PhysicalActivity_Pct +
                profile.CreativityEngagement_Pct +
                profile.MentalWellbeing_Pct
            ) / 8.0;

            // ── STEP 2b: 30-day arithmetic mean of NLP pillars ─────────────
            // Only entries where the AI team has already written NlpXxx_Pct
            // (NlpPillarsCalculatedAt is set) are included; others default 0.
            var nlpEntries = windowEntries
                .Where(e => e.NlpPillarsCalculatedAt.HasValue)
                .ToList();

            if (nlpEntries.Count > 0)
            {
                profile.NlpSpiritualConnection_Pct = nlpEntries.Average(e => e.NlpSpiritualConnection_Pct);
                profile.NlpSocialInteraction_Pct = nlpEntries.Average(e => e.NlpSocialInteraction_Pct);
                profile.NlpAcademicProgress_Pct = nlpEntries.Average(e => e.NlpAcademicProgress_Pct);
                profile.NlpHomeResponsibility_Pct = nlpEntries.Average(e => e.NlpHomeResponsibility_Pct);
                profile.NlpPhysicalHealth_Pct = nlpEntries.Average(e => e.NlpPhysicalHealth_Pct);
                profile.NlpPhysicalActivity_Pct = nlpEntries.Average(e => e.NlpPhysicalActivity_Pct);
                profile.NlpCreativityEngagement_Pct = nlpEntries.Average(e => e.NlpCreativityEngagement_Pct);
                profile.NlpMentalWellbeing_Pct = nlpEntries.Average(e => e.NlpMentalWellbeing_Pct);
            }
            // else: NLP averages stay at their current stored value (do not reset to 0
            // just because no NLP data arrived in the last 30 days — that would trash
            // the hybrid formula if the AI pipeline is temporarily offline).

            // ── STEP 2c: Final Hybrid Pillars (70% Structured + 30% NLP) ──
            profile.FinalSpiritualConnection_Pct = HybridPillar(profile.SpiritualConnection_Pct, profile.NlpSpiritualConnection_Pct);
            profile.FinalSocialInteraction_Pct = HybridPillar(profile.SocialInteraction_Pct, profile.NlpSocialInteraction_Pct);
            profile.FinalAcademicProgress_Pct = HybridPillar(profile.AcademicProgress_Pct, profile.NlpAcademicProgress_Pct);
            profile.FinalHomeResponsibility_Pct = HybridPillar(profile.HomeResponsibility_Pct, profile.NlpHomeResponsibility_Pct);
            profile.FinalPhysicalHealth_Pct = HybridPillar(profile.PhysicalHealth_Pct, profile.NlpPhysicalHealth_Pct);
            profile.FinalPhysicalActivity_Pct = HybridPillar(profile.PhysicalActivity_Pct, profile.NlpPhysicalActivity_Pct);
            profile.FinalCreativityEngagement_Pct = HybridPillar(profile.CreativityEngagement_Pct, profile.NlpCreativityEngagement_Pct);
            profile.FinalMentalWellbeing_Pct = HybridPillar(profile.MentalWellbeing_Pct, profile.NlpMentalWellbeing_Pct);

            profile.FinalMeanScore = (
                profile.FinalSpiritualConnection_Pct +
                profile.FinalSocialInteraction_Pct +
                profile.FinalAcademicProgress_Pct +
                profile.FinalHomeResponsibility_Pct +
                profile.FinalPhysicalHealth_Pct +
                profile.FinalPhysicalActivity_Pct +
                profile.FinalCreativityEngagement_Pct +
                profile.FinalMentalWellbeing_Pct
            ) / 8.0;

            // ── STEP 3: Traits derived from the aggregates above, ──
            // enriched with this child's real 30-day AI signals
            // (same windowEntries already loaded for Step 2 — no
            // extra query needed).
            RecalculateTraits(profile, windowEntries);

            // ── TrustScore (fallback formula — skipped when AI set TrustScoreSetAt) ──
            await _trustScoreEngine.TryRecalculateAndPersistAsync(childId, profile);

            return profile;
        }

        public async Task<PillarResult> RunFullPipelineAsync(DailyLogEntry entry)
        {
            var dailyResult = ApplyDailyPillars(entry);
            await RecalculateProfileAndTraitsAsync(entry.ChildId);
            return dailyResult;
        }

        /// <summary>
        /// Recomputes ChildProfile.TotalTasksCount and CompletedTasksCount
        /// from ChildTask rows for this child.
        ///
        /// TotalTasksCount = all tasks excluding plan-template rows
        ///   (fixed recurring + parent-assigned completable tasks).
        ///   Plan-template rows (Description == "__plan_template__") are
        ///   reference config for the journal, not completable items.
        ///
        /// CompletedTasksCount = today's completions:
        ///   • Fixed tasks (IsFixedTask = true):  IsCompletedToday = true
        ///     (synced from SpiritualActivities at journal save, resets nightly)
        ///   • Parent-assigned tasks (IsParentAssigned = true, no plan-template):
        ///     IsCompleted = true (permanent, never resets)
        ///
        /// Does NOT call SaveChangesAsync — caller is responsible.
        /// </summary>
        public async Task RecalculateTaskCountsAsync(int childId)
        {
            var profile = await _context.ChildProfiles
                .FirstOrDefaultAsync(p => p.UserId == childId);
            if (profile is null) return;

            var tasks = await _context.ChildTasks
                .Where(t => t.ChildId == childId && t.Description != "__plan_template__")
                .ToListAsync();

            profile.TotalTasksCount = tasks.Count;

            profile.CompletedTasksCount =
                tasks.Count(t => t.IsFixedTask ? t.IsCompletedToday
                                                 : t.IsCompleted);
        }

        /// <summary>
        /// Hybrid formula: FinalPillar = 70% Structured + 30% NLP.
        /// When NLP data is absent (zero) the result degrades gracefully
        /// to 70% of the structured value — it does NOT collapse to zero.
        /// Both inputs are clamped to [0, 100] before blending.
        /// </summary>
        private static double HybridPillar(double structured, double nlp)
        {
            double s = Math.Max(0, Math.Min(100, structured));
            double n = Math.Max(0, Math.Min(100, nlp));
            return Math.Round(0.70 * s + 0.30 * n, 4);
        }

        /// <summary>
        /// STEP 3 — Re-derives the 5 personality traits from
        /// profile.SpiritualConnection_Pct / SocialInteraction_Pct / ...
        /// (the 8 aggregates just written by Step 2) PLUS this child's
        /// real AI signals, aggregated from <paramref name="windowEntries"/>
        /// (the same 30-day window already loaded for Step 2 — see
        /// BuildAiAnalyticsResult). Uses TraitSynthesisEngine's existing
        /// weighted formulas unchanged; only the AI input that was
        /// previously hardcoded to neutral is now real.
        /// </summary>
        private static void RecalculateTraits(ChildProfile profile, List<DailyLogEntry> windowEntries)
        {
            var ai = BuildAiAnalyticsResult(windowEntries);

            var engine = new TraitSynthesisEngine();
            var result = engine.Synthesise(profile, ai);
            engine.ApplyToProfile(profile, result); // also syncs ResiliencePct (R7)

            profile.LastTraitSynthesisAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Aggregates the real AI fields already written on this child's
        /// 30-day window of DailyLogEntry rows into the single
        /// AiAnalyticsResult TraitSynthesisEngine expects — replacing the
        /// previous hardcoded neutral stub (SentimentScore = 0,
        /// AnomalyFlag = false) that silently zeroed out Kindness's
        /// sentiment boost and Resilience's sentiment/anomaly terms.
        ///
        ///   • SentimentScore (0–100, engine scale) = average of each
        ///     entry's raw DailyLogEntry.SentimentScore (–1.0 → +1.0, or
        ///     null) converted via the SAME mapping
        ///     PillarCalculationEngine.SentimentToScore uses:
        ///       –1.0 → 0, 0.0 → 50, +1.0 → 100; null → 70 (neutral-positive
        ///       default, so entries the AI team hasn't analysed yet don't
        ///       drag the average down).
        ///   • AnomalyFlag = true if ANY entry in the window was flagged
        ///     by the AI team — one inconsistent log is enough to lower
        ///     confidence across the whole window, same logic already
        ///     used for ScoreVariance / NlpPillars elsewhere in this
        ///     pipeline.
        ///
        /// When the window is empty (no logs yet), falls back to neutral
        /// (SentimentScore = 0, AnomalyFlag = false) — identical to the
        /// previous behaviour for children with no history.
        /// </summary>
        private static AiAnalyticsResult BuildAiAnalyticsResult(List<DailyLogEntry> windowEntries)
        {
            if (windowEntries is null || windowEntries.Count == 0)
            {
                return new AiAnalyticsResult
                {
                    SentimentScore = 0,
                    DetectedEmotion = "Neutral",
                    AnomalyFlag = false,
                };
            }

            double avgSentiment = windowEntries.Average(e => SentimentToEngineScale(e.SentimentScore));
            bool anyAnomaly = windowEntries.Any(e => e.AnomalyFlag);

            // Most frequently detected emotion in the window (falls back to
            // "Neutral" when no entry has one set yet).
            string detectedEmotion = windowEntries
                .Where(e => !string.IsNullOrWhiteSpace(e.DetectedEmotion))
                .GroupBy(e => e.DetectedEmotion!)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault() ?? "Neutral";

            return new AiAnalyticsResult
            {
                SentimentScore = (int)Math.Round(avgSentiment),
                DetectedEmotion = detectedEmotion,
                AnomalyFlag = anyAnomaly,
            };
        }

        /// <summary>
        /// Converts a single entry's raw NLP SentimentScore (–1.0 → +1.0,
        /// or null) to the 0–100 scale TraitSynthesisEngine expects.
        /// Identical mapping to PillarCalculationEngine.SentimentToScore
        /// (kept as a separate private copy here since that method is
        /// private to the pillar engine and this pipeline must not modify
        /// the pillar engine per its design contract).
        ///   –1.0 → 0 (very negative), 0.0 → 50 (neutral), +1.0 → 100 (very positive)
        ///   null → 70 (neutral-positive default — entry not yet analysed)
        /// </summary>
        private static double SentimentToEngineScale(double? sentimentScore)
        {
            if (sentimentScore is null)
                return 70.0;

            double s = Math.Clamp(sentimentScore.Value, -1.0, 1.0);
            return (s + 1.0) / 2.0 * 100.0;
        }
    }
}
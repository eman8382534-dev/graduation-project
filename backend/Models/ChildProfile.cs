// ============================================================
//  ChildProfile.cs — Leafy AI System
//
//  PHASE 11 — SINGLE-SOURCE PIPELINE
//  ─────────────────────────────────────────────────────────
//  1. The 8 pillar percentages below are a SIMPLE ARITHMETIC
//     MEAN of DailyLogEntry.DailyXxx_Pct over the trailing
//     30 days (IsJournalSaved entries only). No duration
//     weighting, no 7-day coverage penalty.
//  2. DailyLogEntry.DailyXxx_Pct are computed exactly once per
//     save via PillarCalculationEngine.FromSingleEntry — that
//     per-entry result is the SINGLE SOURCE OF TRUTH for "this
//     day's performance".
//  3. No manual writes to pillar percentage fields outside
//     PillarPipelineService.
//  4. PillarPipelineService.RecalculateProfileAndTraitsAsync
//     recomputes both the 8 pillars (Step 2) and the 5 traits
//     (Step 3) after every journal save / quick check-in / AI
//     annotation. See Services/PillarPipelineService.cs.
//  ─────────────────────────────────────────────────────────
//
//  Eight Pillars → DailyLogEntry source mapping:
//  ──────────────────────────────────────────────
//  SpiritualConnection_Pct   ← AVG(DailySpiritualConnection_Pct)
//  SocialInteraction_Pct     ← AVG(DailySocialInteraction_Pct)
//  AcademicProgress_Pct      ← AVG(DailyAcademicProgress_Pct)
//  HomeResponsibility_Pct    ← AVG(DailyHomeResponsibility_Pct)
//  PhysicalHealth_Pct        ← AVG(DailyPhysicalHealth_Pct)
//  PhysicalActivity_Pct      ← AVG(DailyPhysicalActivity_Pct)
//  CreativityEngagement_Pct  ← AVG(DailyCreativityEngagement_Pct)
//  MentalWellbeing_Pct       ← AVG(DailyMentalWellbeing_Pct)
//  (each DailyXxx_Pct is itself PillarCalculationEngine.FromSingleEntry
//   for that day — see DailyLogEntry.cs for the raw-input → daily-pillar
//   formula mapping.)
//
//  Trait → Section Mapping Matrix (Spec Rule B):
//  ──────────────────────────────────────────────
//  Connection with Allah   → Primary: Spiritual   | Secondary: Commitment
//  Social Interaction      → Primary: Kindness    | Secondary: Resilience
//  Homework & Study        → Primary: Curiosity   | Secondary: Commitment
//  Family Chores           → Primary: Kindness    | Secondary: Commitment
//  Nutrition & Food        → Primary: Vitality    | Secondary: Resilience
//  Sport & Movement        → Primary: Vitality    | Secondary: Resilience
//  Hobbies                 → Primary: Curiosity   | Secondary: Vitality
//  Mental Health (Mood)    → Primary: Resilience  | Secondary: Kindness
// ============================================================

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GrawiaaApp.API.Models
{
    public class ChildProfile
    {
        // ── Identity / Ownership ──────────────────────────────────────

        [Key, ForeignKey("User")]
        public int UserId { get; set; }
        public virtual ApplicationUser User { get; set; } = null!;

        public bool IsStandalone { get; set; } = true;

        public int? ParentId { get; set; }
        [ForeignKey("ParentId")]
        public virtual ParentProfile? Parent { get; set; }

        // ── Basic Profile Info ────────────────────────────────────────

        public int Age { get; set; }
        public string? CharacterName { get; set; }
        public string? CharacterType { get; set; }

        // ── Device / Security ─────────────────────────────────────────
        public string? LastKnownIp { get; set; }
        public string? DeviceFingerprint { get; set; }
        public bool? PasswordResetPending { get; set; } = false;
        public string? ResetToken { get; set; }

        // ── Gamification ──────────────────────────────────────────────
        public int Level { get; set; } = 1;
        public int CurrentExp { get; set; } = 0;
        // BUG FIX (Phase 11): Changed default from 100 to 70.
        // A default of 100 caused ALL trust score gates (50, 40, 60, 75, 90) to pass
        // trivially for every new user, making the gate completely non-functional.
        // 70 is a neutral "good start" score that still blocks Level 4→5 (requires 75)
        // and Level 5→6 (requires 90) until the child has demonstrated consistent
        // honesty (TrustScore raised via GamingService.UpdateScore on level-ups etc.).
        public int TrustScore { get; set; } = 70;

        /// <summary>
        /// When set, TrustScore was written by the external AI team (direct SQL or API)
        /// and the backend TrustScoreEngine must NOT recompute or overwrite it.
        /// </summary>
        public DateTime? TrustScoreSetAt { get; set; }

        public string? DailyManagementJson { get; set; }

        /// <summary>
        /// Aggregated past illness types from journal entries (comma-separated).
        /// Updated when child logs WasIllToday on any entry.
        /// </summary>
        [MaxLength(1000)]
        public string? PastIllnessSummary { get; set; }

        /// <summary>
        /// Latest AI-generated avoidance suggestion based on past/current illness.
        /// Written by Leafy RAG (external AI team).
        /// </summary>
        [MaxLength(500)]
        public string? IllnessAvoidanceTip { get; set; }

        // ── Linking ───────────────────────────────────────────────────
        public string? PendingParentEmail { get; set; }
        public string? PendingLinkToken { get; set; }

        // ── Category ─────────────────────────────────────────────────
        public Category Category { get; set; } = Category.Feelings;

        // ════════════════════════════════════════════════════════════════
        //  EIGHT PILLARS — Weekly Aggregated Analytics (Tier 2: Synthesis)
        //  These are computed from DailyLogEntry records by the engine.
        //  Column names follow the canonical Leafy naming convention.
        // ════════════════════════════════════════════════════════════════

        /// <summary>Pillar 1 — Connection with Allah & spiritual habits (AVG of SpiritualPct).</summary>
        [Column("SpiritualConnection_Pct")]
        public double SpiritualConnection_Pct { get; set; }

        /// <summary>Pillar 2 — Social behavior & peer interaction (AVG of SocialPct).</summary>
        [Column("SocialInteraction_Pct")]
        public double SocialInteraction_Pct { get; set; }

        /// <summary>Pillar 3 — Homework, study & learning performance (AVG of StudyingPct).</summary>
        [Column("AcademicProgress_Pct")]
        public double AcademicProgress_Pct { get; set; }

        /// <summary>Pillar 4 — Family chores & home responsibilities (AVG of ChorsPct).</summary>
        [Column("HomeResponsibility_Pct")]
        public double HomeResponsibility_Pct { get; set; }

        /// <summary>Pillar 5 — Nutrition, eating habits & body wellness (AVG of FoodPct).</summary>
        [Column("PhysicalHealth_Pct")]
        public double PhysicalHealth_Pct { get; set; }

        /// <summary>Pillar 6 — Sports, movement & energy level (AVG of MovingPct).</summary>
        [Column("PhysicalActivity_Pct")]
        public double PhysicalActivity_Pct { get; set; }

        /// <summary>Pillar 7 — Hobbies, creativity & self-expression (AVG of HobbiesPct).</summary>
        [Column("CreativityEngagement_Pct")]
        public double CreativityEngagement_Pct { get; set; }

        /// <summary>
        /// Pillar 8 — Emotions, stress & mental health state.
        /// NOTE (audit fix #4 — read before treating this as "structured"):
        /// despite living on the Structured/70% side of the Hybrid Pillar
        /// formula, 60% of THIS pillar's own weight is SentimentScore —
        /// an AI/NLP field written exclusively by the external AI team
        /// (DailyLogEntry.SentimentScore), not a child journal input. Only
        /// the remaining 40% (MoodEmoji 25% + ChallengeHandling 15%) is
        /// purely structured. See PillarCalculationEngine.CalcMental for
        /// the exact split and FinalMentalWellbeing_Pct below for how this
        /// compounds with the NLP pillar in the Final Hybrid formula.
        /// This is an accepted, intentional part of the hybrid design —
        /// documented here so it's not mistaken for a bug.
        /// </summary>
        [Column("MentalWellbeing_Pct")]
        public double MentalWellbeing_Pct { get; set; }

        // ── Backward-Compatibility Aliases (NotMapped) ────────────────
        [NotMapped] public double ReligiousPct { get => SpiritualConnection_Pct; set => SpiritualConnection_Pct = value; }
        [NotMapped] public double SocialPct { get => SocialInteraction_Pct; set => SocialInteraction_Pct = value; }
        [NotMapped] public double AcademicPct { get => AcademicProgress_Pct; set => AcademicProgress_Pct = value; }
        [NotMapped] public double EnvPct { get => HomeResponsibility_Pct; set => HomeResponsibility_Pct = value; }
        [NotMapped] public double PhysicalPct { get => PhysicalHealth_Pct; set => PhysicalHealth_Pct = value; }
        [NotMapped] public double SportPct { get => PhysicalActivity_Pct; set => PhysicalActivity_Pct = value; }
        [NotMapped] public double CreaPct { get => CreativityEngagement_Pct; set => CreativityEngagement_Pct = value; }
        [NotMapped] public double MentalPct { get => MentalWellbeing_Pct; set => MentalWellbeing_Pct = value; }
        [NotMapped] public double EmotionalPct { get => MentalWellbeing_Pct; set => MentalWellbeing_Pct = value; }

        public double ResiliencePct { get; set; }

        // ════════════════════════════════════════════════════════════════
        //  MEAN SCORE  (Phase 12 — AI Team Spec Input)
        //
        //  Simple arithmetic mean of the 8 pillar percentages above.
        //  Spec name: "MeanScore" — the AI team's Anomaly Detection Trust
        //  Engine reads this directly as one of its daily inputs (alongside
        //  DailyLogEntry.LogDuration and the ChildTask data exposed
        //  via GET /api/ai/tasks/{hashedChildId}).
        //
        //  Recomputed in the SAME place the 8 pillars themselves are
        //  recomputed — PillarPipelineService.RecalculateProfileAndTraitsAsync
        //  — so it is always in sync and never independently stale.
        //  The backend writes this; the AI team only ever reads it.
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// AI team spec: "MeanScore" — arithmetic mean of the 8 pillar
        /// percentages (SpiritualConnection, SocialInteraction, AcademicProgress,
        /// HomeResponsibility, PhysicalHealth, PhysicalActivity,
        /// CreativityEngagement, MentalWellbeing). Range 0–100.
        /// Recomputed by PillarPipelineService every time the 8 pillars change.
        /// </summary>
        public double MeanScore { get; set; }

        // ── Aggregation Metadata ──────────────────────────────────────
        public DateTime? LastAggregatedAt { get; set; }
        public DateTime? AggregationWeekStart { get; set; }

        // (Weekly raw pct averages and weekly AI aggregation fields removed — Phase 14 cleanup.
        //  Those columns had no writer since Phase 11 deleted WeeklyAggregationTrigger.
        //  Database columns dropped via migration Phase14_DropDeadColumns.)

        // ════════════════════════════════════════════════════════════════
        //  EMOTION SAFETY — PROFILE-LEVEL SUMMARY  (Fix 3)
        //
        //  DailyLogEntry.EmotionSafetyAlertFlag/Text (see DailyLogEntry.cs)
        //  are the AI team's PER-ENTRY outputs, written directly via SQL
        //  for that day's row only. The two fields below are a separate,
        //  ACTIVE-STATE summary kept on ChildProfile so the parent
        //  dashboard can answer "does this child have an active emotion
        //  safety concern right now?" with one row read instead of
        //  scanning DailyLogEntries every time.
        //
        //  Written by NlpPillarSyncService each tick (NOT by the AI team
        //  directly — the AI team never touches ChildProfile for this):
        //  whenever the AI sets EmotionSafetyAlertFlag = true on a child's
        //  latest saved entry, these two fields are promoted up; whenever
        //  the latest entry's flag is false/absent, HasActiveEmotionSafetyAlert
        //  is cleared back to false (LastEmotionSafetyAlertAt is left as a
        //  historical record of the most recent alert, not cleared).
        // ════════════════════════════════════════════════════════════════

        /// <summary>True when the child's most recent saved journal entry has an active Emotion Safety alert (EmotionSafetyAlertFlag = true on that entry).</summary>
        public bool HasActiveEmotionSafetyAlert { get; set; } = false;

        /// <summary>UTC timestamp of the most recent entry where the Emotion Safety Engine set EmotionSafetyAlertFlag = true. Null if no alert has ever fired for this child.</summary>
        public DateTime? LastEmotionSafetyAlertAt { get; set; }

        // ── Legacy AI field aliases (kept for backward compat) ─────────
        // NOTE: All Weekly AI fields are aggregated from DailyLogEntry AI fields
        // which are written exclusively by the external AI team.



        // ── AI Advice Fields (served directly from ChildProfile cache) ──
        /// <summary>Latest child-facing tip (most recent smart-tip result).</summary>
        public string? Tip { get; set; }

        /// <summary>Latest parent-facing alert message (most recent alert result).</summary>
        public string? Alert { get; set; }

        /// <summary>Latest child-facing motivational message.</summary>
        public string? Motivation { get; set; }

        // ════════════════════════════════════════════════════════════════
        //  FIVE CORE PERSONALITY TRAITS
        //  Synthesised weekly from the 8 pillars + AI signals.
        //
        //  Trait formulas (Spec Rule B — see TraitSynthesisEngine.cs):
        //  All inputs are Final Hybrid Pillars (70% Structured + 30% NLP).
        //  ──────────────────────────────────────────────────────────────
        //  Kindness   ← FinalMental (91%) + FinalSocial (9%) + sentiment boost (×0.09 when ≥50)  [cap 90]
        //  Vitality   ← FinalPhysicalHealth (57%) + FinalPhysicalActivity (43%)                  [cap 100]
        //  Curiosity  ← FinalAcademic (60%) + FinalCreativity (40%)                              [cap 100]
        //  Resilience ← FinalMental (52%) + sentiment (18%) + FinalSocial (10%) + FinalChores (10%)
        //               + FinalNutrition (5%) + FinalSport (5%)                                   [cap 65]
        //  Spiritual  ← FinalSpiritual (80%) + FinalMental (20%)                                 [cap 100]
        //  All five traits also receive anomalyFlag penalties (see TraitSynthesisEngine).
        // ════════════════════════════════════════════════════════════════

        /// <summary>Kindness trait score (0–100). Primary: Social Interaction; Secondary: Family Chores.</summary>
        public double KindnessTrait { get; set; }

        /// <summary>Vitality trait score (0–100). Primary: Sport & Movement; Secondary: Nutrition & Food.</summary>
        public double VitalityTrait { get; set; }

        /// <summary>Curiosity trait score (0–100). Primary: Homework & Study; Secondary: Hobbies.</summary>
        public double CuriosityTrait { get; set; }

        /// <summary>Resilience trait score (0–100). Primary: Mental Health / Mood; AI-modulated.</summary>
        public double ResilienceTrait { get; set; }

        /// <summary>Spiritual trait score (0–100). Primary: Connection with Allah (80%).</summary>
        public double SpiritualTrait { get; set; }

        public DateTime? LastTraitSynthesisAt { get; set; }

        // ════════════════════════════════════════════════════════════════
        //  AI RECOMMENDATION OUTPUTS  (Phase 12 — written exclusively by
        //  the external AI team; the backend stores, validates, and
        //  exposes these via API — it never recomputes them)
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Weekly Badge Recommender output — the single best-pick badge the
        /// AI team recommends for this child right now. Distinct from the
        /// per-pillar <see cref="Models.AiBadgeSuggestion"/> list (which
        /// already existed pre-Phase 12): this is the AI's ONE canonical
        /// "top pick" cached directly on the profile for fast reads.
        /// Written via POST /api/ai/badge-recommendation. Null until the
        /// AI team's weekly job has run at least once for this child.
        /// </summary>
        public int? RecommendedBadgeId { get; set; }

        [ForeignKey("RecommendedBadgeId")]
        public virtual Badge? RecommendedBadge { get; set; }

        /// <summary>UTC timestamp the AI team last set RecommendedBadgeId. Used by the weekly orchestration job to detect staleness — never written by the backend's own recompute logic.</summary>
        public DateTime? RecommendedBadgeSetAt { get; set; }

        /// <summary>
        /// Daily Leafy Recommendation output — the AI-selected catalog
        /// <see cref="Models.LeafyTip"/> that best matches this child's
        /// current state (lowest pillar, anomaly signal, sentiment trend).
        /// Distinct from the legacy <see cref="Tip"/>/<see cref="Alert"/>/
        /// <see cref="Motivation"/> free-text caches and from
        /// TipGenerationService's backend-computed "smart tip" — this is
        /// the AI's pointer into the same LeafyTip table that the legacy
        /// random/backend-computed flows already read from. Written via
        /// POST /api/ai/leafy-recommendation. Null until the AI team's
        /// daily job has run at least once for this child.
        /// </summary>
        public int? RecommendedLeafyTipId { get; set; }

        [ForeignKey("RecommendedLeafyTipId")]
        public virtual LeafyTip? RecommendedLeafyTip { get; set; }

        /// <summary>UTC timestamp the AI team last set RecommendedLeafyTipId. Used by the daily orchestration job to detect staleness — never written by the backend's own recompute logic.</summary>
        public DateTime? RecommendedLeafyTipSetAt { get; set; }

        // ════════════════════════════════════════════════════════════════
        //  LEAFY RAG — FREE-TEXT OUTPUTS  (Phase 12 — AI Team Spec)
        //
        //  In addition to RecommendedLeafyTipId (a pointer into the
        //  admin-curated LeafyTip catalog, used by the legacy/backend
        //  "smart tip" fallback flow), the AI team's daily Leafy RAG job
        //  can ALSO write free-form generated text directly here when its
        //  retrieval step produces a custom-composed tip/message/alert
        //  rather than selecting an existing catalog row verbatim.
        //
        //  Written via POST /api/ai/leafy-recommendation (all three are
        //  optional — omit any of them and the previously stored value is
        //  left untouched, same omit-to-preserve pattern as AiLogResultDto's
        //  Phase 12 NLP pillar fields). The backend never generates or
        //  rewrites these — read-only passthrough storage.
        //
        //  Child/parent-facing reads should prefer these free-text fields
        //  when present, and fall back to the resolved
        //  RecommendedLeafyTip.Tip/Alert/Motivation (via the pointer) when
        //  they are null — see ChildController.GetLeafyRecommendation.
        // ════════════════════════════════════════════════════════════════

        /// <summary>AI-generated free-text child-facing tip for today (Leafy RAG output). Null = AI used the catalog pointer instead, or hasn't run yet.</summary>
        [MaxLength(500)]
        public string? LeafyTipText { get; set; }

        /// <summary>AI-generated free-text child-facing message for today (Leafy RAG output). Null = AI used the catalog pointer instead, or hasn't run yet.</summary>
        [MaxLength(500)]
        public string? LeafyMessageText { get; set; }

        /// <summary>
        /// AI-generated free-text parent-facing alert for today (Leafy RAG output).
        /// Null = no alert / AI used the catalog pointer instead.
        ///
        /// ⚠ AI TEAM NOTE — LeafyAlertText vs EmotionSafetyAlertText:
        /// This field (LeafyAlertText) is a Leafy RAG output written to ChildProfile
        /// via POST /api/ai/leafy-recommendation. It is a daily wellness alert shown
        /// to the PARENT from Leafy's perspective and stored on the child's profile.
        ///
        /// It is SEPARATE from EmotionSafetyAlertFlag / EmotionSafetyAlertText on
        /// DailyLogEntry (written via POST /api/ai/log-result). Those fields are
        /// written by the Emotion Safety Engine per-log-entry and trigger the
        /// backend's parent notification flow (AlertGenerationService).
        ///
        /// Do NOT confuse them: LeafyAlertText is a Leafy narrative on ChildProfile;
        /// EmotionSafetyAlertText is a safety flag per entry on DailyLogEntry.
        /// </summary>
        [MaxLength(500)]
        public string? LeafyAlertText { get; set; }

        /// <summary>UTC timestamp the AI team last wrote any of the three LeafyXxxText fields above.</summary>
        public DateTime? LeafyTextSetAt { get; set; }

        /// <summary>
        /// Denormalized pointer to this child's CURRENT personality cluster
        /// (the latest <see cref="Models.AdminChildrenPersonalityCluster"/>
        /// row with IsCurrent = true), kept here so profile/dashboard reads
        /// don't need an extra join. The full assignment history — including
        /// past clusters and the AI's similarity score for each — lives in
        /// AdminChildrenPersonalityClusters. Written by the periodic
        /// Clustering job via POST /api/ai/personality-clusters.
        /// </summary>
        public int? LastPersonalityClusterId { get; set; }

        [ForeignKey("LastPersonalityClusterId")]
        public virtual PersonalityCluster? LastPersonalityCluster { get; set; }

        /// <summary>UTC timestamp of the most recent personality-cluster assignment for this child.</summary>
        public DateTime? PersonalityClusterAssignedAt { get; set; }

        // ════════════════════════════════════════════════════════════════
        //  NLP PROFILE AVERAGES  (Phase 13 — 30-day rolling avg of NlpXxx_Pct
        //  from DailyLogEntry, mirroring how the Structured pillars are stored)
        //  Written by PillarPipelineService alongside the Structured pillars.
        // ════════════════════════════════════════════════════════════════

        public double NlpSpiritualConnection_Pct { get; set; }
        public double NlpSocialInteraction_Pct { get; set; }
        public double NlpAcademicProgress_Pct { get; set; }
        public double NlpHomeResponsibility_Pct { get; set; }
        public double NlpPhysicalHealth_Pct { get; set; }
        public double NlpPhysicalActivity_Pct { get; set; }
        public double NlpCreativityEngagement_Pct { get; set; }
        public double NlpMentalWellbeing_Pct { get; set; }

        // ════════════════════════════════════════════════════════════════
        //  FINAL HYBRID PILLARS  (Phase 13 — Hybrid Formula)
        //
        //  FinalPillar = 0.70 × StructuredPillar + 0.30 × NlpPillar
        //
        //  These are the canonical values consumed by:
        //    • Badge recommendation
        //    • Level-up MeanScore gate
        //    • Personality clustering
        //    • Leafy RAG inputs
        //    • All frontend pillar displays
        //
        //  Recomputed by PillarPipelineService every time either source changes.
        // ════════════════════════════════════════════════════════════════

        [Column("FinalSpiritualConnection_Pct")]
        public double FinalSpiritualConnection_Pct { get; set; }

        [Column("FinalSocialInteraction_Pct")]
        public double FinalSocialInteraction_Pct { get; set; }

        [Column("FinalAcademicProgress_Pct")]
        public double FinalAcademicProgress_Pct { get; set; }

        [Column("FinalHomeResponsibility_Pct")]
        public double FinalHomeResponsibility_Pct { get; set; }

        [Column("FinalPhysicalHealth_Pct")]
        public double FinalPhysicalHealth_Pct { get; set; }

        [Column("FinalPhysicalActivity_Pct")]
        public double FinalPhysicalActivity_Pct { get; set; }

        [Column("FinalCreativityEngagement_Pct")]
        public double FinalCreativityEngagement_Pct { get; set; }

        /// <summary>
        /// NOTE (audit fix #4): unlike the other 7 Final pillars, this one's
        /// AI share is NOT just the nominal 30%. Its Structured input
        /// (MentalWellbeing_Pct) is itself 60% SentimentScore (AI), so the
        /// effective AI influence here is ≈ 0.70×0.60 + 0.30 = 72%, not 30%.
        /// Intentional per the hybrid design — flagged here for transparency.
        /// </summary>
        [Column("FinalMentalWellbeing_Pct")]
        public double FinalMentalWellbeing_Pct { get; set; }

        /// <summary>
        /// FinalMeanScore — arithmetic mean of the 8 Final Hybrid Pillars.
        /// Replaces MeanScore as the input to level-up gate (MinTrustScore check)
        /// and anomaly detection engine. MeanScore is retained for backward compat.
        /// </summary>
        /// 

        // ════════════════════════════════════════════════════════════════
        //  AI SYNC FLAGS  (Phase 15 — Database Polling for AI Jobs)
        //  Used by BadgeRecommendationSyncService, LeafyRagSyncService
        //  to detect new AI writes without API calls.
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Set to true by BadgeRecommendationSyncService after processing
        /// a RecommendedBadgeId written by the AI team. AI sets this to
        /// false when writing a new badge recommendation.
        /// </summary>
        public bool RecommendedBadgeSynced { get; set; } = false;

        /// <summary>
        /// Set to true by LeafyRagSyncService after processing
        /// Leafy text fields written by the AI team. AI sets this to
        /// false when writing new leafy content.
        /// </summary>
        public bool LeafySynced { get; set; } = false;
        public double FinalMeanScore { get; set; }

        // ════════════════════════════════════════════════════════════════
        //  TASK COUNTS  (Phase 15 — AI Team Spec: Anomaly Detection Trust Engine)
        //
        //  AI team spec: "Tasks in child profile table" — the Anomaly Detection
        //  Trust Engine reads TotalTasksCount and CompletedTasksCount from this
        //  table alongside LogDuration (DailyLogEntry) and MeanScore above.
        //
        //  Counts include ALL task types:
        //    • Fixed recurring tasks (IsFixedTask = true): صلوات, قرآن, أذكار, دعاء
        //    • Plan-template reference tasks (Description = "__plan_template__"):
        //      مواد دراسية / رياضة / مهام بيتية — مُعرَّفة من الأب، مصدر الجورنال
        //    • Parent-assigned completable tasks (IsParentAssigned = true, no template marker)
        //
        //  CompletedTasksCount counts today's completions:
        //    • Fixed tasks: IsCompletedToday = true  (resets nightly by DailyLogClosureService)
        //    • Parent-assigned tasks: IsCompleted = true  (permanent, never resets)
        //    • Plan-template tasks: NOT counted toward completed (they are reference config,
        //      not completable items — they don't have a completion flag)
        //
        //  Recomputed by PillarPipelineService.RecalculateTaskCountsAsync() called:
        //    (a) After every journal save (DailyLogsController)
        //    (b) After every task completion (TasksController.CompleteTask)
        //    (c) Nightly after DailyLogClosureService resets IsCompletedToday
        //  The backend is the sole writer. AI team reads only.
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Total number of tasks for this child across all types
        /// (fixed recurring + plan-template reference + parent-assigned completable).
        /// Recomputed by PillarPipelineService.RecalculateTaskCountsAsync.
        /// AI team spec column name: TotalTasksCount.
        /// </summary>
        public int TotalTasksCount { get; set; } = 0;

        /// <summary>
        /// Number of tasks completed today.
        /// Fixed tasks: IsCompletedToday = true (resets nightly).
        /// Parent-assigned tasks: IsCompleted = true (permanent).
        /// Plan-template tasks excluded (not completable items).
        /// Recomputed by PillarPipelineService.RecalculateTaskCountsAsync.
        /// AI team spec column name: CompletedTasksCount.
        /// </summary>
        public int CompletedTasksCount { get; set; } = 0;

        // ── Computed Helpers (NotMapped) ──────────────────────────────
        [NotMapped]
        public int ExpToNextLevel => 100 + (Level - 1) * 50;

        // REMOVED (audit fix #1): AspectThreshold + AllPillarsReady used to
        // live here as a per-level "all 8 pillars above threshold" gate for
        // the OLD LevelRequirementsEngine. That engine was replaced in
        // Phase 10 by NewLevelEngineService, whose EvaluateLevelUpAsync()
        // gates level-up on badge_count + min_trust_score ONLY (matching the
        // AI team's check_for_level_up spec literally — see
        // Services/NewLevelEngineService.cs). AllPillarsReady was never
        // wired into that new gate and had no other reader (not in any DTO,
        // not in the frontend) — it was dead code left over from the old
        // engine, and requiring ALL 8 pillars simultaneously above threshold
        // was the exact bug the Phase 10 rewrite was fixing (see the
        // rationale comment in TasksController.CompleteTask). Deleted rather
        // than wired into the gate, to avoid reintroducing that bug.
    }


}
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GrawiaaApp.API.Models
{
    public class DailyLogEntry
    {
        // ── Identity ─────────────────────────────────────────────────
        public int Id { get; set; }

        [Required]
        public int ChildId { get; set; }

        public DateTime LogDate { get; set; } = DateTime.UtcNow;

        // ── Core Journal Fields ───────────────────────────────────────

        /// <summary>Total active minutes the child spent filling out this journal entry.</summary>
        /// <remarks>
        /// AUDIT FIX: this is now the real mapped database column. The AI
        /// team queries the database directly via SQL (not through any
        /// API), and their spec names this column LogDuration — not
        /// LogDurationInMinutes. Renaming the actual column avoids relying
        /// on a C#-level alias the AI's raw SQL would never see.
        /// </remarks>
        [Column("LogDuration")]
        public int LogDuration { get; set; }

        /// <summary>Backward-compatibility alias for any existing C# code
        /// still referencing the old property name.</summary>
        [NotMapped]
        public int LogDurationInMinutes { get => LogDuration; set => LogDuration = value; }

        // ════════════════════════════════════════════════════════════════
        //  THE 8 DAILY PILLAR PERCENTAGES  (Tier 1: Daily Inputs)
        // ════════════════════════════════════════════════════════════════

        /// <summary>Pillar 3 — Homework & Study: % of active time spent studying (0–100).</summary>
        [Range(0, 100)]
        public double StudyingPct { get; set; }

        /// <summary>Pillar 8 — Mental Health: Mood emoji selected by the child (e.g. "😊", "😢").</summary>
        public string? MoodEmoji { get; set; }

        /// <summary>Pillar 1 — Connection with Allah: % of active time on spiritual/religious activities (0–100).</summary>
        [Range(0, 100)]
        public double SpiritualPct { get; set; }

        /// <summary>Pillar 6 — Sport & Movement: % of active time on physical movement/sport (0–100).</summary>
        [Range(0, 100)]
        public double MovingPct { get; set; }

        /// <summary>Pillar 5 — Nutrition & Food: % of active time on food/nutrition care (0–100).</summary>
        [Range(0, 100)]
        public double FoodPct { get; set; }

        /// <summary>
        /// Pillar 4 — Family Chores: % of active time on home chores / family responsibilities (0–100).
        /// Spec name: ChorsPct (intentional single-r per spec Rule C: Env_Pct domain).
        /// </summary>
        [Range(0, 100)]
        [Column("ChorsPct")]
        public double ChorsPct { get; set; }

        /// <summary>Backward-compatibility alias — maps to ChorsPct.</summary>
        [NotMapped]
        public double ChoresPct { get => ChorsPct; set => ChorsPct = value; }

        /// <summary>Pillar 7 — Hobbies/Creativity: % of active time on hobbies/creative activities (0–100).</summary>
        [Range(0, 100)]
        public double HobbiesPct { get; set; }

        /// <summary>Pillar 2 — Social Interaction: % of active time on social interactions (0–100).</summary>
        [Range(0, 100)]
        public double SocialPct { get; set; }

        // ════════════════════════════════════════════════════════════════
        //  DAILY COMPUTED PILLAR PERCENTAGES  (Phase 11 — Single Pipeline)
        //
        //  Computed EXACTLY ONCE per save by
        //  PillarPipelineService.ApplyDailyPillars(this), which runs
        //  PillarCalculationEngine.FromSingleEntry(this) and stores the
        //  8 results below. These represent THIS DAY'S performance only
        //  (raw inputs above → engine formulas → these outputs).
        //
        //  ChildProfile aggregates these over a rolling 30-day window —
        //  see PillarPipelineService.RecalculateProfileAggregatesAsync.
        //  No other code may write to these 8 fields.
        // ════════════════════════════════════════════════════════════════

        /// <summary>Daily Pillar 1 — Religious/Spiritual Connection (0–100).</summary>
        public double DailySpiritualConnection_Pct { get; set; }

        /// <summary>Daily Pillar 2 — Social Interaction (0–100).</summary>
        public double DailySocialInteraction_Pct { get; set; }

        /// <summary>Daily Pillar 3 — Academic Progress (0–100).</summary>
        public double DailyAcademicProgress_Pct { get; set; }

        /// <summary>Daily Pillar 4 — Environment / Home Responsibility (0–100).</summary>
        public double DailyHomeResponsibility_Pct { get; set; }

        /// <summary>Daily Pillar 5 — Physical Health / Nutrition (0–100).</summary>
        public double DailyPhysicalHealth_Pct { get; set; }

        /// <summary>Daily Pillar 6 — Sport / Physical Activity (0–100).</summary>
        public double DailyPhysicalActivity_Pct { get; set; }

        /// <summary>Daily Pillar 7 — Creativity Engagement (0–100).</summary>
        public double DailyCreativityEngagement_Pct { get; set; }

        /// <summary>Daily Pillar 8 — Mental Wellbeing (0–100).</summary>
        public double DailyMentalWellbeing_Pct { get; set; }

        /// <summary>UTC timestamp of the last time the 8 daily pillars above were computed for this row.</summary>
        public DateTime? PillarsCalculatedAt { get; set; }

        // ── Notes Fields ──────────────────────────────────────────────

        /// <summary>General free-text note for the entry (NLP source text).</summary>
        [Column("GeneralNote")]
        public string? GeneralNote { get; set; }

        /// <summary>Backward compatibility alias for GeneralNote.</summary>
        [NotMapped]
        public string? Note { get => GeneralNote; set => GeneralNote = value; }

        /// <summary>Child's own description of how they feel today (NLP source text, key input for sentiment analysis).</summary>
        public string? FeelingNote { get; set; }

        /// <summary>Private message from the child to their parent (shown on parent dashboard).</summary>
        public string? MessageToParent { get; set; }

        // ── Detailed Activity Fields (journal item tags) ──────────────

        // Spiritual
        public string? SpiritualActivities { get; set; }

        // Academic
        public string? CompletedSubjects { get; set; }
        public string? LearningNote { get; set; }

        // Physical Activity
        public string? PhysicalActivity { get; set; }
        public string? ActivityNote { get; set; }

        // Nutrition / Food
        public string? NutritionItems { get; set; }
        public int WaterGlasses { get; set; }
        public string? FoodNote { get; set; }

        // Chores / Home Responsibility
        public bool HelpedFamily { get; set; }
        public string? CompletedChores { get; set; }

        // Hobbies / Creativity
        public string? FunActivities { get; set; }

        // Social
        public string? SocialTimeWith { get; set; }
        public string? SocialInteraction { get; set; }

        /// <summary>
        /// How the child handled challenges today (comma-separated, e.g. "Stayed calm, Asked for help").
        /// Feeds MentalWellbeing_Pct and contributes to Resilience Trait.
        /// </summary>
        public string? ChallengeHandling { get; set; }

        /// <summary>
        /// Quality score of today's social interactions (0–100).
        /// Either provided directly or auto-calculated from SocialTimeWith + ChallengeHandling.
        /// </summary>
        [Range(0, 100)]
        public double SocialQualityScore { get; set; }

        // Mood / Mental
        public string? MoodLevel { get; set; }
        public string? MoodReason { get; set; }
        public string? ReflectionText { get; set; }

        // Sleep
        public int SleepHours { get; set; }
        public string? SleepNote { get; set; }

        // ── Past illness tracking (presentation: main feature) ─────────────
        /// <summary>Whether the child reported feeling sick today.</summary>
        public bool WasIllToday { get; set; }

        /// <summary>Illness category (e.g. cold, fever, allergy).</summary>
        [MaxLength(100)]
        public string? IllnessType { get; set; }

        /// <summary>Free-text note about today's illness.</summary>
        [MaxLength(500)]
        public string? IllnessNote { get; set; }

        /// <summary>
        /// AI Leafy RAG avoidance suggestion for this illness context.
        /// Written exclusively by the external AI team via direct SQL or API.
        /// </summary>
        [MaxLength(500)]
        public string? IllnessAvoidanceTip { get; set; }

        // Legacy raw fields removed (Phase 14 cleanup — no writer existed since journal v2).
        // WaterCups, FoodFruitsVeggies, ReligiousPractice, ChoresStatus, HomeworkStatus,
        // Sports, HobbiesToday: all always NULL in DB. Use modern fields:
        //   WaterGlasses, NutritionItems, SpiritualActivities, CompletedChores,
        //   HelpedFamily, CompletedSubjects, PhysicalActivity, FunActivities.

        // ════════════════════════════════════════════════════════════════
        //  AI FIELDS  (Populated exclusively by the external AI team)
        //  The backend does NOT compute or write these fields.
        //  The AI team writes directly to these columns in the database.
        // ════════════════════════════════════════════════════════════════

        /// <summary>Emotion detected by the AI team's NLP pipeline from the child's text entries.</summary>
        public string? DetectedEmotion { get; set; }

        /// <summary>
        /// Sentiment score produced by the AI team's NLP pipeline.
        /// Range: –1.0 (very negative) → +1.0 (very positive).
        /// </summary>
        public double? SentimentScore { get; set; }

        /// <summary>
        /// Consistency / honesty anomaly score from the AI team's analysis.
        /// Measures contradiction between mood signals and text signals.
        /// Range: 0 (fully consistent) – 100 (highly anomalous).
        /// </summary>
        [Column("AnomalyScore")]
        public int AnomalyScore { get; set; }

        /// <summary>
        /// NLP-level anomaly score from the AI team's analysis.
        /// Measures unusual patterns in the text itself.
        /// Range: 0 (normal) – 100 (anomalous).
        /// </summary>
        public int NlpAnomalyScore { get; set; }

        /// <summary>
        /// True when the AI team determined an anomaly exists in this entry.
        /// </summary>
        public bool AnomalyFlag { get; set; }

        /// <summary>Whether the AI determined the entry appears honest/consistent. Derived from AnomalyFlag.</summary>
        public bool IsHonest { get; set; } = true;

        // ════════════════════════════════════════════════════════════════
        //  EMOTION SAFETY ENGINE OUTPUT  (منفصل تماماً عن Trust Engine)
        //
        //  AnomalyFlag + AnomalyScore مِلك Trust Engine حصريًا —
        //  الـ Emotion Safety Engine عنده أعمدته الخاصة أدناه.
        //  كل engine عنده ownership واضح لعموده ومفيش تضارب.
        //
        //  CONFIRMED WITH AI TEAM (2026-06-21): the Emotion Safety job
        //  writes DIRECTLY via raw SQL to a column literally named "Alert"
        //  on daily_log_entries (plus SentimentScore, confirmed separately,
        //  see below). That column did NOT exist before this fix — any
        //  attempt by the AI's SQL to write `Alert` against the previous
        //  schema would have failed with an invalid-column-name error and
        //  the write would never have reached the database, silently.
        //
        //  FIX: added the real `Alert` column below (string, matches the
        //  AI team's exact spec name). EmotionSafetyAlertFlag/Text are KEPT
        //  — they are read by ParentController's dashboard filtering and by
        //  NlpPillarSyncService's active-alert promotion/staleness logic,
        //  both of which are real, working features. Rather than rip those
        //  out, PillarPipelineService.ApplyDailyPillars now derives them
        //  FROM `Alert` whenever it changes (Alert non-empty → Flag=true,
        //  Text=Alert; Alert empty/null → Flag=false), so the AI's direct-SQL
        //  write to `Alert` is the single source of truth and the
        //  derived/legacy fields stay in sync automatically. No second
        //  competing source of truth, no manual reconciliation needed.
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// AI team spec field — Emotion Safety Engine output. Free-text alert
        /// describing a detected emotional concern (distress, sadness, anger
        /// triggers, etc.), or null/empty when no concern was detected for
        /// this entry. Written EXCLUSIVELY by the external AI team's Emotion
        /// Safety pipeline via direct SQL (confirmed 2026-06-21 — NOT via
        /// POST /api/ai/log-result). The backend never computes or
        /// overwrites this column; it only reads it to derive
        /// EmotionSafetyAlertFlag/EmotionSafetyAlertText below for the
        /// existing parent-dashboard and alert-promotion logic.
        /// </summary>
        public string? Alert { get; set; }

        /// <summary>
        /// True when the Emotion Safety Engine detected an emotional concern
        /// in this entry (e.g. distress, sadness, anger triggers).
        /// DISTINCT from AnomalyFlag (Trust Engine) — do NOT conflate the two.
        /// DERIVED from <see cref="Alert"/> (non-empty → true) by
        /// PillarPipelineService.ApplyDailyPillars — not written directly by
        /// the AI team. Kept so existing consumers (ParentController,
        /// NlpPillarSyncService) continue to work unchanged.
        /// </summary>
        public bool EmotionSafetyAlertFlag { get; set; } = false;

        /// <summary>
        /// Free-text alert message produced by the Emotion Safety Engine describing
        /// the detected emotional concern (shown to parents on dashboard).
        /// Null when EmotionSafetyAlertFlag = false.
        /// DISTINCT from AnomalyFlag/AnomalyScore (Trust Engine).
        /// DERIVED from <see cref="Alert"/> by PillarPipelineService.ApplyDailyPillars
        /// — mirrors its value whenever Alert is non-empty. Kept so existing
        /// consumers (ParentController, NlpPillarSyncService) continue to work
        /// unchanged.
        ///
        /// ⚠ AI TEAM NOTE — EmotionSafetyAlertText vs LeafyAlertText:
        /// This field lives on DailyLogEntry (per-log-entry scope).
        /// ChildProfile.LeafyAlertText is a DIFFERENT field — it is Leafy RAG
        /// output (daily narrative alert for parents) stored on the child profile,
        /// NOT an Emotion Safety signal. The AI team's `Alert` write here is for
        /// safety alerts; ChildProfile.Alert (a separate column on a separate
        /// table) is the Leafy RAG output — do not confuse the two tables.
        /// </summary>
        public string? EmotionSafetyAlertText { get; set; }

        /// <summary>
        /// Variance/dispersion metric from the AI team's anomaly-detection pipeline —
        /// measures how much THIS entry's signals (mood, sentiment, structured pillar
        /// inputs) disagree with one another or with the child's recent baseline.
        /// A higher variance generally co-occurs with AnomalyFlag = true, but the two
        /// are independent AI outputs (variance is a continuous magnitude, AnomalyFlag
        /// is the AI's binary decision). Range: 0 (fully consistent) – 100 (highly
        /// dispersed/inconsistent). Written exclusively by the external AI team via
        /// POST /api/ai/log-result — the backend never computes or overwrites it.
        /// </summary>
        public double ScoreVariance { get; set; }

        // ════════════════════════════════════════════════════════════════
        //  8 NLP PILLARS  (AI Team Output — Phase 12)
        //
        //  These are DISTINCT from the 8 "DailyXxx_Pct" engine pillars above.
        //  DailyXxx_Pct are deterministic — computed by
        //  PillarCalculationEngine.FromSingleEntry from the STRUCTURED inputs
        //  (StudyingPct, SpiritualPct, MovingPct, ...) and owned exclusively
        //  by the backend pipeline (see PillarPipelineService).
        //
        //  NlpXxx_Pct below are derived PURELY from NLP analysis of this
        //  entry's free text (FeelingNote / GeneralNote / ReflectionText /
        //  MoodReason) by the external AI team's NLP pipeline. They give a
        //  second, text-grounded read on the same 8 pillars that the backend
        //  can compare against its own structured-input calculation (e.g. to
        //  help explain a high ScoreVariance / AnomalyFlag), WITHOUT the
        //  backend ever recomputing them. Written exclusively via
        //  POST /api/ai/log-result — same endpoint/DTO as the other AI
        //  fields, just additional optional properties.
        // ════════════════════════════════════════════════════════════════

        /// <summary>NLP Pillar 1 — Spiritual Connection, derived from text sentiment (0–100).</summary>
        public double NlpSpiritualConnection_Pct { get; set; }

        /// <summary>NLP Pillar 2 — Social Interaction, derived from text sentiment (0–100).</summary>
        public double NlpSocialInteraction_Pct { get; set; }

        /// <summary>NLP Pillar 3 — Academic Progress, derived from text sentiment (0–100).</summary>
        public double NlpAcademicProgress_Pct { get; set; }

        /// <summary>NLP Pillar 4 — Home Responsibility, derived from text sentiment (0–100).</summary>
        public double NlpHomeResponsibility_Pct { get; set; }

        /// <summary>NLP Pillar 5 — Physical Health / Nutrition, derived from text sentiment (0–100).</summary>
        public double NlpPhysicalHealth_Pct { get; set; }

        /// <summary>NLP Pillar 6 — Physical Activity, derived from text sentiment (0–100).</summary>
        public double NlpPhysicalActivity_Pct { get; set; }

        /// <summary>NLP Pillar 7 — Creativity Engagement, derived from text sentiment (0–100).</summary>
        public double NlpCreativityEngagement_Pct { get; set; }

        /// <summary>NLP Pillar 8 — Mental Wellbeing, derived from text sentiment (0–100).</summary>
        public double NlpMentalWellbeing_Pct { get; set; }

        /// <summary>UTC timestamp of the last time the AI team wrote the 8 NLP pillars above for this row.</summary>
        public DateTime? NlpPillarsCalculatedAt { get; set; }

        // ── Gamification Deltas ───────────────────────────────────────
        public double DeltaAcademic { get; set; }
        public double DeltaEmotional { get; set; }
        public double DeltaPhysical { get; set; }
        public double DeltaReligious { get; set; }
        public double DeltaSocial { get; set; }
        public double DeltaResilience { get; set; }

        /// <summary>Experience points awarded to the child for this log entry.</summary>
        public int ExpAwarded { get; set; }

        /// <summary>Whether saving this entry caused a character level-up.</summary>
        public bool TriggeredLevelUp { get; set; } = false;

        /// <summary>The child's level immediately after this entry was processed.</summary>
        public int LevelAfter { get; set; }

        // ── AI Team Spec Fields ───────────────────────────────────────

        /// <summary>
        /// AI team spec: "journal_id" — unique identifier for this journal entry.
        /// Same as Id. NotMapped alias — the actual DB column is named "Id".
        /// For direct SQL access, use the `Id` column on DailyLogEntries.
        /// </summary>
        [NotMapped]
        public int JournalId { get => Id; }

        /// <summary>
        /// AI team spec: "created_at" — when this journal entry was first created.
        /// Maps to LogDate (stored in Egypt local time). NotMapped alias.
        /// For direct SQL access, use the `LogDate` column on DailyLogEntries.
        /// A real `created_at` DB column requires a migration (pending unlock).
        /// </summary>
        [NotMapped]
        public DateTime CreatedAt { get => LogDate; }

        /// <summary>
        /// AI team spec: "hashed_child_id" on journal_entries.
        /// Privacy-safe identifier derived from ChildId via ChildIdentityService.
        /// Populated on each journal save. NOT a FK — extra column for AI team SQL access.
        /// </summary>
        [MaxLength(64)]
        public string HashedChildId { get; set; } = string.Empty;

        /// <summary>
        /// AI team spec: "entry_text" — the main text content submitted by the child.
        /// Maps to ReflectionText (the free-form text field in the journal).
        /// This is a computed alias — no additional DB column.
        /// </summary>
        [NotMapped]
        public string EntryText { get => ReflectionText ?? string.Empty; set => ReflectionText = value; }

        /// <summary>
        /// AI team spec: "submission_date" — when this journal entry was submitted.
        /// Same as LogDate. NotMapped alias.
        /// </summary>
        [NotMapped]
        public DateTime SubmissionDate { get => LogDate; set => LogDate = value; }

        // ── Control Flags ─────────────────────────────────────────────

        /// <summary>Whether the journal has been saved/submitted for this entry.</summary>
        public bool IsJournalSaved { get; set; } = false;

        /// <summary>Whether the daily log has been finalized (closed for edits).</summary>
        public bool IsFinalized { get; set; } = false;

        // ── Navigation Properties ─────────────────────────────────────

        [ForeignKey("ChildId")]
        public virtual ApplicationUser Child { get; set; } = null!;

        public virtual ICollection<ParentFeedback> Feedbacks { get; set; } = new List<ParentFeedback>();
    }
}
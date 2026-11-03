// ============================================================
//  Phase5Dtos.cs — Leafy AI System (Phase 5)
//
//  New DTOs and updated existing DTOs for:
//    • LeafyTip CRUD (Admin) — now includes Tip / Alert / Motivation / Purpose
//    • Weekly profile snapshot (child + parent dashboards)
//    • Multi-entry daily log (incremental daily save)
//    • Feeling validation (14 canonical feelings)
// ============================================================

using System.ComponentModel.DataAnnotations;

namespace GrawiaaApp.API.DTOs
{
    // ════════════════════════════════════════════════════════════════════
    //  FEELING VALIDATION  (Spec Rule A — 14 canonical feelings)
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The 14 canonical child feelings defined in the system spec (Rule A).
    /// Used for validation of MoodLevel / FeelingNote fields.
    /// Note: "Worried / Anxious" is the display label; both "Worried / Anxious"
    /// and "Worried" are accepted so the frontend label matches the backend validator.
    /// </summary>
    public static class CanonicalFeelings
    {
        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            "Good", "Happy", "Excited", "Proud",
            "Sad", "Lonely", "Tired", "Overwhelmed",
            // Spec Rule A: label is "Worried / Anxious" — accept both forms
            "Worried / Anxious", "Worried", "Anxious",
            "Scared", "Angry", "Jealous",
            "Confused", "Hurt"
        };

        public static bool IsValid(string? feeling)
            => !string.IsNullOrWhiteSpace(feeling) && All.Contains(feeling);
    }

    // ════════════════════════════════════════════════════════════════════
    //  LEAFY TIP DTOs
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Full read DTO for LeafyTip — used by admin dashboard list and GET by id.
    /// Exposes all three content channels.
    /// </summary>
    public class LeafyTipFullDto
    {
        public int Id { get; set; }
        public string Side { get; set; } = "";
        public string Type { get; set; } = "";
        public string Purpose { get; set; } = "tip";
        public string Content { get; set; } = "";   // legacy
        public string? Tip { get; set; }
        public string? Alert { get; set; }
        public string? Motivation { get; set; }

        /// <summary>Resolved content for the current Purpose channel.</summary>
        public string ResolvedContent => Purpose switch
        {
            "alert" => Alert ?? Content,
            "motivation" => Motivation ?? Content,
            _ => Tip ?? Content,
        };
    }

    /// <summary>
    /// Slim read DTO used by child/parent dashboard quick-tip endpoints.
    /// Kept for backward compat with existing LeafyTipDto consumers.
    /// </summary>
    public class LeafyTipDto
    {
        public int Id { get; set; }
        public string Side { get; set; } = "";
        public string Type { get; set; } = "";
        public string Purpose { get; set; } = "tip";
        public string Content { get; set; } = "";
    }

    /// <summary>Create / Update DTO for Admin LeafyTip management.</summary>
    public class UpsertLeafyTipDto
    {
        [Required, MaxLength(30)]
        public string Side { get; set; } = "";

        [Required, MaxLength(20)]
        public string Type { get; set; } = "";

        /// <summary>Purpose channel: "tip" | "alert" | "motivation"</summary>
        [Required, MaxLength(20)]
        [RegularExpression("^(tip|alert|motivation)$",
            ErrorMessage = "Purpose must be 'tip', 'alert', or 'motivation'.")]
        public string Purpose { get; set; } = "tip";

        /// <summary>Legacy content (always required for backward compat).</summary>
        [Required, MaxLength(500)]
        public string Content { get; set; } = "";

        /// <summary>Child-facing practical advice (Purpose = "tip").</summary>
        [MaxLength(500)]
        public string? Tip { get; set; }

        /// <summary>Parent-facing alert text (Purpose = "alert").</summary>
        [MaxLength(500)]
        public string? Alert { get; set; }

        /// <summary>Child-facing motivational praise (Purpose = "motivation").</summary>
        [MaxLength(500)]
        public string? Motivation { get; set; }
    }

    // ════════════════════════════════════════════════════════════════════
    //  WEEKLY PROFILE SNAPSHOT DTO  (Tier 2: Synthesis layer read)
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Full weekly snapshot of a child's pillar averages, AI metrics,
    /// and 5 core traits. Returned by:
    ///   GET /api/Children/weekly-snapshot
    ///   GET /api/Parent/child-snapshot/{childId}
    /// </summary>
    public class WeeklySnapshotDto
    {
        public int ChildId { get; set; }
        public string FullName { get; set; } = "";
        public int Age { get; set; }
        public int Level { get; set; }

        // ── Processed 8 Pillar Scores (engine-weighted) ───────────────
        public double SpiritualConnection_Pct { get; set; }
        public double SocialInteraction_Pct { get; set; }
        public double AcademicProgress_Pct { get; set; }
        public double HomeResponsibility_Pct { get; set; }
        public double PhysicalHealth_Pct { get; set; }
        public double PhysicalActivity_Pct { get; set; }
        public double CreativityEngagement_Pct { get; set; }
        public double MentalWellbeing_Pct { get; set; }

        // ── 5 Core Personality Traits ─────────────────────────────────
        public double KindnessTrait { get; set; }
        public double VitalityTrait { get; set; }
        public double CuriosityTrait { get; set; }
        public double ResilienceTrait { get; set; }
        public double SpiritualTrait { get; set; }

        public DateTime? LastTraitSynthesisAt { get; set; }
        public DateTime? LastAggregatedAt { get; set; }
        public DateTime? AggregationWeekStart { get; set; }
    }

    // ════════════════════════════════════════════════════════════════════
    //  INCREMENTAL DAILY LOG DTO  (multi-entry per day)
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Response returned when a child saves an incremental journal entry
    /// (subsequent save on the same day — no EXP awarded).
    /// </summary>
    public class IncrementalSaveResultDto
    {
        public string Message { get; set; } = "";
        public bool IsFirstSave { get; set; }
        public int EntryId { get; set; }

        // Pillar snapshots (updated from the re-save)
        public double StudyingPct { get; set; }
        public double SpiritualPct { get; set; }
        public double MovingPct { get; set; }
        public double FoodPct { get; set; }
        public double ChorsPct { get; set; }
        public double HobbiesPct { get; set; }
        public double SocialPct { get; set; }
    }

    // ════════════════════════════════════════════════════════════════════
    //  TODAY JOURNAL RESPONSE DTO
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Response for GET /api/Children/today-journal.
    /// Pre-fills the frontend journal form with the latest saved values.
    /// Aggregated across all DailyLogEntry rows for today (upsert logic).
    /// </summary>
    // ════════════════════════════════════════════════════════════════════
    // TodayJournalDto  —  مصحح (ضاف عليه حقلين)
    // ════════════════════════════════════════════════════════════════════
    public class TodayJournalDto
    {
        // Activity tags
        public string? SpiritualActivities { get; set; }
        public string? SocialTimeWith { get; set; }
        public string? CompletedSubjects { get; set; }
        public string? CompletedChores { get; set; }
        public bool HelpedFamily { get; set; }
        public string? NutritionItems { get; set; }
        public string? PhysicalActivity { get; set; }
        public string? FunActivities { get; set; }

        // Counts
        public int WaterGlasses { get; set; }

        // Mood
        public string? MoodLevel { get; set; }
        public string? MoodEmoji { get; set; }
        public string? MoodReason { get; set; }

        // Notes
        public string? ReflectionText { get; set; }
        public string? FeelingNote { get; set; }
        public string? MessageToParent { get; set; }

        // Pillar sliders
        public double StudyingPct { get; set; }
        public double SpiritualPct { get; set; }
        public double MovingPct { get; set; }
        public double FoodPct { get; set; }
        public double ChorsPct { get; set; }
        public double HobbiesPct { get; set; }
        public double SocialPct { get; set; }
        public int LogDuration { get; set; }
        public int EntryCount { get; set; }
        public bool HasEntry { get; set; }
        public double MoodPct { get; set; }

        // Sleep
        public int SleepHours { get; set; }
        public string? SleepNote { get; set; }

        // ✅ Phase 7: الحجات اللي كانت ناقصة ────────────────────────────────
        public double SocialQualityScore { get; set; }
        public string? ChallengeHandling { get; set; }

        public bool WasIllToday { get; set; }
        public string? IllnessType { get; set; }
        public string? IllnessNote { get; set; }
    }
}
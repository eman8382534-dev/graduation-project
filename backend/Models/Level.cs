// ============================================================
//  Level.cs — UPDATED for Spec Compliance (Phase 10 + AI Team)
//
//  Added: RequiredBadges (int) and MinTrustScore (double)
//  These are the columns required by the AI team spec:
//      level_id, title, required_badges, min_trust_score
//
//  "title" maps to the existing Name property.
//  RequiredBadges and MinTrustScore are now first-class
//  columns on the Levels table so the AI team can read them
//  directly from SQL without needing the LevelRulesProvider.
//
//  LevelRulesProvider continues to derive values dynamically
//  for internal use, but these columns serve as the canonical
//  DB representation compatible with the AI team schema.
// ============================================================

using System.ComponentModel.DataAnnotations;

namespace GrawiaaApp.API.Models
{
    public class Level
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Level number. 1 = Sprout, 2 = Week One Hero, etc.</summary>
        public int LevelNumber { get; set; }

        /// <summary>Display name (maps to "title" in AI team spec). e.g. "The Sprout"</summary>
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// AI team spec: "title" alias. Same as Name.
        /// Computed property so the DB stores it once.
        /// </summary>
        [MaxLength(100)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// AI team spec: "required_badges" — how many badges (milestones)
        /// must be earned before a child can advance past this level.
        /// Seeded from badge challenge counts; updated on migration.
        /// </summary>
        public int RequiredBadges { get; set; } = 0;

        /// <summary>
        /// AI team spec: "min_trust_score" — minimum trust score required
        /// to advance past this level (0.0–1.0 scale for AI team compatibility,
        /// stored as double; backend compares against TrustScore/100.0).
        /// </summary>
        public double MinTrustScore { get; set; } = 0.0;

        /// <summary>
        /// Presentation spec: maximum allowed average anomaly score (0–100)
        /// for level-up. Lower anomaly = more honest journaling. Child's
        /// trailing 30-day average AnomalyScore must be at or below this.
        /// </summary>
        public int MaxAnomalyScore { get; set; } = 100;

        /// <summary>Emoji representing this level. e.g. "🍒"</summary>
        [MaxLength(10)]
        public string Emoji { get; set; } = "⭐";

        /// <summary>Inspirational quote shown on the journey card.</summary>
        [MaxLength(300)]
        public string Quote { get; set; } = string.Empty;

        /// <summary>Background color for the journey card. e.g. "#fffbe6"</summary>
        [MaxLength(20)]
        public string CardBgColor { get; set; } = "#ffffff";

        /// <summary>Title color for the journey card. e.g. "#e8a800"</summary>
        [MaxLength(20)]
        public string CardTitleColor { get; set; } = "#333333";

        /// <summary>CSS gradient string for the progress bar.</summary>
        [MaxLength(200)]
        public string BarColor { get; set; } = "#4caf50";

        /// <summary>EXP points awarded when this level is reached.</summary>
        public int ExpReward { get; set; } = 0;

        /// <summary>The badge that is auto-awarded when this level is reached. Null = no badge.</summary>
        public int? LevelUpBadgeId { get; set; }

        public virtual ICollection<UserLevel> UserLevels { get; set; } = new List<UserLevel>();
    }
}

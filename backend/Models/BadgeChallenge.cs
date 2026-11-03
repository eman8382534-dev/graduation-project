// ============================================================
//  BadgeChallenge.cs — NEW Badge + Level System (Phase 10)
//
//  Each Badge is now unlocked ONLY via completing challenges.
//  A challenge belongs to ONE badge and has a measurable target.
//  This replaces the legacy BadgeLogic threshold system.
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GrawiaaApp.API.Models
{
    /// <summary>
    /// A measurable challenge that must be completed to unlock a badge.
    /// Challenges are evaluated against DailyLogEntry data.
    /// </summary>
    public class BadgeChallenge
    {
        [Key]
        public int Id { get; set; }

        /// <summary>The badge that this challenge contributes to unlocking.</summary>
        public int BadgeId { get; set; }
        [ForeignKey("BadgeId")]
        public virtual Badge Badge { get; set; } = null!;

        /// <summary>Display text shown to the child. e.g. "Log 5 days in a row"</summary>
        [Required, MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        /// <summary>Emoji icon for the challenge. e.g. "🔥"</summary>
        [MaxLength(10)]
        public string Emoji { get; set; } = "⭐";

        /// <summary>
        /// Type of metric this challenge measures.
        /// Valid values: streak, total_logs, tasks_completed, pillar_pct,
        /// hobby_count, has_hobby, prayer_day, quran_days, water_six, subjects_day,
        /// helped_family, kind_word, has_parent_link, has_hero_name,
        /// movement_days, movement_badges, journal_text (Phase 12 — AI-team-owned;
        /// the backend's NewProgressionService never evaluates these, the AI
        /// team's NLP pipeline writes IsCompleted directly via
        /// POST /api/ai/challenge-progress/{hashedChildId})
        /// </summary>
        [Required, MaxLength(50)]
        public string MetricType { get; set; } = string.Empty;

        /// <summary>
        /// For pillar_pct: which pillar. e.g. "Spiritual", "Academic", "Sport", etc.
        /// For other types: null.
        /// </summary>
        [MaxLength(50)]
        public string? MetricSubType { get; set; }

        /// <summary>Target value to reach. e.g. 5 for streak of 5 days, 30 for pct of 30%</summary>
        public double TargetValue { get; set; }

        /// <summary>Display order within the badge's challenge list.</summary>
        public int SortOrder { get; set; } = 0;

        public virtual ICollection<ChildChallengeProgress> ChildProgresses { get; set; } = new List<ChildChallengeProgress>();
    }
}
// ============================================================
//  LeafyTip.cs — Leafy AI System
//
//  Admin-seeded recommendation content that the AI engine
//  dynamically filters and serves based on the child's
//  real-time pillar percentages.
//
//  Three content channels per tip:
//    Tip        → child-facing practical advice (lowest pillar target)
//    Alert      → parent-facing urgent notification (anomaly / severe lag)
//    Motivation → child-facing praise (daily/weekly achievement highlight)
//
//  Filtering dimensions:
//    Side    → which of the 8 pillars this record targets
//    Type    → tone: "encouragement" | "warning" | "achievement"
//    Purpose → audience: "tip" | "alert" | "motivation"
//
//  AI engine uses Purpose to route content to the correct
//  dashboard panel without any additional lookup.
// ============================================================

using System.ComponentModel.DataAnnotations;

namespace GrawiaaApp.API.Models
{
    // ── Allowed Purpose values ────────────────────────────────────────
    public static class LeafyTipPurpose
    {
        public const string Tip = "tip";
        public const string Alert = "alert";
        public const string Motivation = "motivation";
    }

    // ── Allowed Type values ───────────────────────────────────────────
    public static class LeafyTipType
    {
        public const string Encouragement = "encouragement";
        public const string Warning = "warning";
        public const string Achievement = "achievement";
    }

    // ── Allowed Side values — one per 8 pillars ───────────────────────
    public static class LeafyTipSide
    {
        public const string Spiritual = "spiritual";    // Pillar 1 — Connection with Allah
        public const string Social = "social";       // Pillar 2 — Social Interaction
        public const string Academic = "academic";     // Pillar 3 — Homework & Study
        public const string Environment = "environment";  // Pillar 4 — Family Chores
        public const string Physical = "physical";     // Pillar 5 — Nutrition & Food
        public const string Sport = "sport";        // Pillar 6 — Sport & Movement
        public const string Creativity = "creativity";   // Pillar 7 — Hobbies
        public const string Mental = "mental";       // Pillar 8 — Mental Health / Mood
    }

    public class LeafyTip
    {
        [Key]
        public int Id { get; set; }

        // ── Routing / Filtering Dimensions ───────────────────────────

        /// <summary>
        /// The 8-pillar side this record targets.
        /// Values defined in <see cref="LeafyTipSide"/>:
        /// spiritual | social | academic | environment | physical | sport | creativity | mental
        /// </summary>
        [Required]
        [MaxLength(30)]
        public string Side { get; set; } = string.Empty;

        /// <summary>
        /// Tone of the content.
        /// Values defined in <see cref="LeafyTipType"/>:
        /// encouragement | warning | achievement
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Distribution channel — determines which dashboard panel receives this content.
        /// Values defined in <see cref="LeafyTipPurpose"/>:
        ///   "tip"        → child dashboard (lowest-pillar practical advice)
        ///   "alert"      → parent dashboard (severe lag / anomaly notification)
        ///   "motivation" → child dashboard (achievement praise)
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Purpose { get; set; } = LeafyTipPurpose.Tip;

        // ── Content Fields (three channels) ──────────────────────────

        /// <summary>
        /// Practical advice delivered to the CHILD based on their lowest / target
        /// pillar percentage.  Populated when Purpose = "tip".
        /// Example: "Try to pray Fajr on time tomorrow — even 5 minutes of Quran
        ///           reading at dawn will boost your Spiritual score! 🕌"
        /// </summary>
        [MaxLength(500)]
        public string? Tip { get; set; }

        /// <summary>
        /// Urgent notification directed to the PARENT dashboard when the child is
        /// severely lagging (score &lt; 20) or showing anomalies in a specific pillar.
        /// Populated when Purpose = "alert".
        /// Example: "⚠️ {ChildName}'s Spiritual score has dropped below 20% this week.
        ///           Consider scheduling family Quran time together."
        /// </summary>
        [MaxLength(500)]
        public string? Alert { get; set; }

        /// <summary>
        /// Encouraging praise message delivered to the CHILD highlighting their
        /// specific daily / weekly achievements.
        /// Populated when Purpose = "motivation".
        /// Example: "🌟 Amazing! You completed all your prayers AND helped with
        ///           chores today — that's a double win, hero!"
        /// </summary>
        [MaxLength(500)]
        public string? Motivation { get; set; }

        // ── Legacy Single-Content Field (backward compat) ─────────────

        /// <summary>
        /// Legacy unified content field kept for backward compatibility with
        /// existing GET /api/Admin/leafy-tips responses and seeded data.
        /// New code should prefer <see cref="Tip"/>, <see cref="Alert"/>, or
        /// <see cref="Motivation"/> based on <see cref="Purpose"/>.
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string Content { get; set; } = string.Empty;

        // ── Computed Helper (NotMapped) ───────────────────────────────

        /// <summary>
        /// Returns the correct channel content based on Purpose, falling back to
        /// the legacy Content field so existing records continue to work.
        /// </summary>
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string ResolvedContent => Purpose switch
        {
            LeafyTipPurpose.Alert => Alert ?? Content,
            LeafyTipPurpose.Motivation => Motivation ?? Content,
            _ => Tip ?? Content,
        };
    }
}
// ============================================================
//  Badge.cs — PATCHED for Phase 10 + Spec Compliance
//
//  Added: Pillar percentage columns (acad_pct, soc_pct, etc.)
//  to align with the AI team DB spec:
//      badges(badge_id, badge_name, description,
//             acad_pct, soc_pct, ment_pct, phys_pct,
//             spir_pct, env_pct, crea_pct, res_pct)
//
//  These columns are populated on Badge creation/seeding.
//  For pillar_pct badges (IDs 28-51), exactly one is non-zero.
//  For non-pillar badges, all remain 0.
//
//  Navigation to Challenges is preserved for challenge logic.
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GrawiaaApp.API.Models
{
    public class Badge
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
        public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
        public string PersonalitySide { get; set; } = string.Empty; // "Left" | "Right"
        public BadgeCategory Category { get; set; }

        // ── AI Team Spec: Pillar Percentage Columns ────────────────────────
        // Spec: acad_pct, soc_pct, ment_pct, phys_pct, spir_pct, env_pct, crea_pct, res_pct
        // Stored as DECIMAL(5,2) → supports 0.00 to 999.99 (practically 0–100)
        // Meaning: the target pillar % this badge contributes to / represents.
        // For non-pillar badges these remain 0.

        /// <summary>Academic pillar percentage target (maps to spec column acad_pct).</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal AcadPct { get; set; } = 0;

        /// <summary>Social pillar percentage target (maps to spec column soc_pct).</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal SocPct { get; set; } = 0;

        /// <summary>Mental/Mood pillar percentage target (maps to spec column ment_pct).</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal MentPct { get; set; } = 0;

        /// <summary>Physical/Nutrition pillar percentage target (maps to spec column phys_pct).</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal PhysPct { get; set; } = 0;

        /// <summary>Spiritual pillar percentage target (maps to spec column spir_pct).</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal SpirPct { get; set; } = 0;

        /// <summary>Environment/HomeRes pillar percentage target (maps to spec column env_pct).</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal EnvPct { get; set; } = 0;

        /// <summary>Creative/Hobbies pillar percentage target (maps to spec column crea_pct).</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal CreaPct { get; set; } = 0;

        /// <summary>Resilience/Sport pillar percentage target (maps to spec column res_pct).</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal ResPct { get; set; } = 0;

        // ── Navigation properties ──────────────────────────────────────
        public virtual ICollection<ChildBadge> ChildBadges { get; set; } = new List<ChildBadge>();

        /// <summary>
        /// Challenges that must ALL be completed to unlock this badge.
        /// Empty = legacy badge (no challenge gating, not awarded by new engine).
        /// </summary>
        public virtual ICollection<BadgeChallenge> Challenges { get; set; } = new List<BadgeChallenge>();
    }
}

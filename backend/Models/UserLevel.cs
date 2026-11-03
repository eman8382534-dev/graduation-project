// ============================================================
//  UserLevel.cs — UPDATED for AI Team Spec Compliance (Phase 10)
//
//  Added: HashedChildId (string) — AI team spec requires
//  "hashed_child_id" on the user_levels table.
//
//  HashedChildId is populated by NewLevelEngineService when
//  creating or updating UserLevel records. It is derived from
//  ChildIdentityService.GetSafeChildKey(childId) — the same
//  deterministic hash used in all privacy-sensitive payloads.
//
//  ChildId (int FK) remains the PRIMARY database key for all
//  EF Core queries — HashedChildId is an ADDITIONAL column
//  satisfying the AI team's schema requirement.
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GrawiaaApp.API.Models
{
    public class UserLevel
    {
        [Key]
        public int Id { get; set; }

        public int ChildId { get; set; }
        [ForeignKey("ChildId")]
        public virtual ApplicationUser Child { get; set; } = null!;

        /// <summary>
        /// AI team spec: "hashed_child_id" — privacy-safe identifier
        /// derived from ChildId via SHA-256 + salt. Set by
        /// NewLevelEngineService when creating/updating this record.
        /// NOT a DB foreign key — readable by AI team directly from SQL.
        /// </summary>
        [MaxLength(64)]
        public string HashedChildId { get; set; } = string.Empty;

        /// <summary>FK to the Levels table — the current level the child is at.</summary>
        public int LevelId { get; set; }
        [ForeignKey("LevelId")]
        public virtual Level Level { get; set; } = null!;

        /// <summary>Convenience mirror: LevelNumber from the Level entity.</summary>
        public int LevelNumber { get; set; } = 1;

        /// <summary>Accumulated EXP for the current level.</summary>
        public int CurrentExp { get; set; } = 0;

        /// <summary>Total EXP earned across all time.</summary>
        public int TotalExp { get; set; } = 0;

        /// <summary>When the child last leveled up.</summary>
        public DateTime? LastLevelUpAt { get; set; }

        /// <summary>When this record was last updated by the progression engine.</summary>
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

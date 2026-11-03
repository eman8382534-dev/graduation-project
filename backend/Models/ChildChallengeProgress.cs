// ============================================================
//  ChildChallengeProgress.cs — UPDATED for AI Team Spec (Phase 10)
//
//  Added: HashedChildId (string) — AI team spec requires
//  "hashed_child_id" on the child_challenge_progress table.
//
//  Added: UnlockedAt (DateTime?) — AI team spec field name for
//  when the challenge was completed (alias for CompletedAt).
//
//  Added: IsCompleted bool (already exists) — confirmed.
//
//  AI team spec fields:
//    progress_id   → Id
//    hashed_child_id → HashedChildId (NEW)
//    challenge_id  → ChallengeId
//    is_completed  → IsCompleted
//    unlocked_at   → UnlockedAt (alias for CompletedAt)
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GrawiaaApp.API.Models
{
    public class ChildChallengeProgress
    {
        [Key]
        public int Id { get; set; }

        public int ChildId { get; set; }
        [ForeignKey("ChildId")]
        public virtual ApplicationUser Child { get; set; } = null!;

        /// <summary>
        /// AI team spec: "hashed_child_id" — privacy-safe identifier.
        /// Derived from ChildId via ChildIdentityService.
        /// NOT a DB foreign key — additional column for AI team SQL access.
        /// </summary>
        [MaxLength(64)]
        public string HashedChildId { get; set; } = string.Empty;

        public int ChallengeId { get; set; }
        [ForeignKey("ChallengeId")]
        public virtual BadgeChallenge Challenge { get; set; } = null!;

        /// <summary>Current measured value for this metric. e.g. streak=3, pillar_pct=45.2</summary>
        public double CurrentValue { get; set; } = 0;

        /// <summary>True when CurrentValue >= Challenge.TargetValue</summary>
        public bool IsCompleted { get; set; } = false;

        /// <summary>
        /// AI team spec: "unlocked_at" — when the challenge was first completed.
        /// Null if not yet done. Same value as CompletedAt (canonical name for this field).
        /// </summary>
        public DateTime? UnlockedAt { get; set; }

        /// <summary>When the challenge was first completed (internal alias — same column as UnlockedAt).</summary>
        public DateTime? CompletedAt { get => UnlockedAt; set => UnlockedAt = value; }

        /// <summary>Last time this progress record was recalculated.</summary>
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

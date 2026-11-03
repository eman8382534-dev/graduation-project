// ============================================================
//  AdminChildrenPersonalityCluster.cs — Leafy AI System  (Phase 12)
//
//  Per-child personality-cluster assignment history, written by
//  the external AI team's periodic clustering job and surfaced
//  on the Admin dashboard ("AdminChildrenPersonalityClusters").
//
//  One row per (child, clustering run). On every run the AI team
//  flips the child's previous row(s) to IsCurrent = false and
//  inserts a new IsCurrent = true row — full history is preserved
//  for admin drill-down / trend review, while ChildProfile keeps
//  a denormalized pointer (LastPersonalityClusterId) to the latest
//  one for fast reads without a join.
//
//  WRITE OWNERSHIP: ChildId, PersonalityClusterId, SimilarityScore,
//  ClusteringRunId, IsCurrent, AssignedAt are written EXCLUSIVELY
//  by the AI team via POST /api/ai/personality-clusters(/batch).
//  The backend never computes a similarity score or decides cluster
//  membership — it only persists, validates existence of the child
//  and cluster, and exposes this table through the Admin API.
// ============================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GrawiaaApp.API.Models
{
    public class AdminChildrenPersonalityCluster
    {
        [Key]
        public int Id { get; set; }

        public int ChildId { get; set; }

        [ForeignKey("ChildId")]
        public virtual ChildProfile Child { get; set; } = null!;

        /// <summary>
        /// Privacy-safe child identifier (see IChildIdentityService), denormalized
        /// onto this row at write time — same pattern as DailyLogEntry.HashedChildId
        /// / ChildChallengeProgress.HashedChildId / UserLevel.HashedChildId. Lets the
        /// AI team query GET /api/ai/personality-cluster/{hashedChildId} directly,
        /// without a join through ChildProfile (which has no hash column itself).
        /// </summary>
        [MaxLength(64)]
        public string HashedChildId { get; set; } = string.Empty;

        public int PersonalityClusterId { get; set; }

        [ForeignKey("PersonalityClusterId")]
        public virtual PersonalityCluster Cluster { get; set; } = null!;

        /// <summary>
        /// AI confidence / closeness-to-centroid for this child in this
        /// cluster. Range: 0 (weak fit) – 100 (very strong fit).
        /// </summary>
        public double SimilarityScore { get; set; }

        /// <summary>True for the child's most recent assignment only. Older rows are flipped to false on each new run.</summary>
        public bool IsCurrent { get; set; } = true;

        /// <summary>
        /// Groups every assignment produced by the same periodic clustering
        /// run (e.g. a UTC-timestamp-based run identifier sent by the AI
        /// team), so admins can review "what the clustering job produced
        /// on run X" as a batch.
        /// </summary>
        [MaxLength(100)]
        public string? ClusteringRunId { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }
}

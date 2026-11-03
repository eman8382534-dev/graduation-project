// ============================================================
//  PersonalityCluster.cs — Leafy AI System  (Phase 12)
//
//  Catalog of personality clusters produced by the external AI
//  team's periodic clustering job (e.g. k-means / hierarchical
//  clustering over the 8 pillars + 5 traits + AI signal history
//  across all children). Each row is ONE cluster definition —
//  analogous to how LeafyTip is a catalog of admin/AI content
//  that ChildProfile / AdminChildrenPersonalityCluster point into.
//
//  WRITE OWNERSHIP:
//  ─────────────────────────────────────────────────────────
//  • ClusterKey, Description, DominantTraits, Characteristics,
//    RecommendedFocus  → written EXCLUSIVELY by the AI team via
//    POST /api/ai/personality-clusters (upserted by ClusterKey).
//    The backend never computes or infers these.
//  • Name              → AI-seeded but admin-editable (PUT
//    /api/Admin/personality-clusters/{id}) for human-friendly
//    relabeling, exactly like LeafyTip content curation.
//  • MemberCount       → maintained by the backend's periodic
//    PeriodicClusteringOrchestrationService as a simple COUNT
//    over AdminChildrenPersonalityClusters.IsCurrent rows. This
//    is bookkeeping, NOT re-clustering — the cluster ASSIGNMENT
//    itself is never recomputed by the backend.
// ============================================================

using System.ComponentModel.DataAnnotations;

namespace GrawiaaApp.API.Models
{
    public class PersonalityCluster
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Stable machine key the AI team uses to upsert this cluster across
        /// clustering runs (e.g. "cluster_steady_achiever", "cluster_2").
        /// Must stay constant run-to-run for the same conceptual cluster so
        /// re-posting updates the existing row instead of duplicating it.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string ClusterKey { get; set; } = string.Empty;

        /// <summary>Human-friendly display name. AI-seeded, admin-editable.</summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>AI-generated narrative description of this cluster's typical profile.</summary>
        [MaxLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// Which of the 5 core traits / 8 pillars most define this cluster.
        /// Free-form AI text (e.g. "High Vitality, High Curiosity, Low Resilience").
        /// </summary>
        [MaxLength(300)]
        public string? DominantTraits { get; set; }

        /// <summary>
        /// AI-computed characteristic feature vector / centroid statistics for
        /// this cluster, stored as opaque JSON exactly as the AI team sends it
        /// (e.g. {"academic":0.72,"social":0.41,...}). The backend persists
        /// and returns this verbatim — it never parses or recomputes it.
        /// </summary>
        public string? Characteristics { get; set; }

        /// <summary>AI guidance on what focus area children in this cluster typically benefit from.</summary>
        [MaxLength(500)]
        public string? RecommendedFocus { get; set; }

        /// <summary>
        /// Denormalized count of children currently assigned to this cluster
        /// (IsCurrent = true rows in AdminChildrenPersonalityClusters).
        /// Refreshed by PeriodicClusteringOrchestrationService — bookkeeping
        /// only, not a re-clustering computation.
        /// </summary>
        public int MemberCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>UTC timestamp the AI team last refreshed this cluster definition.</summary>
        public DateTime? LastComputedAt { get; set; }

        // ── Navigation ──────────────────────────────────────────────
        public virtual ICollection<AdminChildrenPersonalityCluster> Assignments { get; set; } = new List<AdminChildrenPersonalityCluster>();
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GrawiaaApp.API.Models
{
    public class ChildTask
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Points { get; set; }

        /// <summary>
        /// For parent-assigned tasks: permanent completion (done once, stays done).
        /// For fixed journal tasks (IsFixedTask = true): this is NOT used for daily tracking —
        /// use IsCompletedToday instead, which resets nightly.
        /// </summary>
        public bool IsCompleted { get; set; } = false;

        /// <summary>
        /// Daily completion flag — applies to fixed journal tasks (IsFixedTask = true) only.
        /// Reflects whether the child completed this task TODAY (based on today's journal entry).
        /// Reset to false every night by DailyLogClosureService alongside log finalization.
        /// Parent-assigned tasks use IsCompleted (permanent), not this field.
        /// </summary>
        public bool IsCompletedToday { get; set; } = false;

        // التصنيف: مهم جداً للرادار (Spiritual, Physical, Academic, Social, Mental)
        public string Category { get; set; } = "General";

        public DateTime DueDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public int ChildId { get; set; }

        /// <summary>
        /// AI team spec — Anomaly Detection Trust Engine reads "Tasks in child profile table"
        /// directly via SQL. Populated by ChildIdentityService.GetSafeChildKey(childId)
        /// at task-creation time, same as DailyLogEntry/ChildChallengeProgress.
        /// </summary>
        [MaxLength(64)]
        public string HashedChildId { get; set; } = string.Empty;

        public int? ParentId { get; set; }
        public bool IsParentAssigned { get; set; } = false;

        /// <summary>
        /// True for the default fixed tasks seeded automatically for every new child
        /// (the 5 daily prayers + other recurring journal activities: Reading Quran,
        /// Athkar, Doaa, and any future fixed items from the journal's static sections).
        /// These tasks are permanent per-child rows — they are never deleted, just
        /// reset daily (IsCompletedToday). Parent-assigned tasks and any custom tasks
        /// have IsFixedTask = false.
        /// </summary>
        public bool IsFixedTask { get; set; } = false;

        [ForeignKey("ChildId")]
        public virtual ApplicationUser Child { get; set; } = null!;
    }
}
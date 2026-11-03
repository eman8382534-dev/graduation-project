// ============================================================
//  DefaultTaskSeederService.cs
//
//  Seeds the fixed recurring tasks that every child has by default.
//  These mirror the static activity buttons in the journal UI
//  (prayers, Quran, etc.) so the AI team's Anomaly Detection
//  Trust Engine can read a unified task list from ChildTasks
//  that includes BOTH fixed defaults AND parent-assigned extras.
//
//  Called once at child registration (AuthController.RegisterChild).
//  Never called again — the rows are permanent, only reset daily
//  by DailyLogClosureService via ResetFixedTasksForToday().
//
//  FIXED TASK LIST (mirrors journal.html static items):
//    Spiritual   → Fajr, Dhuhr, Asr, Maghrib, Isha,
//                  Reading Quran, Athkar, Doaa
//
//  AI TEAM NOTE:
//  The /api/ai/tasks/{hashedChildId} response now includes:
//    total_tasks_count    — all tasks (fixed + parent-assigned)
//    completed_tasks_count — completed today (IsCompletedToday
//                           for fixed, IsCompleted for parent tasks)
//    fixed_tasks          — the fixed recurring rows
//    parent_tasks         — parent-assigned rows (IsParentAssigned=true)
// ============================================================

using GrawiaaApp.API.Data;
using GrawiaaApp.API.Models;
using Microsoft.EntityFrameworkCore;

namespace GrawiaaApp.API.Services
{
    public interface IDefaultTaskSeederService
    {
        /// <summary>
        /// Seeds all fixed recurring tasks for a newly registered child.
        /// Must be called after the child's ApplicationUser row exists in DB.
        /// Does NOT call SaveChangesAsync — caller is responsible for saving.
        /// </summary>
        void SeedFixedTasksForChild(int childId, string hashedChildId, ApplicationDbContext context);

        /// <summary>
        /// Resets IsCompletedToday = false for all fixed tasks of a given child.
        /// Called nightly by DailyLogClosureService.
        /// </summary>
        Task ResetFixedTasksForChildAsync(int childId, ApplicationDbContext context, CancellationToken ct = default);
    }

    public class DefaultTaskSeederService : IDefaultTaskSeederService
    {
        // ── Fixed task definitions ────────────────────────────────────
        // Each tuple: (Title, Category, Points)
        // Category must match the pillar categories used throughout the system.
        private static readonly (string Title, string Category, int Points)[] FixedTasks = new[]
        {
            // ── Spiritual (5 prayers + daily worship) ──────────────────
            ("Fajr",          "Spiritual", 10),
            ("Dhuhr",         "Spiritual", 10),
            ("Asr",           "Spiritual", 10),
            ("Maghrib",       "Spiritual", 10),
            ("Isha",          "Spiritual", 10),
            ("Reading Quran", "Spiritual",  8),
            ("Athkar",        "Spiritual",  5),
            ("Doaa",          "Spiritual",  5),
        };

        public void SeedFixedTasksForChild(int childId, string hashedChildId, ApplicationDbContext context)
        {
            // Use a far-future DueDate so fixed tasks never appear "overdue"
            // in any due-date-based sort or filter.
            var farFuture = new DateTime(2099, 12, 31, 0, 0, 0, DateTimeKind.Utc);
            var now = DateTime.UtcNow;

            foreach (var (title, category, points) in FixedTasks)
            {
                context.ChildTasks.Add(new ChildTask
                {
                    ChildId = childId,
                    HashedChildId = hashedChildId,
                    Title = title,
                    Description = null,
                    Category = category,
                    Points = points,
                    IsFixedTask = true,
                    IsParentAssigned = false,
                    IsCompleted = false,       // fixed tasks never permanently complete
                    IsCompletedToday = false,
                    DueDate = farFuture,
                    CreatedAt = now,
                });
            }
        }

        public async Task ResetFixedTasksForChildAsync(
            int childId,
            ApplicationDbContext context,
            CancellationToken ct = default)
        {
            var fixedTasks = context.ChildTasks
                .Where(t => t.ChildId == childId && t.IsFixedTask && t.IsCompletedToday);

            await foreach (var task in fixedTasks.AsAsyncEnumerable().WithCancellation(ct))
            {
                task.IsCompletedToday = false;
            }
        }
    }
}
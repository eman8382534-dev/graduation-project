using GrawiaaApp.API.Data;
using GrawiaaApp.API.DTOs;
using GrawiaaApp.API.Interfaces;
using GrawiaaApp.API.Models;
using GrawiaaApp.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GrawiaaApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TasksController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICacheService _cache;
        private readonly IGamingService _gamingService;
        private readonly INewProgressionService _newProgression;   // PHASE 10
        private readonly IChildIdentityService _childIdentity;
        private readonly IPillarPipelineService _pillarPipeline;

        public TasksController(
            ApplicationDbContext context,
            ICacheService cache,
            IGamingService gamingService,
            INewProgressionService newProgression,   // PHASE 10
            IChildIdentityService childIdentity,
            IPillarPipelineService pillarPipeline)
        {
            _context = context;
            _cache = cache;
            _gamingService = gamingService;
            _newProgression = newProgression;        // PHASE 10
            _childIdentity = childIdentity;
            _pillarPipeline = pillarPipeline;
        }

        // POST /api/Tasks/assign  [Parent]
        [Authorize(Roles = "Parent")]
        [HttpPost("assign")]
        public async Task<IActionResult> AssignTask([FromBody] AddTaskDto dto)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int parentId))
                return Unauthorized("Invalid token");

            var childProfile = await _context.ChildProfiles
                .FirstOrDefaultAsync(cp => cp.UserId == dto.ChildId && cp.ParentId == parentId);
            if (childProfile == null)
                return Forbid();

            var task = new ChildTask
            {
                ChildId = dto.ChildId,
                HashedChildId = _childIdentity.GetSafeChildKey(dto.ChildId),
                ParentId = parentId,
                Title = dto.Title,
                Description = dto.Description,
                Points = dto.Points,
                DueDate = dto.DueDate,
                IsParentAssigned = true,
                IsCompleted = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.ChildTasks.Add(task);
            await _context.SaveChangesAsync();

            _context.Notifications.Add(new Notification
            {
                ReceiverId = dto.ChildId,
                Message = $"🎯 New task from your parent: {dto.Title}",
                IsUrgent = false,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            _cache.RemoveByPrefix(CacheKeys.ChildPrefix(dto.ChildId));

            return Ok(new { message = "Task assigned!", taskId = task.Id });
        }

        // GET /api/Tasks/my-tasks  [Child]
        // Returns actual tasks assigned by the parent to complete.
        // Excludes plan-template items (Description == "__plan_template__")
        // which are the parent's configured Chores/Subjects/Sports list —
        // those are reference data for the journal, not completable tasks.
        [Authorize(Roles = "Child")]
        [HttpGet("my-tasks")]
        public async Task<IActionResult> GetMyTasks()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int childId))
                return Unauthorized("Invalid token");

            var tasks = await _context.ChildTasks
                .Where(t => t.ChildId == childId && t.Description != "__plan_template__")
                .OrderBy(t => t.IsCompleted)
                .ThenBy(t => t.DueDate)
                .Select(t => new
                {
                    t.Id,
                    t.Title,
                    t.Description,
                    t.Points,
                    t.IsCompleted,
                    t.IsParentAssigned,
                    t.Category,
                    dueDate = t.DueDate.ToString("yyyy-MM-dd"),
                    completedAt = t.CompletedAt.HasValue
                                  ? t.CompletedAt.Value.ToString("yyyy-MM-dd")
                                  : null
                })
                .ToListAsync();

            return Ok(tasks);
        }

        // PATCH /api/Tasks/{id}/complete  [Child]
        // PHASE 10: Task completion runs NewProgressionService, which evaluates
        // ChildChallengeProgresses (incl. tasks_completed for TaskMaster5/20/50)
        // and may unlock badges / trigger a level-up — previously this only
        // fired a no-op IGamingService.UpdateScore(childId, 0) call.
        [Authorize(Roles = "Child")]
        [HttpPatch("{id}/complete")]
        public async Task<IActionResult> CompleteTask(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int childId))
                return Unauthorized("Invalid token");

            var task = await _context.ChildTasks
                .FirstOrDefaultAsync(t => t.Id == id && t.ChildId == childId);

            if (task == null) return NotFound("Task not found");
            if (task.IsCompleted) return BadRequest("Task already completed");

            task.IsCompleted = true;
            task.CompletedAt = DateTime.UtcNow;

            // Award XP for task completion
            var childProfile = await _context.ChildProfiles
                .FirstOrDefaultAsync(cp => cp.UserId == childId);
            if (childProfile != null)
            {
                int expGain = Math.Max(5, task.Points > 0 ? task.Points : 10);
                childProfile.CurrentExp += expGain;

                // PHASE 10 UPDATE: Level-up evaluation is now handled by
                // NewProgressionService.RunAsync(), called below after
                // SaveChangesAsync(). It checks DB-stored challenge
                // completions (e.g. tasks_completed >= target via
                // ChildChallengeProgresses) rather than the old
                // LevelRequirementsEngine / AllPillarsReady threshold logic.
                // CurrentExp accumulation here is still correct and unaffected.
                //
                // Original FIX rationale (still valid):
                //   1. AllPillarsReady required ALL 8 pillars above threshold
                //      simultaneously — almost never true, and when it WAS true
                //      it wiped all 8 pillar percentages to 0.
                //   2. Level-up is now driven by the unified NewProgressionService,
                //      called from BOTH the journal-save path AND this task-complete
                //      path, using the same DB-stored Levels/BadgeChallenges.
                //   3. ExpToNextLevel remains a computed property; no manual
                //      subtraction needed here.
                //
                // What we do instead: accumulate XP here, then immediately run
                // NewProgressionService.RunAsync() below — it evaluates
                // challenges and level-up using DB-stored Levels/BadgeChallenges,
                // for both this task-complete path and the journal-save path.
                //
                // AUDIT FIX #1: ChildProfile.AllPillarsReady / AspectThreshold
                // have since been DELETED (they were dead NotMapped properties
                // with no reader anywhere — not this gate, not any DTO, not the
                // frontend). This comment block is kept only for historical
                // context on why the level-up logic moved off that approach;
                // there is no remaining code reference to AllPillarsReady.
            }

            // Notify parent if parent-assigned task
            if (task.IsParentAssigned && task.ParentId.HasValue)
            {
                var childUser = await _context.Users.FindAsync(childId);
                var childName = childUser?.FullName ?? "Your child";

                _context.Notifications.Add(new Notification
                {
                    ReceiverId = task.ParentId.Value,
                    Message = $"✅ {childName} completed the task: \"{task.Title}\" today!",
                    IsUrgent = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            // Recompute TotalTasksCount / CompletedTasksCount on ChildProfile
            // so the AI team's Anomaly Detection endpoint stays in sync.
            await _pillarPipeline.RecalculateTaskCountsAsync(childId);
            await _context.SaveChangesAsync();

            // PHASE 10: Run new progression engine — evaluates tasks_completed
            // metric for TaskMaster5/20/50 badges (and any other challenge type
            // affected by this completion) and checks level-up.
            var progressionResult = await _newProgression.RunAsync(childId);

            // Keep TrustScore observer pipeline in sync (0 points = no TrustScore
            // change, but ParentNotificationLogic still runs) — matches the
            // pre-Phase-10 "fire badge evaluation" call's side effects.
            await _gamingService.UpdateScore(childId, 0);

            _cache.RemoveByPrefix(CacheKeys.ChildPrefix(childId));

            // Fetch newly unlocked badge details for the response
            var newlyUnlockedBadges = new List<object>();
            if (progressionResult.NewlyUnlockedBadgeIds.Any())
            {
                newlyUnlockedBadges = await _context.Badges
                    .Where(b => progressionResult.NewlyUnlockedBadgeIds.Contains(b.Id))
                    .Select(b => (object)new { b.Id, b.Name, b.Description, b.IconUrl })
                    .ToListAsync();
            }

            return Ok(new
            {
                message = "Task completed! 🎉",
                points = task.Points,
                leveledUp = progressionResult.LeveledUp,
                currentLevel = progressionResult.CurrentLevel,
                levelName = progressionResult.LeveledUp ? progressionResult.LevelName : null,
                newlyUnlockedBadges
            });
        }

        // GET /api/Tasks/parent-view/{childId}  [Parent]
        // Excludes plan-template items (Description == "__plan_template__") —
        // those are managed via PATCH /api/Parent/hero-management/{heroId}
        // and shown in the daily plan editor, not as task items here.
        [Authorize(Roles = "Parent")]
        [HttpGet("parent-view/{childId}")]
        public async Task<IActionResult> GetChildTasks(int childId)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int parentId))
                return Unauthorized("Invalid token");

            var isHisChild = await _context.ChildProfiles
                .AnyAsync(cp => cp.UserId == childId && cp.ParentId == parentId);
            if (!isHisChild) return Forbid();

            var tasks = await _context.ChildTasks
                .Where(t => t.ChildId == childId && t.Description != "__plan_template__")
                .OrderBy(t => t.IsCompleted)
                .ThenByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    t.Id,
                    t.Title,
                    t.Description,
                    t.Points,
                    t.IsCompleted,
                    t.IsParentAssigned,
                    t.Category,
                    dueDate = t.DueDate.ToString("yyyy-MM-dd"),
                    completedAt = t.CompletedAt.HasValue
                                  ? t.CompletedAt.Value.ToString("yyyy-MM-dd")
                                  : null
                })
                .ToListAsync();

            return Ok(tasks);
        }

        // DELETE /api/Tasks/{id}  [Parent]
        [Authorize(Roles = "Parent")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int parentId))
                return Unauthorized("Invalid token");

            var task = await _context.ChildTasks
                .FirstOrDefaultAsync(t => t.Id == id && t.ParentId == parentId);
            if (task == null) return NotFound("Task not found");

            _context.ChildTasks.Remove(task);
            await _context.SaveChangesAsync();

            _cache.RemoveByPrefix(CacheKeys.ChildPrefix(task.ChildId));

            return Ok(new { message = "Task deleted." });
        }
    }
}
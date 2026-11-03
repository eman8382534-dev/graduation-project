using GrawiaaApp.API.Data;
using GrawiaaApp.API.Interfaces;
using GrawiaaApp.API.Models;
using GrawiaaApp.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GrawiaaApp.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class DailyLogsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICacheService _cache;

        public DailyLogsController(
            ApplicationDbContext context,
            ICacheService cache)
        {
            _context = context;
            _cache = cache;
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/DailyLogs/my-logs  [Child]
        // Used by history.html — returns last 30 log entries.
        // NOTE: /api/Children/daily-logs is the preferred endpoint
        // (returns 90 entries with full detail).
        //
        // FIX (Cache Key Conflict): This endpoint previously shared the
        // CacheKeys.ChildLogs(childId) key with GET /api/Children/daily-logs.
        // Because both endpoints project different shapes (this one projects 8
        // fields; Children/daily-logs projects 15+ fields), whichever endpoint
        // ran first would populate the cache and the other would return the wrong
        // response shape, causing history.html or heroprof.html to silently break.
        //
        // Fix: use a dedicated cache key "dailylogs:{childId}:mylogsv1" so the
        // two projections never collide.
        // ─────────────────────────────────────────────────────────────
        [Authorize(Roles = "Child")]
        [HttpGet("my-logs")]
        public async Task<IActionResult> GetMyLogs()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int childId))
                return Unauthorized("Invalid token");

            // FIX: dedicated key — does NOT share with CacheKeys.ChildLogs(childId)
            var cacheKey = $"dailylogs:{childId}:mylogsv1";

            var logs = await _cache.GetOrCreateAsync(
                cacheKey,
                async () => (object)await _context.DailyLogEntries
                    .Where(l => l.ChildId == childId)
                    .OrderByDescending(l => l.LogDate)
                    .Take(30)
                    .Select(l => new
                    {
                        l.Id,
                        logDate = l.LogDate.ToString("yyyy-MM-dd"),
                        l.MoodEmoji,
                        l.WaterGlasses,
                        l.CompletedSubjects,
                        l.PhysicalActivity,
                        l.SpiritualActivities,
                        l.IsJournalSaved,
                        l.SentimentScore
                    })
                    .ToListAsync(),
                TimeSpan.FromMinutes(3)
            );

            return Ok(logs);
        }
    }
}

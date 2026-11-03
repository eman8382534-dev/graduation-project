using GrawiaaApp.API.Data;
using GrawiaaApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GrawiaaApp.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public NotificationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Notifications  [Parent or Child]
        // يجيب آخر 30 إشعار للمستخدم الحالي
        // ─────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetMyNotifications()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
                return Unauthorized("Invalid token");

            var notifications = await _context.Notifications
                .Where(n => n.ReceiverId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(30)
                .Select(n => new
                {
                    n.Id,
                    n.Message,
                    n.IsRead,
                    n.IsUrgent,
                    createdAt = n.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                })
                .ToListAsync();

            return Ok(notifications);
        }

        // ─────────────────────────────────────────────────────────────
        // PATCH /api/Notifications/{id}/read  [Parent or Child]
        // يعلّم الإشعار كمقروء
        // ─────────────────────────────────────────────────────────────
        [HttpPatch("{id}/read")]
        public async Task<IActionResult> MarkRead(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
                return Unauthorized("Invalid token");

            var notif = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.ReceiverId == userId);

            if (notif == null) return NotFound();

            notif.IsRead = true;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Marked as read." });
        }

        // ─────────────────────────────────────────────────────────────
        // PATCH /api/Notifications/mark-all-read  [Parent or Child]
        // يعلّم كل الإشعارات كمقروءة
        // ─────────────────────────────────────────────────────────────
        [HttpPatch("mark-all-read")]
        public async Task<IActionResult> MarkAllRead()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
                return Unauthorized("Invalid token");

            await _context.Notifications
                .Where(n => n.ReceiverId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

            return Ok(new { message = "All notifications marked as read." });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Notifications/unread-count  [Parent or Child]
        // بيرجع عدد الإشعارات غير المقروءة — للـ badge على الناف بار
        // ─────────────────────────────────────────────────────────────
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
                return Unauthorized("Invalid token");

            var count = await _context.Notifications
                .CountAsync(n => n.ReceiverId == userId && !n.IsRead);

            return Ok(new { unreadCount = count });
        }
    }
}

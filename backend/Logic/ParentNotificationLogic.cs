using GrawiaaApp.API.Data;
using GrawiaaApp.API.Interfaces;
using GrawiaaApp.API.Models;
using Microsoft.EntityFrameworkCore;

namespace GrawiaaApp.API.Logic
{
    public class ParentNotificationLogic : IScoreObserver
    {
        private readonly ApplicationDbContext _context;

        public ParentNotificationLogic(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task Execute(int userId, double newScore)
        {
            if (newScore < 70)
            {
                var childProfile = await _context.ChildProfiles
                    .Include(cp => cp.User)
                    .FirstOrDefaultAsync(cp => cp.UserId == userId);

                if (childProfile?.ParentId == null) return;

                var childName = childProfile.User?.FullName ?? "Your hero";

                var alreadyNotified = await _context.Notifications
                    .AnyAsync(n => n.ReceiverId == childProfile.ParentId.Value
                                && n.Message.Contains(childName)
                                && n.CreatedAt.Date == DateTime.UtcNow.Date);

                if (!alreadyNotified)
                {
                    _context.Notifications.Add(new Notification
                    {
                        ReceiverId = childProfile.ParentId.Value,
                        Message = $"⚠️ {childName}'s trust score dropped to {newScore:0}. Consider a check-in chat.",
                        IsRead = false,
                        IsUrgent = true,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                }
            }
        }
    }
}

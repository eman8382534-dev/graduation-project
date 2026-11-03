
using System;

namespace GrawiaaApp.API.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; } = false;

        public bool IsUrgent { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // الربط مع المستخدم (أب أو طفل)
        public int ReceiverId { get; set; }
        public virtual ApplicationUser Receiver { get; set; } = null!;
    }
}
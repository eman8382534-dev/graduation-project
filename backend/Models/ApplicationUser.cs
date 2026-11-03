using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace GrawiaaApp.API.Models
{
    public class ApplicationUser : IdentityUser<int>
    {
        public string FullName { get; set; } = string.Empty;
        public string UserType { get; set; } = "Child";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? ParentId { get; set; }
        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

        // 1:1 Profiles
        public virtual ChildProfile? ChildProfile { get; set; }
        public virtual ParentProfile? ParentProfile { get; set; }
        public virtual AdminProfile? AdminProfile { get; set; }

        // Parent-Child hierarchy
        [ForeignKey("ParentId")]
        public virtual ApplicationUser? Parent { get; set; }
        public virtual ICollection<ApplicationUser> Children { get; set; } = new List<ApplicationUser>();

        // 1:N Child data
        public virtual ICollection<DailyLogEntry> Logs { get; set; } = new List<DailyLogEntry>();
        public virtual ICollection<ChildHobby> ChildHobbies { get; set; } = new List<ChildHobby>();
        public virtual ICollection<Reward> Rewards { get; set; } = new List<Reward>();
        public virtual ICollection<ChildTask> Tasks { get; set; } = new List<ChildTask>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public virtual ICollection<ChildBadge> ChildBadges { get; set; } = new List<ChildBadge>();
    }
}

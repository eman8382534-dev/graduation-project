using System.ComponentModel.DataAnnotations;

namespace GrawiaaApp.API.Models
{
    public class ChildBadge
    {
        [Key]
        public int Id { get; set; }

        public int ChildId { get; set; }
        public virtual ApplicationUser Child { get; set; } = null!;

        public int BadgeId { get; set; }
        public virtual Badge Badge { get; set; } = null!;

        public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}

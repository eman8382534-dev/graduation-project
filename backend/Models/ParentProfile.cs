using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GrawiaaApp.API.Models
{
    public class ParentProfile
    {
        [Key, ForeignKey("User")]
        public int UserId { get; set; }
        public virtual ApplicationUser User { get; set; } = null!;

        public virtual ICollection<ChildProfile> Children { get; set; } = new List<ChildProfile>();
    }
}

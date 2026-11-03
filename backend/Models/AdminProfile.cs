using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GrawiaaApp.API.Models
{
    public class AdminProfile
    {
        [Key]
        [ForeignKey("User")]
        public int UserId { get; set; }
        public virtual ApplicationUser User { get; set; } = null!;
    }
}

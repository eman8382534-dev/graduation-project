
namespace GrawiaaApp.API.Models
{
    public class ChildHobby
    {
        public int ChildId { get; set; }
        public virtual ApplicationUser Child { get; set; } = null!;

        public int HobbyId { get; set; }
        public virtual Hobby Hobby { get; set; } = null!;
    }
}

namespace GrawiaaApp.API.Models
{
    public class Hobby
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // ضيفي السطر ده عشان الـ Errors اللي في الـ Program.cs تروح 👇
        public string? Icon { get; set; }
        public Category Category { get; set; }
        public virtual ICollection<ChildHobby> ChildHobbies { get; set; } = new List<ChildHobby>();
    }
}
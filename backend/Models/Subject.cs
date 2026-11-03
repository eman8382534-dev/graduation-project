using System.ComponentModel.DataAnnotations;

namespace GrawiaaApp.API.Models
{
    public class Subject
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(10)]
        public string Icon { get; set; } = "📚";

        [MaxLength(30)]
        public Category Category { get; set; }
       
    }
}

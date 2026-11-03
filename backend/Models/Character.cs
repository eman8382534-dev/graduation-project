using System.ComponentModel.DataAnnotations;

namespace GrawiaaApp.API.Models
{
    public class Character
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Avatar { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public string Personality { get; set; } = string.Empty;
    }
}

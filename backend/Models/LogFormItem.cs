using System.ComponentModel.DataAnnotations;

namespace GrawiaaApp.API.Models
{
    public class LogFormItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Category { get; set; } = string.Empty; // Religion, Social, ...

        [Required]
        public string Value { get; set; } = string.Empty;   // pray, friends, etc.

        [Required]
        public string Type { get; set; } = "constant";
    }
}
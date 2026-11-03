using System.ComponentModel.DataAnnotations.Schema;

namespace GrawiaaApp.API.Models
{
    public class Reward
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Cost { get; set; }
        public bool IsRedeemed { get; set; } = false;

        public string Description { get; set; } = string.Empty;
        public int ChildId { get; set; }
        // أضيفي بعد ChildId:
        public int? ParentId { get; set; }           // من يوافق
        public bool ParentApproved { get; set; } = false;
        public decimal BudgetLimit { get; set; }     // R9.2
        public DateTime? ApprovedAt { get; set; }
        // في ChildTask و Reward أضيفي:
        [ForeignKey("ChildId")]
        public virtual ApplicationUser Child { get; set; } = null!;
        
    }
}
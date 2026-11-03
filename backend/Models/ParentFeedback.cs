namespace GrawiaaApp.API.Models
{
    public class ParentFeedback
    {
        public int Id { get; set; }
        public int DailyLogEntryId { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public virtual DailyLogEntry DailyLogEntry { get; set; } = null!;
    }
}

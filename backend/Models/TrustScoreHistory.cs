namespace GrawiaaApp.API.Models
{
    public class TrustScoreHistory
    {
        public int Id { get; set; }
        public int ScoreAtTimestamp { get; set; }
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
        public string Reason { get; set; } = string.Empty;
        public int ChildId { get; set; }
        public virtual ApplicationUser Child { get; set; } = null!;
    }
}

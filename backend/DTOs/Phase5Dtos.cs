namespace GrawiaaApp.API.DTOs
{
    public class AlertResultDto
    {
        public int ChildId { get; set; }
        public string ChildName { get; set; } = string.Empty;
        public string PillarKey { get; set; } = string.Empty;
        public string PillarName { get; set; } = string.Empty;
        public string PillarEmoji { get; set; } = string.Empty;
        public double PillarScore { get; set; }
        public string Severity { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Suggestion { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
    }

    public class AlertSummaryDto
    {
        public int ChildId { get; set; }
        public int TotalAlerts { get; set; }
        public int CriticalCount { get; set; }
        public int MediumCount { get; set; }
        public int LowCount { get; set; }
        public List<AlertResultDto> Alerts { get; set; } = new();
        public string OverallStatus { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
    }

    public class MotivationResultDto
    {
        public int ChildId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Basis { get; set; } = string.Empty;
        public string PillarKey { get; set; } = string.Empty;
        public string PillarName { get; set; } = string.Empty;
        public double PillarScore { get; set; }
        public double? ImprovementDelta { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public class TipResultDto
    {
        public string Tip { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string LowestPillar { get; set; } = string.Empty;
        public double PillarScore { get; set; }
        public string CharacterName { get; set; } = string.Empty;
    }
}
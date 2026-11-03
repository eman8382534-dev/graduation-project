// ============================================================
//  AiBadgeSuggestion.cs
//
//  Stores badge suggestions pushed by the external AI/NLP team.
//  The AI team analyses the child's journal entries, sentiment,
//  and pillar scores — then pushes targeted badge suggestions
//  to motivate the child to improve a specific area.
//
//  Lifecycle:
//    1. AI team POSTs to /api/ai/badge-suggestions
//    2. Record saved here with IsShown = false
//    3. Child opens app → GET /api/Children/suggested-badges
//       marks IsShown = true
//    4. Child dismisses → IsDismissed = true
//    5. Expired suggestions (ExpiresAt < now) are filtered out
// ============================================================

namespace GrawiaaApp.API.Models
{
    public class AiBadgeSuggestion
    {
        public int    Id        { get; set; }
        public int    ChildId   { get; set; }

        /// <summary>Which pillar this suggestion targets.
        /// Values: spiritual | social | academic | home | physicalHealth |
        ///         physicalActivity | creativity | mental</summary>
        public string Pillar    { get; set; } = string.Empty;

        /// <summary>Display name of the badge being suggested. e.g. "Spiritual Silver"</summary>
        public string BadgeName { get; set; } = string.Empty;

        /// <summary>Emoji icon for the badge shown in the UI.</summary>
        public string BadgeIcon { get; set; } = "🏅";

        /// <summary>Motivational message shown to the child.</summary>
        public string Message   { get; set; } = string.Empty;

        /// <summary>Internal reason from the AI team (shown on parent dashboard only).</summary>
        public string Reason    { get; set; } = string.Empty;

        /// <summary>AI-assessed urgency: low | medium | high</summary>
        public string Urgency   { get; set; } = "medium";

        /// <summary>Current child % for this pillar at time of suggestion.</summary>
        public double CurrentPct  { get; set; }

        /// <summary>Target % needed to earn the badge.</summary>
        public double TargetPct   { get; set; }

        /// <summary>True once the child has seen this suggestion.</summary>
        public bool   IsShown     { get; set; } = false;

        /// <summary>True if the child dismissed this suggestion.</summary>
        public bool   IsDismissed { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Suggestion expires after 7 days by default.</summary>
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);

        // Navigation
        public virtual ChildProfile Child { get; set; } = null!;
    }
}

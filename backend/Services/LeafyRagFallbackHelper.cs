// Shared fallback logic for LeafyRagSyncService — pillar/trait/risk matching.

using GrawiaaApp.API.Data;
using GrawiaaApp.API.Models;
using Microsoft.EntityFrameworkCore;

namespace GrawiaaApp.API.Services
{
    internal static class LeafyRagFallbackHelper
    {
        private static readonly Dictionary<string, string> PillarToSide = new(StringComparer.OrdinalIgnoreCase)
        {
            { nameof(ChildProfile.FinalSpiritualConnection_Pct), LeafyTipSide.Spiritual },
            { nameof(ChildProfile.FinalSocialInteraction_Pct),     LeafyTipSide.Social },
            { nameof(ChildProfile.FinalAcademicProgress_Pct),      LeafyTipSide.Academic },
            { nameof(ChildProfile.FinalHomeResponsibility_Pct),    LeafyTipSide.Environment },
            { nameof(ChildProfile.FinalPhysicalHealth_Pct),        LeafyTipSide.Physical },
            { nameof(ChildProfile.FinalPhysicalActivity_Pct),      LeafyTipSide.Sport },
            { nameof(ChildProfile.FinalCreativityEngagement_Pct),  LeafyTipSide.Creativity },
            { nameof(ChildProfile.FinalMentalWellbeing_Pct),       LeafyTipSide.Mental },
        };

        public static bool HasAiLeafyRecommendation(ChildProfile child) =>
            child.RecommendedLeafyTipId.HasValue
            || child.RecommendedLeafyTipSetAt.HasValue
            || child.LeafyTextSetAt.HasValue
            || !string.IsNullOrWhiteSpace(child.LeafyTipText)
            || !string.IsNullOrWhiteSpace(child.LeafyMessageText)
            || !string.IsNullOrWhiteSpace(child.LeafyAlertText);

        public static async Task ApplyFallbackAsync(ApplicationDbContext context, ChildProfile child, CancellationToken ct)
        {
            var pillars = GetFinalPillarScores(child);
            var weakest = pillars.OrderBy(p => p.Value).First();
            var strongest = pillars.OrderByDescending(p => p.Value).First();
            var targetSide = PillarToSide[weakest.Key];

            bool hasRisk = child.HasActiveEmotionSafetyAlert
                || child.FinalMentalWellbeing_Pct < 30;

            var latestLog = await context.DailyLogEntries
                .Where(l => l.ChildId == child.UserId && l.IsJournalSaved)
                .OrderByDescending(l => l.LogDate)
                .Select(l => new { l.AnomalyFlag, l.ScoreVariance, l.SentimentScore })
                .FirstOrDefaultAsync(ct);

            if (latestLog != null)
                hasRisk = hasRisk || latestLog.AnomalyFlag || latestLog.ScoreVariance >= 60
                    || (latestLog.SentimentScore.HasValue && latestLog.SentimentScore < -0.3);

            var allTips = await context.LeafyTips.ToListAsync(ct);

            var tipRow = PickBest(allTips, targetSide, weakest.Value, LeafyTipPurpose.Tip);
            var motivationRow = PickBest(allTips, PillarToSide[strongest.Key], strongest.Value, LeafyTipPurpose.Motivation);
            var alertSide = hasRisk ? targetSide : PillarToSide[weakest.Key];
            var alertRow = hasRisk
                ? PickBest(allTips, alertSide, weakest.Value, LeafyTipPurpose.Alert, preferWarning: true)
                : null;

            if (tipRow != null)
            {
                child.RecommendedLeafyTipId = tipRow.Id;
                child.Tip = tipRow.Tip ?? tipRow.Content;
            }

            if (motivationRow != null)
                child.Motivation = motivationRow.Motivation ?? motivationRow.Content;

            if (alertRow != null)
                child.Alert = alertRow.Alert ?? alertRow.Content;

            child.LeafySynced = true;
        }

        private static Dictionary<string, double> GetFinalPillarScores(ChildProfile p) => new()
        {
            { nameof(ChildProfile.FinalSpiritualConnection_Pct),  p.FinalSpiritualConnection_Pct  > 0 ? p.FinalSpiritualConnection_Pct  : p.SpiritualConnection_Pct },
            { nameof(ChildProfile.FinalSocialInteraction_Pct),    p.FinalSocialInteraction_Pct    > 0 ? p.FinalSocialInteraction_Pct    : p.SocialInteraction_Pct },
            { nameof(ChildProfile.FinalAcademicProgress_Pct),     p.FinalAcademicProgress_Pct     > 0 ? p.FinalAcademicProgress_Pct     : p.AcademicProgress_Pct },
            { nameof(ChildProfile.FinalHomeResponsibility_Pct),   p.FinalHomeResponsibility_Pct   > 0 ? p.FinalHomeResponsibility_Pct   : p.HomeResponsibility_Pct },
            { nameof(ChildProfile.FinalPhysicalHealth_Pct),       p.FinalPhysicalHealth_Pct       > 0 ? p.FinalPhysicalHealth_Pct       : p.PhysicalHealth_Pct },
            { nameof(ChildProfile.FinalPhysicalActivity_Pct),     p.FinalPhysicalActivity_Pct     > 0 ? p.FinalPhysicalActivity_Pct     : p.PhysicalActivity_Pct },
            { nameof(ChildProfile.FinalCreativityEngagement_Pct), p.FinalCreativityEngagement_Pct > 0 ? p.FinalCreativityEngagement_Pct : p.CreativityEngagement_Pct },
            { nameof(ChildProfile.FinalMentalWellbeing_Pct),      p.FinalMentalWellbeing_Pct      > 0 ? p.FinalMentalWellbeing_Pct      : p.MentalWellbeing_Pct },
        };

        private static LeafyTip? PickBest(
            List<LeafyTip> catalog,
            string side,
            double pillarScore,
            string purpose,
            bool preferWarning = false)
        {
            var candidates = catalog
                .Where(t => t.Side.Equals(side, StringComparison.OrdinalIgnoreCase)
                         && t.Purpose.Equals(purpose, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!candidates.Any())
                candidates = catalog.Where(t => t.Purpose.Equals(purpose, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!candidates.Any())
                return null;

            string preferredType = preferWarning || pillarScore < 20 ? LeafyTipType.Warning
                                 : pillarScore < 60 ? LeafyTipType.Encouragement
                                 : LeafyTipType.Achievement;

            var typed = candidates
                .Where(t => t.Type.Equals(preferredType, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var pool = typed.Any() ? typed : candidates;
            int index = DateTime.UtcNow.DayOfYear % pool.Count;
            return pool[index];
        }
    }
}

using GrawiaaApp.API.DTOs;
using GrawiaaApp.API.Models;

namespace GrawiaaApp.API.Services
{
    public static class PillarInsightsHelper
    {
        private static readonly (string Key, string Label, string Emoji, Func<ChildProfile, double> GetScore)[] FinalPillars =
        {
            ("spiritual", "Religious", "🕌", p => p.FinalSpiritualConnection_Pct > 0 ? p.FinalSpiritualConnection_Pct : p.SpiritualConnection_Pct),
            ("social", "Social", "🧑‍🤝‍🧑", p => p.FinalSocialInteraction_Pct > 0 ? p.FinalSocialInteraction_Pct : p.SocialInteraction_Pct),
            ("academic", "Academic", "📚", p => p.FinalAcademicProgress_Pct > 0 ? p.FinalAcademicProgress_Pct : p.AcademicProgress_Pct),
            ("environment", "Home & Chores", "🏠", p => p.FinalHomeResponsibility_Pct > 0 ? p.FinalHomeResponsibility_Pct : p.HomeResponsibility_Pct),
            ("nutrition", "Nutrition", "🥗", p => p.FinalPhysicalHealth_Pct > 0 ? p.FinalPhysicalHealth_Pct : p.PhysicalHealth_Pct),
            ("sport", "Physical Activity", "🏃", p => p.FinalPhysicalActivity_Pct > 0 ? p.FinalPhysicalActivity_Pct : p.PhysicalActivity_Pct),
            ("hobbies", "Hobbies", "🎨", p => p.FinalCreativityEngagement_Pct > 0 ? p.FinalCreativityEngagement_Pct : p.CreativityEngagement_Pct),
            ("mental", "Mental & Emotional", "🧠", p => p.FinalMentalWellbeing_Pct > 0 ? p.FinalMentalWellbeing_Pct : p.MentalWellbeing_Pct),
        };

        public static StrengthsWeaknessesDto BuildStrengthsWeaknesses(ChildProfile profile)
        {
            var ranked = FinalPillars
                .Select(p => new PillarScoreItemDto
                {
                    Key = p.Key,
                    Label = p.Label,
                    Emoji = p.Emoji,
                    Score = Math.Round(p.GetScore(profile), 1)
                })
                .OrderByDescending(p => p.Score)
                .ToList();

            return new StrengthsWeaknessesDto
            {
                Strengths = ranked.Take(3).ToList(),
                Weaknesses = ranked.OrderBy(p => p.Score).Take(3).ToList()
            };
        }

        public static void UpdatePastIllnessSummary(ChildProfile profile, DailyLogEntry entry)
        {
            if (!entry.WasIllToday || string.IsNullOrWhiteSpace(entry.IllnessType))
                return;

            var existing = (profile.PastIllnessSummary ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var type = entry.IllnessType.Trim();
            if (!existing.Contains(type, StringComparer.OrdinalIgnoreCase))
                existing.Add(type);

            profile.PastIllnessSummary = string.Join(", ", existing);

            if (!string.IsNullOrWhiteSpace(entry.IllnessAvoidanceTip))
                profile.IllnessAvoidanceTip = entry.IllnessAvoidanceTip;
        }
    }
}

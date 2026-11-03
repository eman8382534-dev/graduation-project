// ============================================================
//  AlertGenerationService.cs — Leafy AI System (Phase 4)
//
//  Parent-facing alert generation.
//  Triggered when ANY pillar drops below severity thresholds.
//
//  Severity Classification:
//    Critical : score < 20
//    Medium   : score 20–39
//    Low      : score 40–54
//    (No alert when all pillars >= 55)
// ============================================================

using GrawiaaApp.API.Data;
using GrawiaaApp.API.DTOs;
using GrawiaaApp.API.Models;
using Microsoft.EntityFrameworkCore;

namespace GrawiaaApp.API.Services
{
    public interface IAlertGenerationService
    {
        Task<List<AlertResultDto>> GenerateAlertsAsync(int childId);
        Task<AlertSummaryDto> GenerateAlertSummaryAsync(int childId);
    }

    public class AlertGenerationService : IAlertGenerationService
    {
        private const double CriticalThreshold = 20.0;
        private const double MediumThreshold = 40.0;
        private const double LowThreshold = 55.0;

        private readonly ApplicationDbContext _context;

        private static readonly Dictionary<string, string> PillarDisplayNames = new()
        {
            { "SpiritualConnection_Pct",  "Spiritual Connection"   },
            { "SocialInteraction_Pct",    "Social Interaction"     },
            { "AcademicProgress_Pct",     "Academic Progress"      },
            { "HomeResponsibility_Pct",   "Home Responsibility"    },
            { "PhysicalHealth_Pct",       "Physical Health"        },
            { "PhysicalActivity_Pct",     "Physical Activity"      },
            { "CreativityEngagement_Pct", "Creativity Engagement"  },
            { "MentalWellbeing_Pct",      "Mental Wellbeing"       },
        };

        private static readonly Dictionary<string, string> PillarEmojis = new()
        {
            { "SpiritualConnection_Pct",  "🕌" },
            { "SocialInteraction_Pct",    "🤝" },
            { "AcademicProgress_Pct",     "📚" },
            { "HomeResponsibility_Pct",   "🏠" },
            { "PhysicalHealth_Pct",       "🥗" },
            { "PhysicalActivity_Pct",     "🏃" },
            { "CreativityEngagement_Pct", "🎨" },
            { "MentalWellbeing_Pct",      "💆" },
        };

        public AlertGenerationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<AlertResultDto>> GenerateAlertsAsync(int childId)
        {
            var profile = await _context.ChildProfiles
                .AsNoTracking()
                .Include(cp => cp.User)
                .FirstOrDefaultAsync(cp => cp.UserId == childId);

            if (profile == null) return new List<AlertResultDto>();

            var pillars = GetPillarScores(profile);
            var alerts = new List<AlertResultDto>();

            foreach (var (pillarKey, score) in pillars.OrderBy(p => p.Value))
            {
                var severity = ClassifySeverity(score);
                if (severity == AlertSeverity.None) continue;

                var displayName = PillarDisplayNames[pillarKey];
                var emoji = PillarEmojis[pillarKey];
                var childName = profile.User?.FullName ?? "Your child";

                alerts.Add(new AlertResultDto
                {
                    ChildId = childId,
                    ChildName = childName,
                    PillarKey = pillarKey,
                    PillarName = displayName,
                    PillarEmoji = emoji,
                    PillarScore = Math.Round(score, 1),
                    Severity = severity.ToString(),
                    Reason = BuildReason(childName, displayName, emoji, score, severity),
                    Suggestion = BuildSuggestion(pillarKey, severity),
                    GeneratedAt = DateTime.UtcNow
                });
            }

            return alerts;
        }

        public async Task<AlertSummaryDto> GenerateAlertSummaryAsync(int childId)
        {
            var alerts = await GenerateAlertsAsync(childId);

            return new AlertSummaryDto
            {
                ChildId = childId,
                TotalAlerts = alerts.Count,
                CriticalCount = alerts.Count(a => a.Severity == "Critical"),
                MediumCount = alerts.Count(a => a.Severity == "Medium"),
                LowCount = alerts.Count(a => a.Severity == "Low"),
                Alerts = alerts,
                OverallStatus = DetermineOverallStatus(alerts),
                GeneratedAt = DateTime.UtcNow
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────

        // FIX (Phase 13 hybrid audit): this used to read the pre-hybrid
        // structured-only Xxx_Pct columns, so parent alerts were generated
        // from stale data while the Weekly Summary dashboard (and every
        // other downstream consumer) already reads the FinalXxx_Pct hybrid
        // columns (70% structured + 30% NLP). Switched to Final* to match
        // the "all downstream systems use Final Pillars" rule — see
        // ChildProfile.cs's Final Hybrid Pillars block. Dictionary keys are
        // left as the short names since PillarDisplayNames/PillarEmojis and
        // the DTO's PillarKey field are keyed on those, not on "Final...".
        private static Dictionary<string, double> GetPillarScores(ChildProfile p) => new()
        {
            { "SpiritualConnection_Pct",  p.FinalSpiritualConnection_Pct  },
            { "SocialInteraction_Pct",    p.FinalSocialInteraction_Pct    },
            { "AcademicProgress_Pct",     p.FinalAcademicProgress_Pct     },
            { "HomeResponsibility_Pct",   p.FinalHomeResponsibility_Pct   },
            { "PhysicalHealth_Pct",       p.FinalPhysicalHealth_Pct       },
            { "PhysicalActivity_Pct",     p.FinalPhysicalActivity_Pct     },
            { "CreativityEngagement_Pct", p.FinalCreativityEngagement_Pct },
            { "MentalWellbeing_Pct",      p.FinalMentalWellbeing_Pct      },
        };

        private static AlertSeverity ClassifySeverity(double score) =>
            score < CriticalThreshold ? AlertSeverity.Critical :
            score < MediumThreshold ? AlertSeverity.Medium :
            score < LowThreshold ? AlertSeverity.Low :
                                        AlertSeverity.None;

        private static string BuildReason(string childName, string displayName, string emoji,
                                          double score, AlertSeverity severity)
        {
            return severity switch
            {
                AlertSeverity.Critical =>
                    $"🔴 {emoji} {childName}'s {displayName} is critically low at {score:F1}%. Immediate attention is recommended.",
                AlertSeverity.Medium =>
                    $"🟡 {emoji} {childName}'s {displayName} has dropped to {score:F1}%. Consider checking in this week.",
                AlertSeverity.Low =>
                    $"🔵 {emoji} {childName}'s {displayName} is slightly below target at {score:F1}%. A gentle nudge may help.",
                _ => string.Empty
            };
        }

        private static string BuildSuggestion(string pillarKey, AlertSeverity severity)
        {
            var suggestions = new Dictionary<string, string[]>
            {
                { "SpiritualConnection_Pct",  new[] { "Encourage daily prayer or reflection time.", "Read a short story with a moral lesson together.", "Join a community spiritual activity." } },
                { "SocialInteraction_Pct",    new[] { "Arrange a playdate or family gathering.", "Encourage group activities or team sports.", "Ask your child about their friendships." } },
                { "AcademicProgress_Pct",     new[] { "Set a daily 30-minute focused study session.", "Review completed homework together.", "Consider a tutor for challenging subjects." } },
                { "HomeResponsibility_Pct",   new[] { "Assign a simple daily chore with a reward.", "Create a chore chart together.", "Praise efforts on household contributions." } },
                { "PhysicalHealth_Pct",       new[] { "Review daily meals for balanced nutrition.", "Ensure adequate water intake (6–8 cups).", "Limit processed snacks and increase fruits/vegetables." } },
                { "PhysicalActivity_Pct",     new[] { "Schedule 30 minutes of outdoor activity daily.", "Try a family walk or bike ride.", "Enroll in a sport or dance class." } },
                { "CreativityEngagement_Pct", new[] { "Dedicate 20 minutes daily to a hobby.", "Try an art, music, or craft project together.", "Visit a library or museum for inspiration." } },
                { "MentalWellbeing_Pct",      new[] { "Check in emotionally with your child daily.", "Establish a calming bedtime routine.", "Consider speaking with a school counselor if mood persists." } },
            };

            if (!suggestions.TryGetValue(pillarKey, out var tips)) return "Spend quality time with your child this week.";

            int index = severity == AlertSeverity.Critical ? 2
                      : severity == AlertSeverity.Medium ? 1 : 0;

            return tips[Math.Min(index, tips.Length - 1)];
        }

        private static string DetermineOverallStatus(List<AlertResultDto> alerts)
        {
            if (!alerts.Any()) return "Healthy";
            if (alerts.Any(a => a.Severity == "Critical")) return "NeedsAttention";
            if (alerts.Any(a => a.Severity == "Medium")) return "Monitor";
            return "Watch";
        }
    }

    internal enum AlertSeverity { None, Low, Medium, Critical }
}
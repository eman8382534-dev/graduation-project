// ============================================================
//  MotivationGenerationService.cs — Leafy AI System (Phase 4)
//
//  Child-facing motivational message generation.
//  Based on:
//     1. Strongest pillar this week (highest score), OR
//     2. Highest improvement from previous week (if available)
//
//  All messages are POSITIVE REINFORCEMENT ONLY.
// ============================================================

using GrawiaaApp.API.Data;
using GrawiaaApp.API.DTOs;
using GrawiaaApp.API.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrawiaaApp.API.Services
{
    public interface IMotivationGenerationService
    {
        Task<MotivationResultDto> GenerateMotivationAsync(int childId);
    }

    public class MotivationGenerationService : IMotivationGenerationService
    {
        private readonly ApplicationDbContext _context;

        private static readonly Dictionary<string, string> PillarDisplayNames = new()
        {
            { "SpiritualConnection_Pct",  "Spiritual Connection"  },
            { "SocialInteraction_Pct",    "Social Interaction"    },
            { "AcademicProgress_Pct",     "Academic Progress"     },
            { "HomeResponsibility_Pct",   "Home Responsibility"   },
            { "PhysicalHealth_Pct",       "Physical Health"       },
            { "PhysicalActivity_Pct",     "Physical Activity"     },
            { "CreativityEngagement_Pct", "Creativity"            },
            { "MentalWellbeing_Pct",      "Mental Wellbeing"      },
        };

        // Positive reinforcement templates per pillar (keyed by pillar property name)
        private static readonly Dictionary<string, string[]> PillarMotivations = new()
        {
            { "SpiritualConnection_Pct", new[]
                {
                    "Your spiritual dedication shines so brightly! 🌅 Keep nourishing your soul — it's your greatest superpower.",
                    "You're a true light! 🌟 Your connection with your faith is growing stronger every day. Proud of you!",
                    "Amazing spiritual week! 🕌 Your commitment to good values is an inspiration. Keep it up, hero!"
                }
            },
            { "SocialInteraction_Pct", new[]
                {
                    "You're a friendship superstar! 🤝 Your kindness and social skills are making the world a better place.",
                    "Look at you spreading smiles! 😊 Your amazing social energy lifts everyone around you. Keep shining!",
                    "Your heart is as big as the sky! 💛 Being a great friend is one of the best things in life — and you rock at it!"
                }
            },
            { "AcademicProgress_Pct", new[]
                {
                    "Your brain is on fire this week! 🧠🔥 All that studying is paying off — you're unstoppable!",
                    "Top marks for effort! 📚⭐ Every page you read, every problem you solve brings you closer to your dreams.",
                    "You're a knowledge champion! 🏆 Your dedication to learning is truly impressive. The world needs curious minds like yours!"
                }
            },
            { "HomeResponsibility_Pct", new[]
                {
                    "Home hero alert! 🏠💪 Your family is lucky to have someone as responsible and caring as you.",
                    "You're making the home shine! ✨ Every chore you complete is a gift to your whole family. You're amazing!",
                    "Super responsible champion! 🌟 Taking care of your home shows what an incredible person you're becoming!"
                }
            },
            { "PhysicalHealth_Pct", new[]
                {
                    "You're fueling your body like a champion! 🥗💪 Great nutrition choices this week — your future self thanks you!",
                    "Healthy hero in the making! 🌿 Your food choices are building a strong, powerful body. Keep it up!",
                    "You're glowing with health! ✨ Every good meal is an investment in your amazing future. So proud of you!"
                }
            },
            { "PhysicalActivity_Pct", new[]
                {
                    "You're an absolute energy machine! 🏃⚡ Your physical activity this week has been outstanding — keep moving!",
                    "Sports star! 🏅 Your body is getting stronger every day. The effort you put in is incredible!",
                    "Active hero! 💥 You crushed your physical goals this week. Your dedication to staying active is truly inspiring!"
                }
            },
            { "CreativityEngagement_Pct", new[]
                {
                    "Your creativity is out of this world! 🎨🚀 You have a gift — keep creating, keep dreaming, keep inspiring!",
                    "You're an artist AND a hero! 🌈 Your creative spirit makes life more colorful for everyone around you.",
                    "Imagination champion! ✏️⭐ Your hobbies and creative work this week have been absolutely amazing. Keep exploring!"
                }
            },
            { "MentalWellbeing_Pct", new[]
                {
                    "Your emotional intelligence is incredible! 💆💛 Understanding your feelings is a real superpower — you've got this!",
                    "Inner peace master! 🧘 Your mental strength and positive mindset are truly inspiring. Keep taking care of your heart!",
                    "You're emotionally strong and wise! 🌟 The way you handle your feelings shows how mature and amazing you are!"
                }
            },
        };

        // Improvement-based motivations (when a pillar improved week-over-week)
        private static readonly string[] ImprovementMotivations =
        {
            "You're on a rocket ship! 🚀 Your {0} improved so much this week — that's REAL growth. Keep going, hero!",
            "Massive progress alert! 📈✨ Your {0} went up this week — proof that you're working hard and it's paying off!",
            "Level UP! 🎮⬆️ Your {0} made a huge jump this week. You should be so proud of yourself — we definitely are!",
            "The transformation is real! 💫 Your {0} improvement this week shows what you're made of. Incredible work!",
        };

        public MotivationGenerationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<MotivationResultDto> GenerateMotivationAsync(int childId)
        {
            var profile = await _context.ChildProfiles
                .AsNoTracking()
                .Include(cp => cp.User)
                .FirstOrDefaultAsync(cp => cp.UserId == childId);

            if (profile == null) return FallbackMotivation(childId);

            // AI Leafy RAG output (ChildProfile.LeafyMessageText / Motivation) takes
            // priority over backend-computed templates — presentation spec.
            var aiMessage = !string.IsNullOrWhiteSpace(profile.LeafyMessageText)
                ? profile.LeafyMessageText
                : profile.Motivation;
            if (!string.IsNullOrWhiteSpace(aiMessage))
            {
                return new MotivationResultDto
                {
                    ChildId = childId,
                    Message = aiMessage,
                    Basis = "AI",
                    PillarKey = "LeafyRAG",
                    PillarName = "Leafy RAG",
                    PillarScore = profile.FinalMeanScore > 0 ? profile.FinalMeanScore : profile.MeanScore,
                    ImprovementDelta = null,
                    GeneratedAt = profile.LeafyTextSetAt ?? DateTime.UtcNow
                };
            }

            var childName = profile.User?.FullName?.Split(' ').FirstOrDefault() ?? "Hero";
            var pillars = GetPillarScores(profile);

            // Strategy 1: Check for significant improvement vs. last week
            var improvement = await DetectBestImprovementAsync(childId, pillars);

            // التعديل هنا: استخدام الـ Pattern Matching للتحقق من الـ Struct Nullable بأمان واختصار
            if (improvement is PillarImprovement actualImprovement && actualImprovement.Delta >= 10.0)
            {
                return BuildImprovementMotivation(childId, childName, actualImprovement);
            }

            // Strategy 2: Celebrate the strongest pillar
            var strongest = pillars.OrderByDescending(p => p.Value).First();
            return BuildStrengthMotivation(childId, childName, strongest.Key, strongest.Value);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static Dictionary<string, double> GetPillarScores(ChildProfile p) => new()
        {
            { "SpiritualConnection_Pct",  p.FinalSpiritualConnection_Pct  > 0 ? p.FinalSpiritualConnection_Pct  : p.SpiritualConnection_Pct  },
            { "SocialInteraction_Pct",    p.FinalSocialInteraction_Pct    > 0 ? p.FinalSocialInteraction_Pct    : p.SocialInteraction_Pct    },
            { "AcademicProgress_Pct",     p.FinalAcademicProgress_Pct     > 0 ? p.FinalAcademicProgress_Pct     : p.AcademicProgress_Pct     },
            { "HomeResponsibility_Pct",   p.FinalHomeResponsibility_Pct   > 0 ? p.FinalHomeResponsibility_Pct   : p.HomeResponsibility_Pct   },
            { "PhysicalHealth_Pct",       p.FinalPhysicalHealth_Pct       > 0 ? p.FinalPhysicalHealth_Pct       : p.PhysicalHealth_Pct       },
            { "PhysicalActivity_Pct",     p.FinalPhysicalActivity_Pct     > 0 ? p.FinalPhysicalActivity_Pct     : p.PhysicalActivity_Pct     },
            { "CreativityEngagement_Pct", p.FinalCreativityEngagement_Pct > 0 ? p.FinalCreativityEngagement_Pct : p.CreativityEngagement_Pct },
            { "MentalWellbeing_Pct",      p.FinalMentalWellbeing_Pct      > 0 ? p.FinalMentalWellbeing_Pct      : p.MentalWellbeing_Pct      },
        };

        /// <summary>
        /// Computes per-pillar weekly averages for the PREVIOUS week and finds
        /// the pillar with the greatest positive delta vs. current scores.
        /// Returns null if insufficient historical data exists.
        /// </summary>
        private async Task<PillarImprovement?> DetectBestImprovementAsync(
            int childId,
            Dictionary<string, double> currentScores)
        {
            var twoWeeksAgo = DateTime.UtcNow.Date.AddDays(-14);
            var oneWeekAgo = DateTime.UtcNow.Date.AddDays(-7);

            var prevLogs = await _context.DailyLogEntries
                .Where(l => l.ChildId == childId
                         && l.LogDate.Date >= twoWeeksAgo
                         && l.LogDate.Date < oneWeekAgo)
                .ToListAsync();

            if (!prevLogs.Any()) return null;

            // Previous week averages from computed daily pillars (same source as profile aggregate)
            var prevAverages = new Dictionary<string, double>
            {
                { "SpiritualConnection_Pct",  prevLogs.Average(l => l.DailySpiritualConnection_Pct)  },
                { "SocialInteraction_Pct",    prevLogs.Average(l => l.DailySocialInteraction_Pct)    },
                { "AcademicProgress_Pct",     prevLogs.Average(l => l.DailyAcademicProgress_Pct)     },
                { "HomeResponsibility_Pct",   prevLogs.Average(l => l.DailyHomeResponsibility_Pct)   },
                { "PhysicalHealth_Pct",       prevLogs.Average(l => l.DailyPhysicalHealth_Pct)       },
                { "PhysicalActivity_Pct",     prevLogs.Average(l => l.DailyPhysicalActivity_Pct)     },
                { "CreativityEngagement_Pct", prevLogs.Average(l => l.DailyCreativityEngagement_Pct) },
                { "MentalWellbeing_Pct",      prevLogs.Average(l => l.DailyMentalWellbeing_Pct)      },
            };

            PillarImprovement? best = null;
            foreach (var key in currentScores.Keys)
            {
                if (!prevAverages.TryGetValue(key, out var prevScore)) continue;
                double delta = currentScores[key] - prevScore;
                if (delta <= 0) continue;

                // التعديل هنا: التعامل مع تداخل قيم الـ Struct Nullable أثناء المقارنة بشكل سليم
                if (best == null || delta > best.Value.Delta)
                    best = new PillarImprovement(key, prevScore, currentScores[key], delta);
            }

            return best;
        }

        private MotivationResultDto BuildImprovementMotivation(
            int childId, string childName,
            PillarImprovement improvement)
        {
            var displayName = PillarDisplayNames[improvement.PillarKey];
            int templateIndex = DateTime.UtcNow.DayOfYear % ImprovementMotivations.Length;
            var template = ImprovementMotivations[templateIndex];
            var message = string.Format(template, displayName);

            return new MotivationResultDto
            {
                ChildId = childId,
                Message = $"Hey {childName}! {message}",
                Basis = "Improvement",
                PillarKey = improvement.PillarKey,
                PillarName = displayName,
                PillarScore = Math.Round(improvement.CurrentScore, 1),
                ImprovementDelta = Math.Round(improvement.Delta, 1),
                GeneratedAt = DateTime.UtcNow
            };
        }

        private MotivationResultDto BuildStrengthMotivation(
            int childId, string childName,
            string pillarKey, double score)
        {
            var displayName = PillarDisplayNames[pillarKey];

            if (!PillarMotivations.TryGetValue(pillarKey, out var templates))
                templates = new[] { $"You are amazing, {childName}! Keep up the wonderful work! 🌟" };

            int index = DateTime.UtcNow.DayOfYear % templates.Length;
            var message = $"Hey {childName}! {templates[index]}";

            return new MotivationResultDto
            {
                ChildId = childId,
                Message = message,
                Basis = "Strength",
                PillarKey = pillarKey,
                PillarName = displayName,
                PillarScore = Math.Round(score, 1),
                ImprovementDelta = null,
                GeneratedAt = DateTime.UtcNow
            };
        }

        private static MotivationResultDto FallbackMotivation(int childId) => new()
        {
            ChildId = childId,
            Message = "You are doing an amazing job! 🌟 Every step you take is progress. Keep going, hero!",
            Basis = "General",
            PillarKey = "General",
            PillarName = "Overall",
            PillarScore = 0,
            ImprovementDelta = null,
            GeneratedAt = DateTime.UtcNow
        };

        private record struct PillarImprovement(string PillarKey, double PreviousScore, double CurrentScore, double Delta);
    }
}
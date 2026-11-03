// ============================================================
//  TipGenerationService.cs — Leafy AI System (Phase 4)
//
//  Child-facing tip generation.
//  Selects the best matching LeafyTip from the database based
//  on the child's LOWEST pillar score (not random selection).
//
//  ⚠ AI TEAM NOTE — Structured pillars used here (backend fallback path):
//  This service is the BACKEND FALLBACK path for Leafy tip generation.
//  It reads STRUCTURED pillar scores (SpiritualConnection_Pct etc.) —
//  NOT the Final Hybrid pillars (FinalSpiritualConnection_Pct etc.).
//  The AI team's daily Leafy RAG job writes LeafyTipText / LeafyAlertText
//  directly to ChildProfile (via POST /api/ai/leafy-recommendation);
//  the frontend prioritises those AI-generated texts over this fallback.
//  When the RAG texts are null (AI hasn't run or hasn't written yet),
//  ChildController.GetLeafyRecommendation() falls back to this service,
//  which selects from the admin-curated LeafyTip catalog using Structured
//  pillars. This means the fallback UX may differ slightly from the AI RAG
//  output (Hybrid vs Structured pillar inputs) — accepted per design, but
//  noted here so the AI team is aware of the pillar source difference.
//
//  Pillar → LeafyTip.Side mapping:
//    SpiritualConnection_Pct   → "spiritual"
//    SocialInteraction_Pct     → "social"
//    AcademicProgress_Pct      → "academic"
//    HomeResponsibility_Pct    → "environment"
//    PhysicalHealth_Pct        → "physical"
//    PhysicalActivity_Pct      → "sport"
//    CreativityEngagement_Pct  → "creativity"
//    MentalWellbeing_Pct       → "mental"
// ============================================================

using GrawiaaApp.API.Data;
using GrawiaaApp.API.DTOs;
using GrawiaaApp.API.Models;
using Microsoft.EntityFrameworkCore;

namespace GrawiaaApp.API.Services
{
    public interface ITipGenerationService
    {
        Task<TipResultDto> GenerateTipAsync(int childId);
    }

    public class TipGenerationService : ITipGenerationService
    {
        private readonly ApplicationDbContext _context;

        // Maps canonical pillar property names to LeafyTip.Side values
        private static readonly Dictionary<string, string> PillarToSide = new(StringComparer.OrdinalIgnoreCase)
        {
            { "SpiritualConnection_Pct", "spiritual"   },
            { "SocialInteraction_Pct",   "social"      },
            { "AcademicProgress_Pct",    "academic"    },
            { "HomeResponsibility_Pct",  "environment" },
            { "PhysicalHealth_Pct",      "physical"    },
            { "PhysicalActivity_Pct",    "sport"       },
            { "CreativityEngagement_Pct","creativity"  },
            { "MentalWellbeing_Pct",     "mental"      },
        };

        public TipGenerationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<TipResultDto> GenerateTipAsync(int childId)
        {
            var profile = await _context.ChildProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(cp => cp.UserId == childId);

            if (profile == null)
                return FallbackTip();

            // Build ordered list: pillar name → score (ascending = weakest first)
            var pillars = GetPillarScores(profile);
            var weakest = pillars.OrderBy(p => p.Value).First();
            var targetSide = PillarToSide[weakest.Key];

            // Fetch all tips for the target side from the database
            var candidateTips = await _context.LeafyTips
                .Where(t => t.Side.ToLower() == targetSide)
                .ToListAsync();

            if (!candidateTips.Any())
            {
                // Fall back to any tip in the database
                candidateTips = await _context.LeafyTips.ToListAsync();
            }

            if (!candidateTips.Any())
                return FallbackTip();

            // Intelligent selection: prefer "encouragement" type when score is 20-60,
            // prefer "warning" type when score is < 20, prefer "achievement" when > 60
            var selectedTip = SelectBestTip(candidateTips, weakest.Value);

            return new TipResultDto
            {
                Tip          = selectedTip.Content,
                Side         = selectedTip.Side,
                Type         = selectedTip.Type,
                LowestPillar = weakest.Key,
                PillarScore  = Math.Round(weakest.Value, 1),
                CharacterName = "Leafy"
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────

        // NOTE: Uses STRUCTURED pillar scores (not Final Hybrid).
        // This is intentional for the backend fallback path — see class header.
        private static Dictionary<string, double> GetPillarScores(ChildProfile p) => new()
        {
            { "SpiritualConnection_Pct",  p.SpiritualConnection_Pct  },
            { "SocialInteraction_Pct",    p.SocialInteraction_Pct    },
            { "AcademicProgress_Pct",     p.AcademicProgress_Pct     },
            { "HomeResponsibility_Pct",   p.HomeResponsibility_Pct   },
            { "PhysicalHealth_Pct",       p.PhysicalHealth_Pct       },
            { "PhysicalActivity_Pct",     p.PhysicalActivity_Pct     },
            { "CreativityEngagement_Pct", p.CreativityEngagement_Pct },
            { "MentalWellbeing_Pct",      p.MentalWellbeing_Pct      },
        };

        private static LeafyTip SelectBestTip(List<LeafyTip> candidates, double pillarScore)
        {
            string preferredType = pillarScore < 20.0  ? "warning"
                                 : pillarScore < 60.0  ? "encouragement"
                                 :                       "achievement";

            var preferred = candidates
                .Where(t => t.Type.Equals(preferredType, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (preferred.Any())
            {
                // Deterministic but varied: rotate by day-of-year so child sees
                // different tips across days without true randomness per request.
                int index = DateTime.UtcNow.DayOfYear % preferred.Count;
                return preferred[index];
            }

            // If no preferred type, return any candidate rotated by day
            int fallbackIndex = DateTime.UtcNow.DayOfYear % candidates.Count;
            return candidates[fallbackIndex];
        }

        private static TipResultDto FallbackTip() => new TipResultDto
        {
            Tip           = "Drink more water today! 💧 Small habits build big strength.",
            Side          = "physical",
            Type          = "encouragement",
            LowestPillar  = "Unknown",
            PillarScore   = 0,
            CharacterName = "Leafy"
        };
    }
}

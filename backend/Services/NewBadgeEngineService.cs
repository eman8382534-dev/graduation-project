// ============================================================
//  NewBadgeEngineService.cs — NEW Badge System (Phase 10)
//
//  REPLACES: BadgeLogic.cs (IScoreObserver threshold-based system)
//
//  Rules:
//  1. Badges are ONLY unlocked via completed challenges.
//  2. A badge is unlocked when ALL its challenges are completed.
//  3. This service is called by NewProgressionService after
//     challenge progress is updated.
//  4. Never writes to ChildBadges in any other code path.
// ============================================================
using GrawiaaApp.API.Data;
using GrawiaaApp.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrawiaaApp.API.Services
{
    public interface INewBadgeEngineService
    {
        /// <summary>
        /// Check all badge-challenge completions for the child and unlock
        /// any badges whose ALL challenges are now complete.
        /// Returns list of newly unlocked badge IDs.
        /// </summary>
        Task<List<int>> EvaluateAndUnlockBadgesAsync(int childId);
    }

    public class NewBadgeEngineService : INewBadgeEngineService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NewBadgeEngineService> _logger;

        public NewBadgeEngineService(
            ApplicationDbContext context,
            ILogger<NewBadgeEngineService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<int>> EvaluateAndUnlockBadgesAsync(int childId)
        {
            var newlyUnlocked = new List<int>();

            // Get all badges that have at least one challenge defined
            var badgesWithChallenges = await _context.Badges
                .Include(b => b.Challenges)
                .Where(b => b.Challenges.Any())
                .ToListAsync();

            if (!badgesWithChallenges.Any()) return newlyUnlocked;

            // Get badges this child already has (to skip re-evaluation)
            var alreadyEarnedBadgeIdsList = await _context.ChildBadges
                .Where(cb => cb.ChildId == childId && cb.IsActive)
                .Select(cb => cb.BadgeId)
                .ToListAsync(); // تعديل هنا

            var alreadyEarnedBadgeIds = alreadyEarnedBadgeIdsList.ToHashSet();

            // Get this child's completed challenges
            var completedChallengeIdsList = await _context.ChildChallengeProgresses
                .Where(cp => cp.ChildId == childId && cp.IsCompleted)
                .Select(cp => cp.ChallengeId)
                .ToListAsync(); // تعديل هنا

            var completedChallengeIds = completedChallengeIdsList.ToHashSet();

            foreach (var badge in badgesWithChallenges)
            {
                // Skip if already earned
                if (alreadyEarnedBadgeIds.Contains(badge.Id)) continue;

                // Check if ALL challenges for this badge are completed
                bool allDone = badge.Challenges.All(c => completedChallengeIds.Contains(c.Id));

                if (allDone)
                {
                    _context.ChildBadges.Add(new ChildBadge
                    {
                        ChildId = childId,
                        BadgeId = badge.Id,
                        UnlockedAt = DateTime.UtcNow,
                        IsActive = true
                    });

                    newlyUnlocked.Add(badge.Id);
                    _logger.LogInformation(
                        "🏅 Badge unlocked via challenges: ChildId={ChildId} BadgeId={BadgeId} ({BadgeName})",
                        childId, badge.Id, badge.Name);
                }
            }

            if (newlyUnlocked.Any())
                await _context.SaveChangesAsync();

            return newlyUnlocked;
        }
    }
}
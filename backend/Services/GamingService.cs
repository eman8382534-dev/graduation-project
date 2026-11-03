// GamingService.cs — updates TrustScore and fires remaining score observers.
//
// PHASE 16: TrustScore is computed by TrustScoreEngine (MeanScore ×
// completion_rate × streak_bonus) unless the AI team has set
// TrustScoreSetAt (external authoritative value).
using GrawiaaApp.API.Data;
using GrawiaaApp.API.Interfaces;
using GrawiaaApp.API.Models;
using GrawiaaApp.API.Services;
using Microsoft.EntityFrameworkCore;

namespace GrawiaaApp.API.Logic
{
    public class GamingService : IGamingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEnumerable<IScoreObserver> _observers;
        private readonly ITrustScoreEngine _trustScoreEngine;

        public GamingService(
            ApplicationDbContext context,
            IEnumerable<IScoreObserver> observers,
            ITrustScoreEngine trustScoreEngine)
        {
            _context = context;
            _observers = observers;
            _trustScoreEngine = trustScoreEngine;
        }

        public async Task<double> UpdateScore(int userId, int points)
        {
            var childProfile = await _context.ChildProfiles
                .FirstOrDefaultAsync(cp => cp.UserId == userId);

            if (childProfile == null)
                return 0;

            if (points != 0)
            {
                if (!childProfile.TrustScoreSetAt.HasValue)
                    await _trustScoreEngine.TryRecalculateAndPersistAsync(userId, childProfile);

                _context.TrustScoreHistories.Add(new TrustScoreHistory
                {
                    ChildId           = userId,
                    ScoreAtTimestamp  = childProfile.TrustScore,
                    RecordedAt        = DateTime.UtcNow,
                    Reason            = points > 0 ? "إنجاز مهمة/صدق" : "سلوك سلبي/عدم صدق"
                });

                await _context.SaveChangesAsync();
            }

            foreach (var observer in _observers)
            {
                await observer.Execute(userId, childProfile.TrustScore);
            }

            return childProfile.TrustScore;
        }
    }
}

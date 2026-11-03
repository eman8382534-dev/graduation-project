// ============================================================
//  TrustScoreEngine.cs — Trust Score calculation (Phase 16)
//
//  Formula (backend fallback — runs only when the AI team has NOT
//  externally set TrustScore, indicated by TrustScoreSetAt = null):
//
//      TrustScore = gate_score × completion_rate × streak_bonus
//
//  Where:
//    gate_score       = ChildProfile.FinalMeanScore (70% Structured + 30% NLP)
//                       Falls back to MeanScore when FinalMeanScore = 0
//                       (AI NLP pipeline has not run yet for this child).
//    completion_rate  = saved journal days in trailing 30 / 30  (0–1)
//    streak_bonus     = 1.0 + min(0.5, currentStreak / 30)     (1.0–1.5)
//
//  Result is clamped to [0, 100] and stored as int on ChildProfile.
//  The AI team may write TrustScore + TrustScoreSetAt directly via SQL
//  or API — those values are NEVER overwritten by this engine.
// ============================================================

using GrawiaaApp.API.Data;
using GrawiaaApp.API.Models;
using Microsoft.EntityFrameworkCore;

namespace GrawiaaApp.API.Services
{
    public interface ITrustScoreEngine
    {
        /// <summary>
        /// Pure calculation — reusable by any caller.
        /// </summary>
        double Calculate(double meanScore, double completionRate, double streakBonus);

        /// <summary>
        /// Computes completion_rate and streak_bonus from DB metrics,
        /// applies the formula, and persists TrustScore when allowed.
        /// Returns the computed value (existing score if externally set).
        /// </summary>
        Task<int> TryRecalculateAndPersistAsync(int childId, ChildProfile? profile = null);
    }

    public sealed class TrustScoreEngine : ITrustScoreEngine
    {
        private readonly ApplicationDbContext _context;

        private const int AggregationWindowDays = 30;
        private const double MaxStreakBonus = 1.5;

        public TrustScoreEngine(ApplicationDbContext context)
        {
            _context = context;
        }

        public double Calculate(double meanScore, double completionRate, double streakBonus)
        {
            double rate = Math.Clamp(completionRate, 0, 1);
            double bonus = Math.Max(0, streakBonus);
            double raw = meanScore * rate * bonus;
            return Math.Clamp(Math.Round(raw, 2), 0, 100);
        }

        public async Task<int> TryRecalculateAndPersistAsync(int childId, ChildProfile? profile = null)
        {
            profile ??= await _context.ChildProfiles
                .FirstOrDefaultAsync(p => p.UserId == childId);

            if (profile is null)
                return 0;

            // AI / external write is authoritative — never overwrite.
            if (profile.TrustScoreSetAt.HasValue)
                return profile.TrustScore;

            var (completionRate, streakBonus) = await BuildMetricsAsync(childId);
            double gateScore = profile.FinalMeanScore > 0 ? profile.FinalMeanScore : profile.MeanScore;
            double computed = Calculate(gateScore, completionRate, streakBonus);
            profile.TrustScore = (int)Math.Round(computed);

            return profile.TrustScore;
        }

        /// <summary>
        /// Shared metric builder — also used by NewLevelEngineService for display.
        /// </summary>
        public static async Task<(double completionRate, double streakBonus, int currentStreak)> BuildMetricsAsync(
            ApplicationDbContext context, int childId)
        {
            var windowStart = DateTime.UtcNow.Date.AddDays(-(AggregationWindowDays - 1));

            var savedDays = await context.DailyLogEntries
                .Where(l => l.ChildId == childId
                         && l.IsJournalSaved
                         && l.LogDate.Date >= windowStart)
                .Select(l => l.LogDate.Date)
                .Distinct()
                .CountAsync();

            double completionRate = Math.Min(1.0, savedDays / (double)AggregationWindowDays);

            int streak = await ComputeCurrentStreakAsync(context, childId);
            double streakBonus = Math.Min(MaxStreakBonus, 1.0 + streak / 30.0);

            return (completionRate, streakBonus, streak);
        }

        private async Task<(double completionRate, double streakBonus)> BuildMetricsAsync(int childId)
        {
            var (rate, bonus, _) = await BuildMetricsAsync(_context, childId);
            return (rate, bonus);
        }

        private static async Task<int> ComputeCurrentStreakAsync(ApplicationDbContext context, int childId)
        {
            TimeZoneInfo egyptZone;
            try { egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
            catch { egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }

            var todayEgypt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptZone).Date;
            var cutoff = todayEgypt.AddDays(-365);

            var logDates = await context.DailyLogEntries
                .Where(l => l.ChildId == childId && l.IsJournalSaved && l.LogDate.Date >= cutoff)
                .Select(l => l.LogDate.Date)
                .Distinct()
                .ToListAsync();

            var logDateSet = logDates.ToHashSet();
            int streak = 0;
            var checkDay = todayEgypt;
            while (logDateSet.Contains(checkDay))
            {
                streak++;
                checkDay = checkDay.AddDays(-1);
                if (streak >= 365) break;
            }

            return streak;
        }
    }
}

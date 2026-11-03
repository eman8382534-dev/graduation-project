// ============================================================
//  IPillarCalculationService.cs — Leafy AI System
//
//  DI-friendly wrapper interface over the static
//  PillarCalculationEngine. Inject this into controllers and
//  services instead of calling the static class directly so
//  that the calculation can be mocked in unit tests.
// ============================================================

using System.Collections.Generic;
using System.Threading.Tasks;
using GrawiaaApp.API.Models;

namespace GrawiaaApp.API.Services
{
    public interface IPillarCalculationService
    {
        /// <summary>
        /// Computes 8 pillars from a single journal entry (inline / delta path).
        /// </summary>
        PillarResult CalculateFromEntry(DailyLogEntry entry);

        /// <summary>
        /// Computes 8 pillars as a duration-weighted weekly average.
        /// </summary>
        PillarResult CalculateFromWeek(IEnumerable<DailyLogEntry> entries);

        /// <summary>
        /// Applies a PillarResult onto a ChildProfile in-place.
        /// </summary>
        void Apply(ChildProfile profile, PillarResult result);
    }

    // ── Concrete scoped implementation ───────────────────────────────
    public sealed class PillarCalculationService : IPillarCalculationService
    {
        public PillarResult CalculateFromEntry(DailyLogEntry entry)
            => PillarCalculationEngine.FromSingleEntry(entry);

        public PillarResult CalculateFromWeek(IEnumerable<DailyLogEntry> entries)
            => PillarCalculationEngine.FromWeeklyEntries(entries);

        public void Apply(ChildProfile profile, PillarResult result)
            => PillarCalculationEngine.ApplyToProfile(profile, result);
    }
}

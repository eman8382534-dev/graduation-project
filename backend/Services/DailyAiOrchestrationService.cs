// ============================================================
//  DailyAiOrchestrationService.cs — Leafy AI System (Phase 12)
//
//  Daily scheduled job covering: Anomaly Detection, NLP Outputs,
//  Leafy Recommendation.
//
//  IMPORTANT — ORCHESTRATION / VALIDATION ONLY:
//  ─────────────────────────────────────────────────────────
//  This service NEVER computes a sentiment score, an anomaly
//  score, an NLP pillar value, or a Leafy recommendation. Those
//  are written exclusively by the external AI team via the
//  /api/ai/* endpoints. This service only:
//    1. Validates that yesterday's finalized journal entries
//       received AI annotation, and logs a clear, actionable
//       warning (with LogId/ChildId) for any that did not —
//       so ops/the AI team can follow up on a gap, instead of
//       the backend silently inventing a value.
//    2. Validates that every active child has a Leafy
//       recommendation pointer set. It does NOT generate one —
//       child-facing reads already fall back to the existing
//       backend-computed smart tip at request time
//       (see ChildController.GetLeafyRecommendation); this job
//       just surfaces, via logging, which children are still
//       waiting on the AI team's daily run.
//
//  Mirrors the existing DailyLogClosureService BackgroundService
//  pattern already used in this project (timer loop + DI scope
//  per tick) — no new scheduling dependency introduced.
// ============================================================

using GrawiaaApp.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GrawiaaApp.API.Services
{
    public class DailyAiOrchestrationService : BackgroundService
    {
        private readonly ILogger<DailyAiOrchestrationService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public DailyAiOrchestrationService(
            ILogger<DailyAiOrchestrationService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DailyAiOrchestrationService started at {Time}", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Presentation spec: nightly validation at 2 AM (Egypt local).
                    await Task.Delay(NightlyScheduleHelper.DelayUntilNextRun(), stoppingToken);
                    await RunDailyValidationAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Normal during shutdown.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in DailyAiOrchestrationService at {Time}", DateTime.UtcNow);
                }
            }
        }

        private async Task RunDailyValidationAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var yesterday = DateTime.UtcNow.Date.AddDays(-1);

            // ── 1. Anomaly Detection + NLP Outputs completeness check ──────
            var yesterdaysEntries = await context.DailyLogEntries
                .Where(l => l.LogDate.Date == yesterday && l.IsJournalSaved)
                .ToListAsync(cancellationToken);

            var missingAiAnnotation = yesterdaysEntries
                .Where(l => l.SentimentScore == null
                         && l.AnomalyScore == 0
                         && l.ScoreVariance == 0
                         && l.NlpPillarsCalculatedAt == null)
                .ToList();

            foreach (var log in missingAiAnnotation)
            {
                _logger.LogWarning(
                    "[AiOrchestration:Daily] Journal entry {LogId} for child {ChildId} (date {LogDate:yyyy-MM-dd}) " +
                    "has no AI anomaly/NLP annotation yet. The backend will NOT generate one — " +
                    "flagging for the AI team's daily pipeline.",
                    log.Id, log.ChildId, log.LogDate);
            }

            // ── 2. Leafy Recommendation completeness check ─────────────────
            // "Active" = at least one journal entry in the last 3 days.
            var recentWindow = DateTime.UtcNow.Date.AddDays(-3);
            var activeChildIds = await context.DailyLogEntries
                .Where(l => l.LogDate.Date >= recentWindow && l.IsJournalSaved)
                .Select(l => l.ChildId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (activeChildIds.Count > 0)
            {
                var staleCutoff = DateTime.UtcNow.AddDays(-2);

                var childrenMissingLeafyRec = await context.ChildProfiles
                    .Where(c => activeChildIds.Contains(c.UserId) && c.RecommendedLeafyTipId == null)
                    .Select(c => c.UserId)
                    .ToListAsync(cancellationToken);

                // Present but stale (AI hasn't refreshed it in 2+ days) — separate,
                // milder signal from "missing entirely". Consumes RecommendedLeafyTipSetAt,
                // which is written by AiController on every AI POST but otherwise only
                // exposed for read-back; this is its actual orchestration use.
                var childrenWithStaleLeafyRec = await context.ChildProfiles
                    .Where(c => activeChildIds.Contains(c.UserId)
                             && c.RecommendedLeafyTipId != null
                             && (c.RecommendedLeafyTipSetAt == null || c.RecommendedLeafyTipSetAt < staleCutoff))
                    .Select(c => c.UserId)
                    .ToListAsync(cancellationToken);

                if (childrenMissingLeafyRec.Count > 0)
                {
                    _logger.LogWarning(
                        "[AiOrchestration:Daily] {Count} active child/children have no AI Leafy recommendation set yet: {ChildIds}. " +
                        "Child-facing reads continue to use the existing backend smart-tip fallback in the meantime.",
                        childrenMissingLeafyRec.Count, string.Join(",", childrenMissingLeafyRec));
                }

                if (childrenWithStaleLeafyRec.Count > 0)
                {
                    _logger.LogInformation(
                        "[AiOrchestration:Daily] {Count} active child/children have a Leafy recommendation older than 2 days: {ChildIds}. " +
                        "Not regenerated by the backend — flagged for the AI team's next daily run.",
                        childrenWithStaleLeafyRec.Count, string.Join(",", childrenWithStaleLeafyRec));
                }
            }

            _logger.LogInformation(
                "[AiOrchestration:Daily] Validation run complete — {EntryCount} journal entries checked, {MissingCount} missing AI annotation.",
                yesterdaysEntries.Count, missingAiAnnotation.Count);
        }
    }
}

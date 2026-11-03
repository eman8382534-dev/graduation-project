using GrawiaaApp.API.Data;
using GrawiaaApp.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace GrawiaaApp.API.Services
{
    public class DailyLogClosureService : BackgroundService
    {
        private readonly ILogger<DailyLogClosureService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public DailyLogClosureService(
            ILogger<DailyLogClosureService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DailyLogClosureService started at {Time}", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Presentation spec: nightly processing at 2 AM (Egypt local).
                    await Task.Delay(NightlyScheduleHelper.DelayUntilNextRun(), stoppingToken);
                    await ProcessDailyClosure(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // طبيعي لما الخدمة تتوقف
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in DailyLogClosureService at {Time}", DateTime.UtcNow);
                }
            }
        }

        private async Task ProcessDailyClosure(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var yesterday = DateTime.UtcNow.Date.AddDays(-1);

            // ── 1. Finalize open journal entries ──────────────────────
            var openLogs = await context.DailyLogEntries
                .Where(l => l.LogDate.Date == yesterday && !l.IsFinalized)
                .ToListAsync(cancellationToken);

            foreach (var log in openLogs)
            {
                log.IsFinalized = true;
                // NOTE (Phase 9): AI fields (SentimentScore, DetectedEmotion, AnomalyScore,
                // NlpAnomalyScore, AnomalyFlag) are written exclusively by the external AI team.
                // The backend does not set default or fallback values for these fields.

                _logger.LogInformation("Auto-closed log {LogId} for child {ChildId}", log.Id, log.ChildId);
            }

            // ── 2. Reset fixed daily tasks (IsCompletedToday → false) ──
            // Fixed tasks (IsFixedTask = true) represent recurring daily activities
            // (prayers, Quran, etc.). They reset every night so children start
            // each day with a fresh task list. Parent-assigned tasks (IsCompleted)
            // are permanent and are NOT touched here.
            var fixedTasksToReset = await context.ChildTasks
                .Where(t => t.IsFixedTask && t.IsCompletedToday)
                .ToListAsync(cancellationToken);

            foreach (var task in fixedTasksToReset)
            {
                task.IsCompletedToday = false;
            }

            if (fixedTasksToReset.Any())
                _logger.LogInformation(
                    "Reset {Count} fixed tasks (IsCompletedToday → false) at nightly closure",
                    fixedTasksToReset.Count);

            if (openLogs.Any() || fixedTasksToReset.Any())
                await context.SaveChangesAsync(cancellationToken);

            // ── 3. Recompute task counts after nightly reset ──────────────
            // After IsCompletedToday is cleared, CompletedTasksCount on every
            // affected child's ChildProfile must drop to reflect the new day.
            // We only touch children who actually had tasks reset above.
            if (fixedTasksToReset.Any())
            {
                var pillarPipeline = scope.ServiceProvider.GetRequiredService<IPillarPipelineService>();
                var affectedChildIds = fixedTasksToReset
                    .Select(t => t.ChildId)
                    .Distinct()
                    .ToList();

                foreach (var childId in affectedChildIds)
                    await pillarPipeline.RecalculateTaskCountsAsync(childId);

                await context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation(
                    "Recalculated task counts for {Count} children after nightly reset",
                    affectedChildIds.Count);
            }
        }

    }
}
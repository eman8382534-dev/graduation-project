// ============================================================
//  PeriodicClusteringOrchestrationService.cs — Leafy AI System (Phase 12)
//
//  Periodic scheduled job covering: Personality Clustering.
//
//  IMPORTANT — ORCHESTRATION / VALIDATION ONLY:
//  ─────────────────────────────────────────────────────────
//  Never assigns a child to a cluster and never decides cluster
//  membership or similarity — that is written exclusively by the
//  external AI team via POST /api/ai/personality-clusters(/batch).
//  This job only:
//    1. Refreshes PersonalityCluster.MemberCount — a simple
//       COUNT(*) over existing AdminChildrenPersonalityClusters
//       rows where IsCurrent = true. This is bookkeeping for the
//       Admin dashboard's display performance, NOT a re-clustering
//       computation; the underlying assignments are never touched.
//    2. Logs which active children currently have NO cluster
//       assignment at all, so the AI team can include them in
//       the next clustering run.
//
//  Interval is configurable via appsettings:
//    "AiTeam": { "ClusteringIntervalDays": 7 }
//  Defaults to 7 days if not configured. Implemented as an
//  in-process BackgroundService (timer loop + DI scope per tick),
//  matching the existing DailyLogClosureService pattern — no new
//  scheduling dependency introduced.
// ============================================================

using GrawiaaApp.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GrawiaaApp.API.Services
{
    public class PeriodicClusteringOrchestrationService : BackgroundService
    {
        private readonly ILogger<PeriodicClusteringOrchestrationService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;

        public PeriodicClusteringOrchestrationService(
            ILogger<PeriodicClusteringOrchestrationService> logger,
            IServiceProvider serviceProvider,
            IConfiguration config)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PeriodicClusteringOrchestrationService started at {Time}", DateTime.UtcNow);

            var intervalDays = _config.GetValue<int?>("AiTeam:ClusteringIntervalDays") ?? 7;
            var interval = TimeSpan.FromDays(Math.Max(1, intervalDays));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, stoppingToken);
                    await RunPeriodicValidationAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Normal during shutdown.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in PeriodicClusteringOrchestrationService at {Time}", DateTime.UtcNow);
                }
            }
        }

        private async Task RunPeriodicValidationAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // ── 1. Refresh MemberCount bookkeeping (not re-clustering) ──────
            var clusters = await context.PersonalityClusters.ToListAsync(cancellationToken);
            foreach (var cluster in clusters)
            {
                cluster.MemberCount = await context.AdminChildrenPersonalityClusters
                    .CountAsync(a => a.PersonalityClusterId == cluster.Id && a.IsCurrent, cancellationToken);
            }
            if (clusters.Count > 0)
                await context.SaveChangesAsync(cancellationToken);

            // ── 2. Flag active children with no current cluster assignment ──
            var recentWindow = DateTime.UtcNow.Date.AddDays(-14);
            var activeChildIds = await context.DailyLogEntries
                .Where(l => l.LogDate.Date >= recentWindow && l.IsJournalSaved)
                .Select(l => l.ChildId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (activeChildIds.Count > 0)
            {
                var assignedChildIds = await context.AdminChildrenPersonalityClusters
                    .Where(a => a.IsCurrent && activeChildIds.Contains(a.ChildId))
                    .Select(a => a.ChildId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                var unassigned = activeChildIds.Except(assignedChildIds).ToList();

                if (unassigned.Count > 0)
                {
                    _logger.LogWarning(
                        "[AiOrchestration:Clustering] {Count} active child/children have no current personality cluster assignment: {ChildIds}. " +
                        "Applying rule-based fallback (AI clustering still supported via POST /api/ai/personality-clusters).",
                        unassigned.Count, string.Join(",", unassigned));

                    var fallback = scope.ServiceProvider.GetRequiredService<IPersonalityClusterFallbackService>();
                    await fallback.ApplyFallbackForUnassignedAsync(cancellationToken);
                }
            }

            _logger.LogInformation(
                "[AiOrchestration:Clustering] Periodic validation complete — {ClusterCount} cluster definitions refreshed, {ActiveCount} active children checked.",
                clusters.Count, activeChildIds.Count);
        }
    }
}

// ============================================================
//  NlpPillarSyncService.cs — Phase 13 Bug Fix
//  REWRITTEN 2026-06-21 — broadened scope, see "AUDIT FIX" below.
//
//  PROBLEM SOLVED:
//  ───────────────
//  Confirmed with the AI team (2026-06-21): they write ALL of their
//  outputs DIRECTLY to the daily_log_entries table via raw SQL —
//  SentimentScore, DetectedEmotion, AnomalyScore, NlpAnomalyScore,
//  AnomalyFlag, ScoreVariance, EmotionSafetyAlertFlag/Text, and the
//  8 NlpXxx_Pct + NlpPillarsCalculatedAt columns. None of this goes
//  through AiController — the /api/ai/* endpoints exist as a secondary
//  path only, not the one actually in use.
//
//  Because EF Core / the app layer never sees these writes, NEITHER
//  of the two pipeline steps that normally follow an AI annotation
//  ever run on their own:
//    • Step 1 (PillarPipelineService.ApplyDailyPillars) — recomputes
//      this ENTRY's 8 DailyXxx_Pct fields. This matters even for a
//      SentimentScore-only write: DailyMentalWellbeing_Pct is 60%
//      SentimentScore by design (see ChildProfile.cs), so until Step 1
//      re-runs on that entry, its daily mental-wellbeing figure is
//      still computed from a stale/zero SentimentScore.
//    • Step 2/3 (PillarPipelineService.RecalculateProfileAndTraitsAsync)
//      — recomputes the 30-day ChildProfile aggregates (Structured +
//      NLP + FinalXxx_Pct hybrid) and the 5 traits.
//
//  STEP 4 (Fix 3, added 2026-06-21): promotes DailyLogEntry's per-entry
//  EmotionSafetyAlertFlag (also written directly via SQL by the AI
//  team's Emotion Safety Engine) up to ChildProfile.HasActiveEmotionSafetyAlert
//  / LastEmotionSafetyAlertAt, based on the child's most recent saved
//  entry in the 30-day window. This is what GET /api/Parent/emotion-safety/{heroId}
//  reads — a single ChildProfile row instead of scanning DailyLogEntries
//  on every parent-dashboard request.
//
//  STEP 5 (Fix 3, added 2026-06-21): a separate staleness guard for the
//  same flag — see ClearStaleEmotionSafetyFlagsAsync — that runs every
//  tick regardless of the 30-day candidate window, so a child with no
//  recent AI activity at all still gets HasActiveEmotionSafetyAlert
//  correctly cleared once their actual latest entry no longer carries
//  the alert.
//
//  AUDIT FIX (2026-06-21): the previous version of this service only
//  watched NlpPillarsCalculatedAt and, when stale, called Step 2/3
//  ONLY — it never called Step 1 on the individual entries. That meant
//  even the case it WAS designed for (NLP pillar writes) left
//  DailyMentalWellbeing_Pct (and any other Daily figure whose engine
//  inputs the AI also touched) unrefreshed, because Step 2/3 just
//  averages whatever is currently sitting in the DailyXxx_Pct columns
//  — it does not recompute them. And because the trigger condition was
//  scoped to NlpPillarsCalculatedAt specifically, a direct-SQL write
//  that touched ONLY Trust-Engine or Emotion-Safety columns (no NLP
//  pillars in the same write) was invisible to this service entirely.
//
//  NEW APPROACH: rather than trying to detect exactly which column
//  changed and when (impossible to do reliably for writes that bypass
//  EF Core without adding a DB trigger + a generated timestamp column
//  — and SQL Server triggers on a table with an identity/computed
//  column require disabling EF Core's default OUTPUT-clause INSERT/
//  UPDATE behaviour via UseSqlOutputClause(false), which is a bigger,
//  riskier change to make blind in this pass), this service now just
//  unconditionally re-runs Step 1 + Step 2/3 for every child who has
//  ANY AI-annotated entry in the last 30 days, every tick. Idempotent
//  and cheap per child, so the "wasted" recompute on children with no
//  new data is an acceptable trade for guaranteed correctness.
//
//  ROTATION: candidates are ordered by LastAggregatedAt ascending (oldest
//  first), so if BatchSize is ever smaller than the active child count,
//  everyone still gets refreshed in rotation rather than the same
//  children winning every tick.
//
//  CONFIGURATION (appsettings.json):
//    "AiTeam": {
//      "NlpSyncIntervalMinutes": 60,   // how often to run (default 60)
//      "NlpSyncBatchSize":       200   // max children per tick (default 200)
//    }
//
//  NOTE FOR LATER: if/when this needs to scale beyond brute-force, the
//  precise fix is a SQL Server AFTER UPDATE trigger on DailyLogEntries
//  that stamps a new AiLastWriteAt column whenever any AI-owned column
//  changes, paired with modelBuilder.Entity<DailyLogEntry>().ToTable(tb
//  => tb.UseSqlOutputClause(false)) in OnModelCreating (required —
//  otherwise EF Core's default OUTPUT INSERTED.* on INSERT/UPDATE will
//  start failing once ANY trigger exists on this table). That EF Core
//  config change touches every write to DailyLogEntries in the app
//  (journal save, AI POST endpoint, everything) and should be made and
//  tested as its own deliberate change, not bundled into this fix.
// ============================================================

using GrawiaaApp.API.Data;
using Microsoft.EntityFrameworkCore;

namespace GrawiaaApp.API.Services
{
    public class NlpPillarSyncService : BackgroundService
    {
        private readonly ILogger<NlpPillarSyncService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;

        public NlpPillarSyncService(
            ILogger<NlpPillarSyncService> logger,
            IServiceProvider serviceProvider,
            IConfiguration config)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[AiPillarSync] Service started at {Time}", DateTime.UtcNow);

            // AUDIT FIX (real-time tuning): NlpSyncIntervalSeconds takes priority
            // when present, so this service can run sub-minute (e.g. every 5-10
            // seconds for live demos/testing). Falls back to the original
            // NlpSyncIntervalMinutes (minimum 1 minute) when the seconds key is
            // absent, so existing appsettings.json files keep working unchanged.
            var intervalSeconds = _config.GetValue<int?>("AiTeam:NlpSyncIntervalSeconds");
            TimeSpan interval;
            if (intervalSeconds.HasValue)
            {
                interval = TimeSpan.FromSeconds(Math.Max(1, intervalSeconds.Value));
            }
            else
            {
                var intervalMinutes = _config.GetValue<int?>("AiTeam:NlpSyncIntervalMinutes") ?? 60;
                interval = TimeSpan.FromMinutes(Math.Max(1, intervalMinutes));
            }

            // Initial delay: let the app finish startup before first run.
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SyncAiAnnotatedProfilesAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Normal during shutdown.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AiPillarSync] Error during sync at {Time}", DateTime.UtcNow);
                }

                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("[AiPillarSync] Service stopped at {Time}", DateTime.UtcNow);
        }

        private async Task SyncAiAnnotatedProfilesAsync(CancellationToken cancellationToken)
        {
            int batchSize = _config.GetValue<int?>("AiTeam:NlpSyncBatchSize") ?? 200;
            var windowStart = DateTime.UtcNow.Date.AddDays(-30);

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pipeline = scope.ServiceProvider.GetRequiredService<IPillarPipelineService>();
            var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

            // AUDIT FIX: candidate set is now "any child with ANY AI-written
            // signal in the 30-day window" — SentimentScore, AnomalyFlag,
            // NlpPillarsCalculatedAt, or Alert — not just NLP pillars. This
            // is what makes the Trust Engine / Emotion Safety direct-SQL
            // writes visible to this job at all.
            //
            // AUDIT FIX (2026-06-21): added e.Alert as its own condition.
            // Alert is the column the AI team's Emotion Safety Engine
            // actually writes via direct SQL (confirmed) — EmotionSafetyAlertFlag
            // is now DERIVED from Alert inside ApplyDailyPillars (see
            // PillarPipelineService), not written by the AI directly. Without
            // this condition, an entry where the AI wrote ONLY Alert (no
            // SentimentScore/AnomalyFlag/NlpPillarsCalculatedAt in the same
            // write) would never enter the candidate set, so the derivation
            // that turns Alert into EmotionSafetyAlertFlag/Text would never run.
            var candidateChildIds = await context.ChildProfiles
                .Where(cp => context.DailyLogEntries.Any(e =>
                    e.ChildId == cp.UserId &&
                    e.IsJournalSaved &&
                    e.LogDate.Date >= windowStart &&
                    (e.SentimentScore != null ||
                     e.AnomalyFlag ||
                     e.AnomalyScore != 0 ||
                     e.NlpPillarsCalculatedAt != null ||
                     e.EmotionSafetyAlertFlag ||
                     (e.Alert != null && e.Alert != ""))))
                .OrderBy(cp => cp.LastAggregatedAt ?? DateTime.MinValue)
                .Select(cp => cp.UserId)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (candidateChildIds.Count == 0)
            {
                _logger.LogDebug("[AiPillarSync] No AI-annotated profiles found at {Time}", DateTime.UtcNow);
                return;
            }

            _logger.LogInformation(
                "[AiPillarSync] Refreshing {Count} AI-annotated profiles (Step 1 entry recompute + Step 2/3 aggregate).",
                candidateChildIds.Count);

            int succeeded = 0;
            int failed = 0;

            foreach (var childId in candidateChildIds)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    // ── Step 1: re-run the per-entry pillar engine on every
                    // entry in the window, so any direct-SQL write to
                    // SentimentScore/AnomalyFlag/etc. that feeds into
                    // DailyXxx_Pct (e.g. DailyMentalWellbeing_Pct's 60%
                    // SentimentScore term) is actually reflected before
                    // Step 2 averages these columns. ApplyDailyPillars only
                    // mutates the tracked entities — SaveChanges below
                    // flushes everything in one round trip alongside Step 2/3.
                    var entries = await context.DailyLogEntries
                        .Where(e => e.ChildId == childId
                                 && e.IsJournalSaved
                                 && e.LogDate.Date >= windowStart)
                        .ToListAsync(cancellationToken);

                    foreach (var entry in entries)
                        pipeline.ApplyDailyPillars(entry);

                    // ── Step 2/3: 30-day structured + NLP aggregates,
                    // FinalXxx_Pct hybrid, FinalMeanScore, and the 5 traits.
                    var profile = await pipeline.RecalculateProfileAndTraitsAsync(childId);

                    // ── Step 4 (Fix 3): promote Emotion Safety active state
                    // up to ChildProfile so the parent dashboard can read one
                    // row instead of scanning DailyLogEntries. "Active" is
                    // defined by the MOST RECENT saved entry in the window
                    // (entries is already ordered by nothing in particular,
                    // so pick explicitly by LogDate) — an alert from 25 days
                    // ago should not still read as "active" today just
                    // because it's inside the 30-day window.
                    if (profile != null)
                    {
                        var latestEntry = entries
                            .OrderByDescending(e => e.LogDate)
                            .FirstOrDefault();

                        if (latestEntry != null && latestEntry.EmotionSafetyAlertFlag)
                        {
                            profile.HasActiveEmotionSafetyAlert = true;
                            profile.LastEmotionSafetyAlertAt = latestEntry.LogDate.ToUniversalTime();
                        }
                        else
                        {
                            // No alert on the latest entry — clear the active
                            // flag, but keep LastEmotionSafetyAlertAt as a
                            // historical record of the most recent alert ever.
                            profile.HasActiveEmotionSafetyAlert = false;
                        }
                    }

                    await context.SaveChangesAsync(cancellationToken);
                    succeeded++;

                    // AUDIT FIX (cache-invalidation gap): this service writes
                    // directly to ChildProfile/DailyLogEntry via EF Core, but
                    // ChildController.GetProfile (and other child/parent reads)
                    // cache their results for up to 3 minutes via ICacheService.
                    // That cache is normally invalidated by RemoveByPrefix calls
                    // sprinkled through the app's own write endpoints — but THIS
                    // service's writes never go through those endpoints, so
                    // without this explicit invalidation, a child/parent could
                    // see a stale FinalMeanScore/traits/Nlp pillars for up to
                    // 3 minutes after this sync already wrote the fresh values
                    // to the database. Clearing both prefixes here keeps reads
                    // consistent with what was just persisted.
                    cacheService.RemoveByPrefix(CacheKeys.ChildPrefix(childId));
                    if (profile?.ParentId != null)
                        cacheService.RemoveByPrefix(CacheKeys.ParentPrefix(profile.ParentId.Value));

                    _logger.LogDebug(
                        "[AiPillarSync] ChildId={ChildId} — {EntryCount} entries + profile aggregate refreshed.",
                        childId, entries.Count);
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex,
                        "[AiPillarSync] Failed to refresh profile for ChildId={ChildId}", childId);
                }
            }

            _logger.LogInformation(
                "[AiPillarSync] Sync complete — {Succeeded} refreshed, {Failed} failed.",
                succeeded, failed);

            // ── Step 5 (Fix 3 — staleness guard): the Step 4 promotion above
            // only ran for children inside this tick's 30-day-activity
            // candidate batch. A child whose LAST EVER AI activity was an
            // EmotionSafetyAlertFlag=true entry more than 30 days ago, with
            // NOTHING since, never re-enters that candidate set — so without
            // this pass HasActiveEmotionSafetyAlert would stay stuck at true
            // forever, even though the situation may be long resolved.
            //
            // This is intentionally a SEPARATE, narrow pass — not folded into
            // the candidate query above — because correctness here matters
            // more than batching efficiency: a parent should never see a
            // stale "active" safety alert just because the AI pipeline has
            // gone quiet for that child. It only touches children who
            // currently have the flag set, so it stays cheap in practice
            // (true emotion-safety alerts should be rare, not the common case).
            await ClearStaleEmotionSafetyFlagsAsync(cancellationToken);
        }

        /// <summary>
        /// For every ChildProfile with HasActiveEmotionSafetyAlert = true,
        /// re-checks against that child's single most recent saved
        /// DailyLogEntry (NO 30-day window — true "latest entry, period").
        /// If that latest entry no longer carries an Alert (AI cleared it,
        /// or a newer alert-free entry was saved since), clears
        /// HasActiveEmotionSafetyAlert. LastEmotionSafetyAlertAt is never
        /// touched here — it stays as the historical record of the most
        /// recent alert, cleared or not.
        ///
        /// AUDIT FIX (2026-06-21): checks the raw `Alert` column directly
        /// rather than the derived EmotionSafetyAlertFlag. Alert is what the
        /// AI team's Emotion Safety Engine actually writes via direct SQL —
        /// EmotionSafetyAlertFlag is only set when ApplyDailyPillars next
        /// runs on that entry, which may not have happened yet for a
        /// brand-new direct-SQL write. Reading Alert directly keeps this
        /// check correct regardless of derivation timing.
        /// </summary>
        private async Task ClearStaleEmotionSafetyFlagsAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var flaggedChildIds = await context.ChildProfiles
                .Where(cp => cp.HasActiveEmotionSafetyAlert)
                .Select(cp => cp.UserId)
                .ToListAsync(cancellationToken);

            if (flaggedChildIds.Count == 0) return;

            int cleared = 0;

            foreach (var childId in flaggedChildIds)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var latestEntry = await context.DailyLogEntries
                    .Where(e => e.ChildId == childId && e.IsJournalSaved)
                    .OrderByDescending(e => e.LogDate)
                    .FirstOrDefaultAsync(cancellationToken);

                // No saved entry at all, or the latest one no longer carries
                // an Alert → the active flag is stale, clear it.
                bool latestHasAlert = latestEntry != null && !string.IsNullOrWhiteSpace(latestEntry.Alert);
                if (!latestHasAlert)
                {
                    var profile = await context.ChildProfiles
                        .FirstOrDefaultAsync(cp => cp.UserId == childId, cancellationToken);
                    if (profile != null)
                    {
                        profile.HasActiveEmotionSafetyAlert = false;
                        cleared++;
                    }
                }
            }

            if (cleared > 0)
            {
                await context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation(
                    "[AiPillarSync] Cleared {Count} stale HasActiveEmotionSafetyAlert flag(s) (no alert on child's latest entry).",
                    cleared);
            }
        }
    }
}
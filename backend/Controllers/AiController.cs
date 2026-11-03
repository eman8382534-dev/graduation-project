// ============================================================
//  AiController.cs — Leafy AI System  (Phase 9)
//
//  Receives AI analysis results from the external AI team and
//  persists them to DailyLogEntry records in the database.
//
//  SECURITY:
//  ─────────────────────────────────────────────────────────
//  Protected by a shared API Key (X-AI-Api-Key header).
//  The AI team must include the key in every request.
//  The key is configured in appsettings.json → AiTeam:ApiKey
//  This is intentionally separate from JWT auth because the
//  AI service is not a user — it has no JWT token.
//
//  ENDPOINTS:
//  ─────────────────────────────────────────────────────────
//  POST /api/ai/log-result        → single entry result
//  POST /api/ai/log-result/batch  → multiple entries at once
//
//  AFTER SAVE:
//  ─────────────────────────────────────────────────────────
//  Triggers an immediate WeeklyTraitSynthesis for the affected
//  child so ChildProfile weekly AI fields are updated right away
//  without waiting for the 24-hour background service cycle.
// ============================================================

using GrawiaaApp.API.Data;
using GrawiaaApp.API.DTOs;
using GrawiaaApp.API.Models;
using GrawiaaApp.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GrawiaaApp.API.Controllers
{
    [Route("api/ai")]
    [ApiController]
    public class AiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPillarPipelineService _pillarPipeline; // PHASE 11
        private readonly IConfiguration _config;
        private readonly ILogger<AiController> _logger;
        private readonly IChildIdentityService _childIdentity; // PHASE 12

        public AiController(
            ApplicationDbContext context,
            IPillarPipelineService pillarPipeline,
            IConfiguration config,
            ILogger<AiController> logger,
            IChildIdentityService childIdentity)
        {
            _context = context;
            _pillarPipeline = pillarPipeline;
            _config = config;
            _logger = logger;
            _childIdentity = childIdentity;
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/ai/log-result
        // Single entry — AI team sends results for one DailyLogEntry
        // ─────────────────────────────────────────────────────────────
        [HttpPost("log-result")]
        public async Task<IActionResult> PostLogResult([FromBody] AiLogResultDto dto)
        {
            if (!ValidateApiKey()) return Unauthorized(new { error = "Invalid or missing API key." });
            if (dto is null) return BadRequest(new { error = "Request body is required." });

            var log = await _context.DailyLogEntries.FindAsync(dto.LogId);
            if (log is null)
                return NotFound(new { error = $"DailyLogEntry with Id={dto.LogId} not found." });

            ApplyAiResult(log, dto);
            _pillarPipeline.ApplyDailyPillars(log);

            if (!string.IsNullOrWhiteSpace(dto.IllnessAvoidanceTip))
            {
                var cp = await _context.ChildProfiles.FirstOrDefaultAsync(c => c.UserId == log.ChildId);
                if (cp != null)
                    cp.IllnessAvoidanceTip = dto.IllnessAvoidanceTip;
            }

            await _context.SaveChangesAsync(); // flush AI fields + DailyXxx_Pct

            // PHASE 11: now that SentimentScore/AnomalyFlag + DailyXxx_Pct are
            // persisted, recalculate the 30-day ChildProfile aggregate + traits.
            await _pillarPipeline.RecalculateProfileAndTraitsAsync(log.ChildId);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[AI] Log {LogId} updated — Sentiment={Sentiment:F2}, Emotion={Emotion}, AnomalyFlag={Flag}",
                log.Id, log.SentimentScore, log.DetectedEmotion, log.AnomalyFlag);

            var updatedFields = new List<string> { "SentimentScore", "DetectedEmotion", "AnomalyScore", "NlpAnomalyScore", "AnomalyFlag" };
            if (dto.ScoreVariance.HasValue) updatedFields.Add("ScoreVariance");

            bool nlpPillarsSent =
                dto.NlpSpiritualConnection_Pct.HasValue || dto.NlpSocialInteraction_Pct.HasValue ||
                dto.NlpAcademicProgress_Pct.HasValue || dto.NlpHomeResponsibility_Pct.HasValue ||
                dto.NlpPhysicalHealth_Pct.HasValue || dto.NlpPhysicalActivity_Pct.HasValue ||
                dto.NlpCreativityEngagement_Pct.HasValue || dto.NlpMentalWellbeing_Pct.HasValue;
            if (nlpPillarsSent) updatedFields.Add("NlpPillars(x8)");

            return Ok(new
            {
                message = "AI result saved successfully.",
                logId = log.Id,
                childId = log.ChildId,
                updatedFields
            });
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/ai/log-result/batch
        // Batch — AI team sends results for multiple entries at once
        // ─────────────────────────────────────────────────────────────
        [HttpPost("log-result/batch")]
        public async Task<IActionResult> PostLogResultBatch([FromBody] AiLogResultBatchDto dto)
        {
            if (!ValidateApiKey()) return Unauthorized(new { error = "Invalid or missing API key." });
            if (dto?.Results is null || dto.Results.Count == 0)
                return BadRequest(new { error = "Results list is required and must not be empty." });

            var logIds = dto.Results.Select(r => r.LogId).Distinct().ToList();
            var logs = await _context.DailyLogEntries
                                .Where(l => logIds.Contains(l.Id))
                                .ToListAsync();

            var logMap = logs.ToDictionary(l => l.Id);
            var notFound = new List<int>();
            var updated = new List<int>();
            var childIds = new HashSet<int>();

            foreach (var result in dto.Results)
            {
                if (!logMap.TryGetValue(result.LogId, out var log))
                {
                    notFound.Add(result.LogId);
                    continue;
                }

                ApplyAiResult(log, result);
                childIds.Add(log.ChildId);
                updated.Add(result.LogId);
            }

            if (updated.Count > 0)
            {
                // PHASE 11: Step 1 — compute + store DailyXxx_Pct on each
                // updated entry in-memory, then persist all at once.
                foreach (var result in dto.Results)
                {
                    if (logMap.TryGetValue(result.LogId, out var updatedLog))
                        _pillarPipeline.ApplyDailyPillars(updatedLog);
                }

                await _context.SaveChangesAsync(); // flush AI fields + DailyXxx_Pct for all entries

                // Step 2–3 — one 30-day aggregate + trait recalc per affected child.
                foreach (var childId in childIds)
                    await _pillarPipeline.RecalculateProfileAndTraitsAsync(childId);

                await _context.SaveChangesAsync(); // flush updated ChildProfile

                _logger.LogInformation(
                    "[AI] Batch update — {Count} logs updated for {ChildCount} children.",
                    updated.Count, childIds.Count);
            }

            return Ok(new
            {
                message = "Batch AI results processed.",
                updated,
                notFound
            });
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/ai/badge-suggestions
        // Single badge suggestion pushed by the AI/NLP team
        // ─────────────────────────────────────────────────────────────
        [HttpPost("badge-suggestions")]
        public async Task<IActionResult> PostBadgeSuggestion([FromBody] AiBadgeSuggestionDto dto)
        {
            if (!ValidateApiKey()) return Unauthorized(new { error = "Invalid or missing API key." });
            if (dto is null) return BadRequest(new { error = "Request body is required." });

            // Verify child exists
            var child = await _context.ChildProfiles
                .FirstOrDefaultAsync(c => c.UserId == dto.ChildId);
            if (child is null)
                return NotFound(new { error = $"Child with Id={dto.ChildId} not found." });

            // Avoid duplicate active suggestions for same child + pillar
            var existing = await _context.AiBadgeSuggestions
                .FirstOrDefaultAsync(s => s.ChildId == dto.ChildId
                                       && s.Pillar == dto.Pillar
                                       && !s.IsDismissed
                                       && s.ExpiresAt > DateTime.UtcNow);
            if (existing != null)
            {
                // Update the existing suggestion instead of creating a duplicate
                existing.BadgeName = dto.BadgeName;
                existing.BadgeIcon = dto.BadgeIcon;
                existing.Message = dto.Message;
                existing.Reason = dto.Reason;
                existing.Urgency = dto.Urgency;
                existing.CurrentPct = dto.CurrentPct;
                existing.TargetPct = dto.TargetPct;
                existing.ExpiresAt = DateTime.UtcNow.AddDays(dto.ExpiresInDays);
                existing.IsShown = false; // reset so child sees it again
                await _context.SaveChangesAsync();

                return Ok(new { message = "Existing suggestion updated.", id = existing.Id });
            }

            var suggestion = new AiBadgeSuggestion
            {
                ChildId = dto.ChildId,
                Pillar = dto.Pillar,
                BadgeName = dto.BadgeName,
                BadgeIcon = dto.BadgeIcon,
                Message = dto.Message,
                Reason = dto.Reason,
                Urgency = dto.Urgency,
                CurrentPct = dto.CurrentPct,
                TargetPct = dto.TargetPct,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(dto.ExpiresInDays),
            };

            _context.AiBadgeSuggestions.Add(suggestion);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[AI] Badge suggestion saved for Child={ChildId} Pillar={Pillar} Badge={Badge} Urgency={Urgency}",
                dto.ChildId, dto.Pillar, dto.BadgeName, dto.Urgency);

            return Ok(new { message = "Badge suggestion saved.", id = suggestion.Id });
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/ai/badge-suggestions/batch
        // Batch badge suggestions from the AI/NLP team
        // ─────────────────────────────────────────────────────────────
        [HttpPost("badge-suggestions/batch")]
        public async Task<IActionResult> PostBadgeSuggestionBatch([FromBody] AiBadgeSuggestionBatchDto dto)
        {
            if (!ValidateApiKey()) return Unauthorized(new { error = "Invalid or missing API key." });
            if (dto?.Suggestions is null || dto.Suggestions.Count == 0)
                return BadRequest(new { error = "Suggestions list is required and must not be empty." });

            var saved = new List<int>();
            var updated = new List<int>();
            var notFound = new List<int>();

            foreach (var s in dto.Suggestions)
            {
                var child = await _context.ChildProfiles
                    .FirstOrDefaultAsync(c => c.UserId == s.ChildId);
                if (child is null) { notFound.Add(s.ChildId); continue; }

                var existing = await _context.AiBadgeSuggestions
                    .FirstOrDefaultAsync(x => x.ChildId == s.ChildId
                                           && x.Pillar == s.Pillar
                                           && !x.IsDismissed
                                           && x.ExpiresAt > DateTime.UtcNow);
                if (existing != null)
                {
                    existing.BadgeName = s.BadgeName;
                    existing.BadgeIcon = s.BadgeIcon;
                    existing.Message = s.Message;
                    existing.Reason = s.Reason;
                    existing.Urgency = s.Urgency;
                    existing.CurrentPct = s.CurrentPct;
                    existing.TargetPct = s.TargetPct;
                    existing.ExpiresAt = DateTime.UtcNow.AddDays(s.ExpiresInDays);
                    existing.IsShown = false;
                    updated.Add(existing.Id);
                }
                else
                {
                    var suggestion = new AiBadgeSuggestion
                    {
                        ChildId = s.ChildId,
                        Pillar = s.Pillar,
                        BadgeName = s.BadgeName,
                        BadgeIcon = s.BadgeIcon,
                        Message = s.Message,
                        Reason = s.Reason,
                        Urgency = s.Urgency,
                        CurrentPct = s.CurrentPct,
                        TargetPct = s.TargetPct,
                        CreatedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddDays(s.ExpiresInDays),
                    };
                    _context.AiBadgeSuggestions.Add(suggestion);
                    saved.Add(s.ChildId);
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[AI] Badge suggestions batch — saved={Saved} updated={Updated} notFound={NotFound}",
                saved.Count, updated.Count, notFound.Count);

            return Ok(new { message = "Batch badge suggestions processed.", saved = saved.Count, updated = updated.Count, notFound });
        }

        // ════════════════════════════════════════════════════════════
        //  PHASE 12 — PERSONALITY CLUSTERING
        // ════════════════════════════════════════════════════════════

        // ─────────────────────────────────────────────────────────────
        // POST /api/ai/personality-clusters
        // Single child→cluster assignment from the periodic clustering job.
        // ─────────────────────────────────────────────────────────────
        [HttpPost("personality-clusters")]
        public async Task<IActionResult> PostPersonalityCluster([FromBody] AiPersonalityClusterAssignmentDto dto)
        {
            if (!ValidateApiKey()) return Unauthorized(new { error = "Invalid or missing API key." });
            if (dto is null) return BadRequest(new { error = "Request body is required." });
            if (string.IsNullOrWhiteSpace(dto.ClusterKey))
                return BadRequest(new { error = "ClusterKey is required." });

            var (ok, error, assignment) = await UpsertClusterAssignmentAsync(dto);
            if (!ok) return NotFound(new { error });

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[AI] Personality cluster assigned — Child={ChildId} Cluster={ClusterKey} Similarity={Score:F1}",
                dto.ChildId, dto.ClusterKey, dto.SimilarityScore);

            return Ok(new { message = "Personality cluster assignment saved.", childId = dto.ChildId, clusterKey = dto.ClusterKey, assignmentId = assignment!.Id });
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/ai/personality-clusters/batch
        // Batch — the clustering job typically re-clusters every active
        // child in a single periodic run.
        // ─────────────────────────────────────────────────────────────
        [HttpPost("personality-clusters/batch")]
        public async Task<IActionResult> PostPersonalityClusterBatch([FromBody] AiPersonalityClusterBatchDto dto)
        {
            if (!ValidateApiKey()) return Unauthorized(new { error = "Invalid or missing API key." });
            if (dto?.Assignments is null || dto.Assignments.Count == 0)
                return BadRequest(new { error = "Assignments list is required and must not be empty." });

            var saved = new List<int>();
            var notFound = new List<int>();

            foreach (var a in dto.Assignments)
            {
                if (string.IsNullOrWhiteSpace(a.ClusterKey)) { notFound.Add(a.ChildId); continue; }

                var (ok, _, assignment) = await UpsertClusterAssignmentAsync(a);
                if (!ok) { notFound.Add(a.ChildId); continue; }
                saved.Add(a.ChildId);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[AI] Personality clustering batch — saved={Saved} notFound={NotFound}",
                saved.Count, notFound.Count);

            return Ok(new { message = "Batch personality cluster assignments processed.", saved = saved.Count, notFound });
        }

        /// <summary>
        /// Shared upsert logic for single + batch cluster assignment endpoints
        /// (kept in one place so there is exactly one code path that writes
        /// AdminChildrenPersonalityClusters — avoids duplicate AI-write logic).
        /// Does NOT compute SimilarityScore or decide cluster membership —
        /// it only persists exactly what the AI team sent.
        /// </summary>
        private async Task<(bool ok, string? error, AdminChildrenPersonalityCluster? assignment)> UpsertClusterAssignmentAsync(AiPersonalityClusterAssignmentDto dto)
        {
            var child = await _context.ChildProfiles.FirstOrDefaultAsync(c => c.UserId == dto.ChildId);
            if (child is null) return (false, $"Child with Id={dto.ChildId} not found.", null);

            // Upsert the cluster definition by its stable key. Name is only
            // set the first time (admin may have since relabeled it); the
            // AI-owned descriptive fields are refreshed on every run.
            var cluster = await _context.PersonalityClusters
                .FirstOrDefaultAsync(c => c.ClusterKey == dto.ClusterKey);

            if (cluster is null)
            {
                cluster = new PersonalityCluster
                {
                    ClusterKey = dto.ClusterKey,
                    Name = string.IsNullOrWhiteSpace(dto.ClusterName) ? dto.ClusterKey : dto.ClusterName,
                    CreatedAt = DateTime.UtcNow,
                };
                _context.PersonalityClusters.Add(cluster);
            }

            cluster.Description = dto.Description ?? cluster.Description;
            cluster.DominantTraits = dto.DominantTraits ?? cluster.DominantTraits;
            cluster.Characteristics = dto.Characteristics ?? cluster.Characteristics;
            cluster.RecommendedFocus = dto.RecommendedFocus ?? cluster.RecommendedFocus;
            cluster.LastComputedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(); // ensure cluster.Id is available for new clusters

            // Flip the child's previous assignment(s) to not-current.
            var previous = await _context.AdminChildrenPersonalityClusters
                .Where(a => a.ChildId == dto.ChildId && a.IsCurrent)
                .ToListAsync();
            foreach (var p in previous) p.IsCurrent = false;

            var assignment = new AdminChildrenPersonalityCluster
            {
                ChildId = dto.ChildId,
                HashedChildId = _childIdentity.GetSafeChildKey(dto.ChildId),
                PersonalityClusterId = cluster.Id,
                SimilarityScore = dto.SimilarityScore,
                IsCurrent = true,
                ClusteringRunId = dto.ClusteringRunId,
                AssignedAt = DateTime.UtcNow,
            };
            _context.AdminChildrenPersonalityClusters.Add(assignment);

            // Denormalized pointer on the profile for fast reads.
            child.LastPersonalityClusterId = cluster.Id;
            child.PersonalityClusterAssignedAt = assignment.AssignedAt;

            return (true, null, assignment);
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/ai/personality-cluster/{hashedChildId}
        // AI team: read back the currently-stored cluster for a child
        // (useful for the AI pipeline to verify what was persisted).
        // ─────────────────────────────────────────────────────────────
        [HttpGet("personality-cluster/{hashedChildId}")]
        public async Task<IActionResult> GetPersonalityClusterByHashedId(string hashedChildId)
        {
            if (!ValidateApiKey())
                return Unauthorized(new { error = "Invalid or missing X-AI-Api-Key header." });

            var result = await _context.AdminChildrenPersonalityClusters
                .Include(a => a.Cluster)
                .Where(a => a.IsCurrent && a.HashedChildId == hashedChildId)
                .Select(a => new
                {
                    hashed_child_id = hashedChildId,
                    cluster_key = a.Cluster.ClusterKey,
                    cluster_name = a.Cluster.Name,
                    description = a.Cluster.Description,
                    dominant_traits = a.Cluster.DominantTraits,
                    recommended_focus = a.Cluster.RecommendedFocus,
                    similarity_score = a.SimilarityScore,
                    assigned_at = a.AssignedAt
                })
                .FirstOrDefaultAsync();

            if (result is null)
                return NotFound(new { error = $"No current personality cluster found for hashed_child_id={hashedChildId}" });

            return Ok(result);
        }

        // ════════════════════════════════════════════════════════════
        //  PHASE 12 — BADGE RECOMMENDER  (weekly, single best-pick)
        // ════════════════════════════════════════════════════════════

        // ─────────────────────────────────────────────────────────────
        // POST /api/ai/badge-recommendation
        // ─────────────────────────────────────────────────────────────
        [HttpPost("badge-recommendation")]
        public async Task<IActionResult> PostBadgeRecommendation([FromBody] AiBadgeRecommendationDto dto)
        {
            if (!ValidateApiKey()) return Unauthorized(new { error = "Invalid or missing API key." });
            if (dto is null) return BadRequest(new { error = "Request body is required." });

            var (ok, error) = await ApplyBadgeRecommendationAsync(dto);
            if (!ok) return NotFound(new { error });

            await _context.SaveChangesAsync();

            _logger.LogInformation("[AI] Badge recommendation set — Child={ChildId} BadgeId={BadgeId}", dto.ChildId, dto.RecommendedBadgeId);

            return Ok(new { message = "Badge recommendation saved.", childId = dto.ChildId, recommendedBadgeId = dto.RecommendedBadgeId });
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/ai/badge-recommendation/batch
        // ─────────────────────────────────────────────────────────────
        [HttpPost("badge-recommendation/batch")]
        public async Task<IActionResult> PostBadgeRecommendationBatch([FromBody] AiBadgeRecommendationBatchDto dto)
        {
            if (!ValidateApiKey()) return Unauthorized(new { error = "Invalid or missing API key." });
            if (dto?.Recommendations is null || dto.Recommendations.Count == 0)
                return BadRequest(new { error = "Recommendations list is required and must not be empty." });

            var saved = new List<int>();
            var notFound = new List<int>();

            foreach (var r in dto.Recommendations)
            {
                var (ok, _) = await ApplyBadgeRecommendationAsync(r);
                if (ok) saved.Add(r.ChildId); else notFound.Add(r.ChildId);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("[AI] Badge recommendation batch — saved={Saved} notFound={NotFound}", saved.Count, notFound.Count);

            return Ok(new { message = "Batch badge recommendations processed.", saved = saved.Count, notFound });
        }

        private async Task<(bool ok, string? error)> ApplyBadgeRecommendationAsync(AiBadgeRecommendationDto dto)
        {
            var child = await _context.ChildProfiles.FirstOrDefaultAsync(c => c.UserId == dto.ChildId);
            if (child is null) return (false, $"Child with Id={dto.ChildId} not found.");

            var badgeExists = await _context.Badges.AnyAsync(b => b.Id == dto.RecommendedBadgeId);
            if (!badgeExists) return (false, $"Badge with Id={dto.RecommendedBadgeId} not found.");

            child.RecommendedBadgeId = dto.RecommendedBadgeId;
            child.RecommendedBadgeSetAt = DateTime.UtcNow;
            return (true, null);
        }

        // ════════════════════════════════════════════════════════════
        //  PHASE 12 — LEAFY RECOMMENDATION  (daily)
        // ════════════════════════════════════════════════════════════

        // ─────────────────────────────────────────────────────────────
        // POST /api/ai/leafy-recommendation
        // ─────────────────────────────────────────────────────────────
        [HttpPost("leafy-recommendation")]
        public async Task<IActionResult> PostLeafyRecommendation([FromBody] AiLeafyRecommendationDto dto)
        {
            if (!ValidateApiKey()) return Unauthorized(new { error = "Invalid or missing API key." });
            if (dto is null) return BadRequest(new { error = "Request body is required." });

            var (ok, error) = await ApplyLeafyRecommendationAsync(dto);
            if (!ok) return NotFound(new { error });

            await _context.SaveChangesAsync();

            _logger.LogInformation("[AI] Leafy recommendation set — Child={ChildId} TipId={TipId} HasFreeText={HasFreeText}",
                dto.ChildId, dto.RecommendedLeafyTipId, dto.TipText != null || dto.MessageText != null || dto.AlertText != null);

            return Ok(new { message = "Leafy recommendation saved.", childId = dto.ChildId, recommendedLeafyTipId = dto.RecommendedLeafyTipId });
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/ai/leafy-recommendation/batch
        // ─────────────────────────────────────────────────────────────
        [HttpPost("leafy-recommendation/batch")]
        public async Task<IActionResult> PostLeafyRecommendationBatch([FromBody] AiLeafyRecommendationBatchDto dto)
        {
            if (!ValidateApiKey()) return Unauthorized(new { error = "Invalid or missing API key." });
            if (dto?.Recommendations is null || dto.Recommendations.Count == 0)
                return BadRequest(new { error = "Recommendations list is required and must not be empty." });

            var saved = new List<int>();
            var notFound = new List<int>();

            foreach (var r in dto.Recommendations)
            {
                var (ok, _) = await ApplyLeafyRecommendationAsync(r);
                if (ok) saved.Add(r.ChildId); else notFound.Add(r.ChildId);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("[AI] Leafy recommendation batch — saved={Saved} notFound={NotFound}", saved.Count, notFound.Count);

            return Ok(new { message = "Batch Leafy recommendations processed.", saved = saved.Count, notFound });
        }

        private async Task<(bool ok, string? error)> ApplyLeafyRecommendationAsync(AiLeafyRecommendationDto dto)
        {
            var child = await _context.ChildProfiles.FirstOrDefaultAsync(c => c.UserId == dto.ChildId);
            if (child is null) return (false, $"Child with Id={dto.ChildId} not found.");

            bool wroteSomething = false;

            // ── Pointer-based selection (optional) ──────────────────────
            if (dto.RecommendedLeafyTipId.HasValue)
            {
                var tipExists = await _context.LeafyTips.AnyAsync(t => t.Id == dto.RecommendedLeafyTipId.Value);
                if (!tipExists) return (false, $"LeafyTip with Id={dto.RecommendedLeafyTipId.Value} not found.");

                child.RecommendedLeafyTipId = dto.RecommendedLeafyTipId.Value;
                child.RecommendedLeafyTipSetAt = DateTime.UtcNow;
                wroteSomething = true;
            }

            // ── Free-text RAG output (optional, can be sent alongside or
            //    instead of the pointer) ──────────────────────────────────
            bool anyTextSent = dto.TipText != null || dto.MessageText != null || dto.AlertText != null;
            if (anyTextSent)
            {
                if (dto.TipText != null)
                {
                    child.LeafyTipText = dto.TipText;
                    child.Tip = dto.TipText;
                }
                if (dto.MessageText != null)
                {
                    child.LeafyMessageText = dto.MessageText;
                    child.Motivation = dto.MessageText;
                }
                if (dto.AlertText != null)
                {
                    child.LeafyAlertText = dto.AlertText;
                    child.Alert = dto.AlertText;
                }
                child.LeafyTextSetAt = DateTime.UtcNow;
                wroteSomething = true;
            }

            if (!wroteSomething)
                return (false, "At least one of RecommendedLeafyTipId, TipText, MessageText, or AlertText must be provided.");

            return (true, null);
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/ai/score-variance/{hashedChildId}
        // AI team: read back recently-stored ScoreVariance + NLP pillars
        // (lets the AI pipeline verify what is currently persisted).
        // ─────────────────────────────────────────────────────────────
        [HttpGet("score-variance/{hashedChildId}")]
        public async Task<IActionResult> GetScoreVarianceByHashedId(string hashedChildId)
        {
            if (!ValidateApiKey())
                return Unauthorized(new { error = "Invalid or missing X-AI-Api-Key header." });

            var entries = await _context.DailyLogEntries
                .Where(e => e.HashedChildId == hashedChildId)
                .OrderByDescending(e => e.LogDate)
                .Take(30)
                .Select(e => new
                {
                    log_id = e.Id,
                    log_date = e.LogDate,
                    score_variance = e.ScoreVariance,
                    anomaly_score = e.AnomalyScore,
                    anomaly_flag = e.AnomalyFlag,
                    nlp_spiritual = e.NlpSpiritualConnection_Pct,
                    nlp_social = e.NlpSocialInteraction_Pct,
                    nlp_academic = e.NlpAcademicProgress_Pct,
                    nlp_home = e.NlpHomeResponsibility_Pct,
                    nlp_physical_health = e.NlpPhysicalHealth_Pct,
                    nlp_physical_activity = e.NlpPhysicalActivity_Pct,
                    nlp_creativity = e.NlpCreativityEngagement_Pct,
                    nlp_mental = e.NlpMentalWellbeing_Pct,
                    nlp_calculated_at = e.NlpPillarsCalculatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                hashed_child_id = hashedChildId,
                count = entries.Count,
                entries
            });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/ai/child-profile/{hashedChildId}
        // AI team: read the child's full profile snapshot — all three
        // pillar sets (Structured, NLP Average, Final Hybrid), the
        // current level, traits, personality cluster, and latest
        // anomaly/sentiment signals.  This is the AI team's primary
        // read endpoint for inputs to clustering, badge recommendation,
        // and Leafy RAG.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("child-profile/{hashedChildId}")]
        public async Task<IActionResult> GetChildProfileByHashedId(string hashedChildId)
        {
            if (!ValidateApiKey())
                return Unauthorized(new { error = "Invalid or missing X-AI-Api-Key header." });

            var childId = await ResolveHashedChildIdAsync(hashedChildId);
            if (childId == null)
                return NotFound(new { error = $"No child found for hashed_child_id '{hashedChildId}'." });

            var profile = await _context.ChildProfiles
                .Include(p => p.LastPersonalityCluster)
                .FirstOrDefaultAsync(p => p.UserId == childId.Value);

            if (profile == null)
                return NotFound(new { error = "Child profile not found." });

            // Latest saved log for current anomaly / sentiment signals
            var latestLog = await _context.DailyLogEntries
                .Where(l => l.ChildId == childId.Value && l.IsJournalSaved)
                .OrderByDescending(l => l.LogDate)
                .Select(l => new
                {
                    l.SentimentScore,
                    l.DetectedEmotion,
                    l.AnomalyScore,
                    l.NlpAnomalyScore,
                    l.AnomalyFlag,
                    l.ScoreVariance,
                    l.LogDate
                })
                .FirstOrDefaultAsync();

            // Earned badge count
            int earnedBadgeCount = await _context.ChildBadges
                .CountAsync(cb => cb.ChildId == childId.Value && cb.IsActive);

            return Ok(new
            {
                hashed_child_id = hashedChildId,
                child_id = childId.Value,   // included for AI team convenience
                age = profile.Age,

                // ── Structured Pillars (30-day avg of structured journal inputs) ──
                structured = new
                {
                    spiritual_pct = profile.SpiritualConnection_Pct,
                    social_pct = profile.SocialInteraction_Pct,
                    academic_pct = profile.AcademicProgress_Pct,
                    environmental_pct = profile.HomeResponsibility_Pct,
                    physical_health_pct = profile.PhysicalHealth_Pct,
                    physical_activity_pct = profile.PhysicalActivity_Pct,
                    creativity_pct = profile.CreativityEngagement_Pct,
                    mental_pct = profile.MentalWellbeing_Pct,
                    mean_score = profile.MeanScore  // raw structured mean (backward-compat only — see final.mean_score below)
                },

                // ── NLP Pillar Averages (30-day avg written by NLP Trait Extractor) ──
                nlp = new
                {
                    spiritual_pct = profile.NlpSpiritualConnection_Pct,
                    social_pct = profile.NlpSocialInteraction_Pct,
                    academic_pct = profile.NlpAcademicProgress_Pct,
                    environmental_pct = profile.NlpHomeResponsibility_Pct,
                    physical_health_pct = profile.NlpPhysicalHealth_Pct,
                    physical_activity_pct = profile.NlpPhysicalActivity_Pct,
                    creativity_pct = profile.NlpCreativityEngagement_Pct,
                    mental_pct = profile.NlpMentalWellbeing_Pct
                },

                // ── Final Hybrid Pillars (70% Structured + 30% NLP) ──────────────
                final = new
                {
                    spiritual_pct = profile.FinalSpiritualConnection_Pct,
                    social_pct = profile.FinalSocialInteraction_Pct,
                    academic_pct = profile.FinalAcademicProgress_Pct,
                    environmental_pct = profile.FinalHomeResponsibility_Pct,
                    physical_health_pct = profile.FinalPhysicalHealth_Pct,
                    physical_activity_pct = profile.FinalPhysicalActivity_Pct,
                    creativity_pct = profile.FinalCreativityEngagement_Pct,
                    mental_pct = profile.FinalMentalWellbeing_Pct,
                    mean_score = profile.FinalMeanScore
                },

                // ── 5 Core Traits (derived from Final Hybrid pillars by PillarPipelineService) ──
                // BUG FIX (Phase 14 Audit): this comment previously said "Structured
                // pillars", which was true pre-Phase-13. TraitSynthesisEngine was
                // updated in Phase 13 to read profile.FinalXxx_Pct (70% Structured +
                // 30% NLP) instead — see TraitSynthesisEngine.cs's own header comment,
                // which already quotes this line as if it said "Final Hybrid Pillars".
                // The comment here was never actually updated to match. Text-only fix;
                // the values returned below were already correct (TraitSynthesisEngine
                // is the single source of truth for KindnessTrait/etc., this endpoint
                // just reads the already-computed profile columns).
                traits = new
                {
                    kindness = profile.KindnessTrait,
                    vitality = profile.VitalityTrait,
                    curiosity = profile.CuriosityTrait,
                    resilience = profile.ResilienceTrait,
                    spiritual = profile.SpiritualTrait
                },

                // ── Level & Gamification ──────────────────────────────────────────
                level = new
                {
                    level_number = profile.Level,
                    earned_badges = earnedBadgeCount,
                    trust_score = profile.TrustScore / 100.0,  // 0.0–1.0 (legacy TrustScore — NOT the level-up gate)
                    // BUG FIX (Phase 14): The AI team's check_for_level_up spec uses
                    // FinalMeanScore (70% Structured + 30% NLP) as the gate score,
                    // NOT TrustScore. Exposing both so the AI team reads the correct
                    // value. NewLevelEngineService.EvaluateLevelUpAsync uses FinalMeanScore
                    // (with MeanScore fallback when FinalMeanScore = 0).
                    final_mean_score = profile.FinalMeanScore > 0
                        ? profile.FinalMeanScore
                        : profile.MeanScore,   // 0-100 scale — same fallback as EvaluateLevelUpAsync
                    current_exp = profile.CurrentExp
                },

                // ── Personality Cluster (set by POST /api/ai/personality-clusters) ──
                personality_cluster = profile.LastPersonalityCluster == null ? null : new
                {
                    id = profile.LastPersonalityClusterId,
                    name = profile.LastPersonalityCluster.Name,
                    cluster_key = profile.LastPersonalityCluster.ClusterKey,
                    dominant_traits = profile.LastPersonalityCluster.DominantTraits,
                    recommended_focus = profile.LastPersonalityCluster.RecommendedFocus,
                    assigned_at = profile.PersonalityClusterAssignedAt
                },

                // ── Latest Emotion Safety / Anomaly signals ───────────────────────
                latest_log = latestLog == null ? null : new
                {
                    log_date = latestLog.LogDate,
                    sentiment_score = latestLog.SentimentScore,
                    detected_emotion = latestLog.DetectedEmotion,
                    anomaly_score = latestLog.AnomalyScore,
                    nlp_anomaly_score = latestLog.NlpAnomalyScore,
                    anomaly_flag = latestLog.AnomalyFlag,
                    score_variance = latestLog.ScoreVariance
                }
            });
        }

        private static void ApplyAiResult(DailyLogEntry log, AiLogResultDto dto)
        {
            log.SentimentScore = dto.SentimentScore;
            log.DetectedEmotion = dto.DetectedEmotion;
            log.AnomalyScore = dto.AnomalyScore;
            log.NlpAnomalyScore = dto.NlpAnomalyScore;
            log.AnomalyFlag = dto.AnomalyFlag;
            log.IsHonest = !dto.AnomalyFlag;

            // PHASE 12 — optional fields. Only overwrite when the AI team
            // actually sent a value, so older AI payloads that omit these
            // (pre-Phase-12 shape) leave existing stored values untouched.
            if (dto.ScoreVariance.HasValue)
                log.ScoreVariance = dto.ScoreVariance.Value;

            bool anyNlpPillarSent =
                dto.NlpSpiritualConnection_Pct.HasValue || dto.NlpSocialInteraction_Pct.HasValue ||
                dto.NlpAcademicProgress_Pct.HasValue || dto.NlpHomeResponsibility_Pct.HasValue ||
                dto.NlpPhysicalHealth_Pct.HasValue || dto.NlpPhysicalActivity_Pct.HasValue ||
                dto.NlpCreativityEngagement_Pct.HasValue || dto.NlpMentalWellbeing_Pct.HasValue;

            if (dto.NlpSpiritualConnection_Pct.HasValue) log.NlpSpiritualConnection_Pct = dto.NlpSpiritualConnection_Pct.Value;
            if (dto.NlpSocialInteraction_Pct.HasValue) log.NlpSocialInteraction_Pct = dto.NlpSocialInteraction_Pct.Value;
            if (dto.NlpAcademicProgress_Pct.HasValue) log.NlpAcademicProgress_Pct = dto.NlpAcademicProgress_Pct.Value;
            if (dto.NlpHomeResponsibility_Pct.HasValue) log.NlpHomeResponsibility_Pct = dto.NlpHomeResponsibility_Pct.Value;
            if (dto.NlpPhysicalHealth_Pct.HasValue) log.NlpPhysicalHealth_Pct = dto.NlpPhysicalHealth_Pct.Value;
            if (dto.NlpPhysicalActivity_Pct.HasValue) log.NlpPhysicalActivity_Pct = dto.NlpPhysicalActivity_Pct.Value;
            if (dto.NlpCreativityEngagement_Pct.HasValue) log.NlpCreativityEngagement_Pct = dto.NlpCreativityEngagement_Pct.Value;
            if (dto.NlpMentalWellbeing_Pct.HasValue) log.NlpMentalWellbeing_Pct = dto.NlpMentalWellbeing_Pct.Value;

            if (anyNlpPillarSent)
                log.NlpPillarsCalculatedAt = DateTime.UtcNow;

            // Emotion Safety — primary spec column `Alert` (also accepts legacy flag/text DTO fields)
            if (dto.Alert != null)
                log.Alert = dto.Alert;
            else if (dto.EmotionSafetyAlertText != null)
                log.Alert = dto.EmotionSafetyAlertText;

            if (dto.EmotionSafetyAlertFlag.HasValue)
                log.EmotionSafetyAlertFlag = dto.EmotionSafetyAlertFlag.Value;
            if (dto.EmotionSafetyAlertText != null)
                log.EmotionSafetyAlertText = dto.EmotionSafetyAlertText;

            if (!string.IsNullOrWhiteSpace(dto.IllnessAvoidanceTip))
                log.IllnessAvoidanceTip = dto.IllnessAvoidanceTip;
        }

        private bool ValidateApiKey()
        {
            var expectedKey = _config["AiTeam:ApiKey"];
            if (string.IsNullOrWhiteSpace(expectedKey))
            {
                _logger.LogWarning("[AI] AiTeam:ApiKey is not configured in appsettings.");
                return false;
            }

            Request.Headers.TryGetValue("X-AI-Api-Key", out var receivedKey);
            return receivedKey == expectedKey;
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/ai/journal
        // AI team: retrieve journal_entries by hashed_child_id
        // Returns all journal entries for a given hashed child.
        // Spec: journal_entries table — hashed_child_id, entry_text,
        //       submission_date, created_at
        //
        // AUDIT FIX (NLP inputs): the three NLP daily jobs (Honesty
        // Validator, Trait Extractor, Emotion Safety) all spec their
        // input as "general note, feeling note, message to parent note
        // in daily log table". GeneralNote and FeelingNote are now
        // write-through columns on this same table (the old
        // DailyQuestionAnswer table was removed), and MessageToParent
        // is written directly here too (the old ChildMessage table was
        // removed) — so all three are plain columns on this row with
        // no join or fallback required, which also means this same data
        // is correct for the AI team's direct-database reads, not just
        // through this API.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("journal/{hashedChildId}")]
        public async Task<IActionResult> GetJournalByHashedId(string hashedChildId)
        {
            if (!ValidateApiKey())
                return Unauthorized(new { error = "Invalid or missing X-AI-Api-Key header." });

            if (string.IsNullOrWhiteSpace(hashedChildId))
                return BadRequest(new { error = "hashedChildId is required." });

            var entries = await _context.DailyLogEntries
                .Where(e => e.HashedChildId == hashedChildId && e.IsJournalSaved)
                .OrderByDescending(e => e.LogDate)
                .Select(e => new
                {
                    journal_id = e.Id,
                    hashed_child_id = e.HashedChildId,
                    entry_text = e.ReflectionText ?? string.Empty,
                    general_note = e.GeneralNote ?? string.Empty,
                    feeling_note = e.FeelingNote ?? string.Empty,
                    message_to_parent = e.MessageToParent ?? string.Empty,
                    submission_date = e.LogDate,
                    created_at = e.LogDate,
                    mood_level = e.MoodLevel,
                    mood_emoji = e.MoodEmoji,
                    sentiment_score = e.SentimentScore,
                    detected_emotion = e.DetectedEmotion,
                    anomaly_score = e.AnomalyScore,
                    anomaly_flag = e.AnomalyFlag,
                    is_honest = e.IsHonest
                })
                .ToListAsync();

            return Ok(new
            {
                hashed_child_id = hashedChildId,
                count = entries.Count,
                journal_entries = entries
            });
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/ai/challenge-progress/{hashedChildId}
        // AI team: retrieve child_challenge_progress by hashed_child_id
        // ─────────────────────────────────────────────────────────────
        [HttpGet("challenge-progress/{hashedChildId}")]
        public async Task<IActionResult> GetChallengeProgressByHashedId(string hashedChildId)
        {
            if (!ValidateApiKey())
                return Unauthorized(new { error = "Invalid or missing X-AI-Api-Key header." });

            var progress = await _context.ChildChallengeProgresses
                .Include(cp => cp.Challenge)
                .Where(cp => cp.HashedChildId == hashedChildId)
                .OrderBy(cp => cp.ChallengeId)
                .Select(cp => new
                {
                    progress_id = cp.Id,
                    hashed_child_id = cp.HashedChildId,
                    challenge_id = cp.ChallengeId,
                    badge_id = cp.Challenge != null ? (int?)cp.Challenge.BadgeId : null,
                    challenge_text = cp.Challenge != null ? cp.Challenge.Description : null,
                    // BUG FIX (Phase 14): metric_type was missing from this response,
                    // so the AI team had no way to distinguish which challenges they own
                    // (metric_type = "journal_text") vs which the backend evaluates
                    // (streak, total_logs, tasks_completed, pillar_pct, etc.).
                    // Without this field, the NLP pipeline either had to hard-code
                    // challenge IDs or call GET /api/ai/badges separately and cross-
                    // reference — neither is reliable. Now the AI team can filter
                    // challenge_progress rows to metric_type="journal_text" and only
                    // write IsCompleted for those (matching the POST endpoint's own
                    // MetricType guard that rejects non-journal_text writes).
                    metric_type = cp.Challenge != null ? cp.Challenge.MetricType : null,
                    is_completed = cp.IsCompleted,
                    unlocked_at = cp.UnlockedAt,
                    current_value = cp.CurrentValue,
                    target_value = cp.Challenge != null ? (double?)cp.Challenge.TargetValue : null,
                    last_updated_at = cp.LastUpdatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                hashed_child_id = hashedChildId,
                count = progress.Count,
                challenge_progress = progress
            });
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/ai/challenge-progress/{hashedChildId}
        // AI team: write completion decisions for "journal_text" type
        // challenges (the 13-badge spec library) — the backend has no
        // numeric metric for these, so the AI's NLP pipeline (running over
        // GeneralNote/FeelingNote/ReflectionText) decides IsCompleted here.
        // Creates the ChildChallengeProgress row if it doesn't exist yet.
        // Does NOT touch any non-"journal_text" challenge (the backend's
        // own NewProgressionService owns those exclusively) — requests
        // targeting a challenge of a different MetricType are rejected.
        // ─────────────────────────────────────────────────────────────
        [HttpPost("challenge-progress/{hashedChildId}")]
        public async Task<IActionResult> PostChallengeProgressByHashedId(string hashedChildId, [FromBody] AiChallengeProgressBatchDto dto)
        {
            if (!ValidateApiKey())
                return Unauthorized(new { error = "Invalid or missing X-AI-Api-Key header." });

            if (dto?.Challenges is null || dto.Challenges.Count == 0)
                return BadRequest(new { error = "Challenges list is required and must not be empty." });

            int? childId = await ResolveHashedChildIdAsync(hashedChildId);
            if (childId is null)
                return NotFound(new { error = $"No child found for hashed_child_id={hashedChildId}" });

            var updated = new List<int>();
            var rejected = new List<object>();

            foreach (var item in dto.Challenges)
            {
                var challenge = await _context.BadgeChallenges.FindAsync(item.ChallengeId);
                if (challenge is null)
                {
                    rejected.Add(new { challengeId = item.ChallengeId, reason = "Challenge not found." });
                    continue;
                }

                if (challenge.MetricType != "journal_text")
                {
                    rejected.Add(new { challengeId = item.ChallengeId, reason = "Not an AI-owned journal_text challenge." });
                    continue;
                }

                var progress = await _context.ChildChallengeProgresses
                    .FirstOrDefaultAsync(cp => cp.ChildId == childId.Value && cp.ChallengeId == item.ChallengeId);

                if (progress is null)
                {
                    progress = new ChildChallengeProgress
                    {
                        ChildId = childId.Value,
                        HashedChildId = hashedChildId,
                        ChallengeId = item.ChallengeId,
                        CurrentValue = item.IsCompleted ? 1 : 0,
                        IsCompleted = item.IsCompleted,
                        UnlockedAt = item.IsCompleted ? DateTime.UtcNow : null,
                        LastUpdatedAt = DateTime.UtcNow
                    };
                    _context.ChildChallengeProgresses.Add(progress);
                }
                else if (!progress.IsCompleted)
                {
                    // Never reverse a completion once earned, same rule as
                    // the backend's own NewProgressionService.
                    progress.IsCompleted = item.IsCompleted;
                    progress.CurrentValue = item.IsCompleted ? 1 : 0;
                    progress.HashedChildId = hashedChildId;
                    progress.LastUpdatedAt = DateTime.UtcNow;
                    if (item.IsCompleted)
                        progress.UnlockedAt = DateTime.UtcNow;
                }

                updated.Add(item.ChallengeId);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("[AI] Challenge progress written — Child={ChildId} Updated={Updated} Rejected={Rejected}",
                childId, updated.Count, rejected.Count);

            return Ok(new { hashed_child_id = hashedChildId, updated, rejected });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/ai/user-level/{hashedChildId}
        // AI team: retrieve user_levels by hashed_child_id
        // ─────────────────────────────────────────────────────────────
        [HttpGet("user-level/{hashedChildId}")]
        public async Task<IActionResult> GetUserLevelByHashedId(string hashedChildId)
        {
            if (!ValidateApiKey())
                return Unauthorized(new { error = "Invalid or missing X-AI-Api-Key header." });

            var userLevel = await _context.UserLevels
                .Include(ul => ul.Level)
                .Where(ul => ul.HashedChildId == hashedChildId)
                .Select(ul => new
                {
                    hashed_child_id = ul.HashedChildId,
                    current_level_id = ul.LevelId,
                    level_number = ul.LevelNumber,
                    level_title = ul.Level != null ? ul.Level.Title : null,
                    level_name = ul.Level != null ? ul.Level.Name : null,
                    required_badges = ul.Level != null ? ul.Level.RequiredBadges : 0,
                    min_trust_score = ul.Level != null ? ul.Level.MinTrustScore : 0.0,
                    current_exp = ul.CurrentExp,
                    total_exp = ul.TotalExp,
                    last_level_up_at = ul.LastLevelUpAt,
                    last_updated_at = ul.LastUpdatedAt
                })
                .FirstOrDefaultAsync();

            if (userLevel == null)
                return NotFound(new { error = $"No user_level found for hashed_child_id={hashedChildId}" });

            return Ok(userLevel);
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/ai/tasks/{hashedChildId}
        // AI team spec: Anomaly Detection Trust Engine input — "Tasks in
        // child profile table". Returns this child's ChildTask records
        // (assigned + self-created, completed + pending) so the AI team's
        // daily job can read task-completion signal alongside
        // LogDuration and FinalMeanScore (NOT MeanScore).
        //
        // ⚠ ANOMALY DETECTION SPEC CLARIFICATION:
        // The AI team's Anomaly Detection Trust Engine must use
        // `final.mean_score` from GET /api/ai/child-profile, which maps to
        // ChildProfile.FinalMeanScore — the arithmetic mean of the 8 Final
        // Hybrid pillars (70% Structured + 30% NLP).
        // ChildProfile.MeanScore is the structured-only mean retained for
        // backward-compat and must NOT be read by the anomaly engine.
        //
        // ChildTask has no HashedChildId column (it's a high-churn,
        // backend-only table), so this endpoint resolves the hash to the
        // underlying ChildId via ResolveHashedChildIdAsync before querying.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("tasks/{hashedChildId}")]
        public async Task<IActionResult> GetTasksByHashedId(string hashedChildId)
        {
            if (!ValidateApiKey())
                return Unauthorized(new { error = "Invalid or missing X-AI-Api-Key header." });

            if (string.IsNullOrWhiteSpace(hashedChildId))
                return BadRequest(new { error = "hashedChildId is required." });

            int? childId = await ResolveHashedChildIdAsync(hashedChildId);
            if (childId is null)
                return NotFound(new { error = $"No child found for hashed_child_id={hashedChildId}" });

            var tasks = await _context.ChildTasks
                .Where(t => t.ChildId == childId.Value)
                .OrderByDescending(t => t.DueDate)
                .Select(t => new
                {
                    task_id = t.Id,
                    hashed_child_id = hashedChildId,
                    title = t.Title,
                    description = t.Description,
                    category = t.Category,
                    points = t.Points,
                    is_completed = t.IsCompleted,
                    is_parent_assigned = t.IsParentAssigned,
                    due_date = t.DueDate,
                    created_at = t.CreatedAt,
                    completed_at = t.CompletedAt
                })
                .ToListAsync();

            // ── BUG FIX (Phase 14): Anomaly Detection spec reads MeanScore as
            // its gate signal, but the Phase 14 note in GetChildProfileByHashedId
            // clarifies that FinalMeanScore (70% Structured + 30% NLP hybrid)
            // is the correct value — NOT ChildProfile.MeanScore (structured only).
            //
            // The tasks endpoint is the Anomaly engine's primary read call
            // (it fetches tasks + the score in one shot). Without final_mean_score
            // here, the AI team must make a SECOND call to /api/ai/child-profile
            // and read final.mean_score from a nested object — an easy mistake
            // that has caused silent use of the wrong (structured-only) MeanScore.
            //
            // FIX: embed final_mean_score directly in this response alongside the
            // tasks list, using the same fallback logic as EvaluateLevelUpAsync
            // (FinalMeanScore when > 0, MeanScore when NLP has not yet run).
            // ChildProfile.MeanScore is also included as structured_mean_score
            // for reference only — the anomaly engine MUST use final_mean_score.
            var profile = await _context.ChildProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == childId.Value);

            double finalMeanScore = profile != null
                ? (profile.FinalMeanScore > 0 ? profile.FinalMeanScore : profile.MeanScore)
                : 0;

            return Ok(new
            {
                hashed_child_id = hashedChildId,
                count = tasks.Count,
                completed_count = tasks.Count(t => t.is_completed),

                // ── Anomaly Detection score signal ──────────────────────────────
                // USE THIS for the Anomaly engine gate check (NOT structured_mean_score).
                // Mirrors the exact value NewLevelEngineService and GetChildProfileByHashedId
                // use: FinalMeanScore (hybrid 70%+30%) when available, MeanScore fallback.
                final_mean_score = finalMeanScore,

                // Kept for reference / backward-compat. Do NOT use for anomaly detection.
                structured_mean_score = profile?.MeanScore ?? 0,

                tasks
            });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/ai/journal-text-challenges
        // AI team: full catalog of every BadgeChallenge whose MetricType
        // is "journal_text" — i.e. all 26 challenges across the 13 pillar
        // badges (Ids 28-40) that the NLP pipeline must evaluate.
        //
        // BUG FIX (Phase 14): previously the AI team had no single endpoint
        // that listed *exactly* the challenges they own. They had to either:
        //   (a) Hard-code challenge IDs — fragile if the seeder changes.
        //   (b) Call GET /api/ai/badges, iterate badges, then call another
        //       endpoint per-badge — two round-trips per child run.
        //   (c) Read the BadgeChallenges table directly via raw SQL — same
        //       direct-DB coupling pattern we are trying to eliminate.
        //
        // This endpoint returns the complete journal_text challenge catalog
        // in one call. The NLP pipeline should:
        //   1. Call this once at startup / cache it — the catalog rarely changes.
        //   2. For each child's journal entry (GeneralNote / FeelingNote /
        //      ReflectionText), run NLP to decide IsCompleted for each challenge.
        //   3. POST the decisions to /api/ai/challenge-progress/{hashedChildId}.
        //
        // No hashedChildId needed — this is global reference data.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("journal-text-challenges")]
        public async Task<IActionResult> GetJournalTextChallenges()
        {
            if (!ValidateApiKey())
                return Unauthorized(new { error = "Invalid or missing X-AI-Api-Key header." });

            var challenges = await _context.BadgeChallenges
                .Include(bc => bc.Badge)
                .Where(bc => bc.MetricType == "journal_text")
                .OrderBy(bc => bc.BadgeId)
                .ThenBy(bc => bc.SortOrder)
                .Select(bc => new
                {
                    challenge_id = bc.Id,
                    badge_id = bc.BadgeId,
                    badge_name = bc.Badge != null ? bc.Badge.Name : null,
                    description = bc.Description,
                    emoji = bc.Emoji,
                    metric_type = bc.MetricType,
                    target_value = bc.TargetValue,
                    sort_order = bc.SortOrder
                })
                .ToListAsync();

            return Ok(new
            {
                count = challenges.Count,
                // All challenges here are metric_type="journal_text" and are
                // owned exclusively by the AI team's NLP pipeline.
                // POST completions to /api/ai/challenge-progress/{hashedChildId}.
                journal_text_challenges = challenges
            });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/ai/badges
        // AI team spec: Badge Recommender input — "Badges names and the
        // 8 pillars of each badge in badges table".
        //
        // AUDIT FIX: the AI team previously had only a WRITE endpoint
        // (POST /api/ai/badge-recommendation) to set the chosen badge —
        // there was no READ endpoint exposing the Badge catalog itself
        // (names + the 8 pillar-percentage target columns), forcing the
        // AI pipeline to query the Badges table directly. This endpoint
        // returns the full catalog so the AI can match a child's pillar
        // profile against each badge's target pillars exclusively
        // through the API.
        //
        // No hashedChildId needed — the badge catalog is global (admin-
        // curated reference data), not per-child.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("badges")]
        public async Task<IActionResult> GetBadgesCatalog()
        {
            if (!ValidateApiKey())
                return Unauthorized(new { error = "Invalid or missing X-AI-Api-Key header." });

            var badges = await _context.Badges
                .Select(b => new
                {
                    badge_id = b.Id,
                    badge_name = b.Name,
                    description = b.Description,
                    icon_url = b.IconUrl,
                    personality_side = b.PersonalitySide,
                    category = b.Category.ToString(),
                    // ── The 8 pillars of each badge (spec column names) ──
                    acad_pct = b.AcadPct,
                    soc_pct = b.SocPct,
                    ment_pct = b.MentPct,
                    phys_pct = b.PhysPct,
                    spir_pct = b.SpirPct,
                    env_pct = b.EnvPct,
                    crea_pct = b.CreaPct,
                    res_pct = b.ResPct
                })
                .ToListAsync();

            return Ok(new
            {
                count = badges.Count,
                badges
            });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/ai/leafy-tips
        // AI team spec: Leafy RAG input — "the content of Leafy tip
        // table (tip, motivation, alert) to map it to each other [with
        // the child's 8 pillars and 5 traits]".
        //
        // AUDIT FIX: same gap as /api/ai/badges — the AI team had only a
        // WRITE endpoint (POST /api/ai/leafy-recommendation) to persist
        // its chosen tip/alert/motivation, with no READ endpoint exposing
        // the LeafyTip catalog content itself. This returns every catalog
        // row's Side (which of the 8 pillars it targets), Type (tone),
        // Purpose (tip|alert|motivation channel), and the three content
        // fields, so the AI's daily RAG job can do the pillar/trait →
        // content matching purely from API data.
        //
        // Optional ?side= and ?purpose= query filters are provided for
        // convenience (e.g. the AI job pulling only "mental"/"tip" rows)
        // but default to returning the full catalog.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("leafy-tips")]
        public async Task<IActionResult> GetLeafyTipsCatalog([FromQuery] string? side, [FromQuery] string? purpose)
        {
            if (!ValidateApiKey())
                return Unauthorized(new { error = "Invalid or missing X-AI-Api-Key header." });

            var query = _context.LeafyTips.AsQueryable();

            if (!string.IsNullOrWhiteSpace(side))
                query = query.Where(t => t.Side == side);

            if (!string.IsNullOrWhiteSpace(purpose))
                query = query.Where(t => t.Purpose == purpose);

            var tips = await query
                .Select(t => new
                {
                    tip_id = t.Id,
                    side = t.Side,        // which of the 8 pillars this targets
                    type = t.Type,        // encouragement | warning | achievement
                    purpose = t.Purpose,  // tip | alert | motivation
                    tip = t.Tip,
                    alert = t.Alert,
                    motivation = t.Motivation,
                    content = t.Content   // legacy unified field, kept for back-compat
                })
                .ToListAsync();

            return Ok(new
            {
                count = tips.Count,
                leafy_tips = tips
            });
        }

        // ─────────────────────────────────────────────────────────────
        // Resolves a hashed_child_id (as produced by IChildIdentityService.
        // GetSafeChildKey) back to the underlying int ChildId. The hash is
        // one-way by design, so this scans known ChildProfile.UserId values
        // and matches the derived hash — acceptable here because this
        // endpoint is AI-team-internal/low-volume and ChildProfile.UserId
        // is already indexed as the table's primary key.
        // ─────────────────────────────────────────────────────────────
        private async Task<int?> ResolveHashedChildIdAsync(string hashedChildId)
        {
            var childIds = await _context.ChildProfiles
                .Select(p => p.UserId)
                .ToListAsync();

            foreach (var id in childIds)
            {
                if (_childIdentity.GetSafeChildKey(id) == hashedChildId)
                    return id;
            }

            return null;
        }
    }
}
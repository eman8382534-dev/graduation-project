// ============================================================
//  PersonalityClusterFallbackService.cs — Rule-based cluster fallback
//
//  Real ML clustering is owned by the AI team (POST /api/ai/personality-clusters).
//  This service assigns a simple rule-based cluster ONLY when a child has
//  no current cluster assignment — it never replaces AI assignments.
//
//  Rules (first match wins by highest signal):
//    High Empathy       (KindnessTrait)              → Helper
//    High Physical      (VitalityTrait)              → Explorer
//    High Responsibility (HomeResponsibility_Pct)    → Leader
//    High Creativity    (CreativityEngagement_Pct)  → Creator
// ============================================================

using GrawiaaApp.API.Data;
using GrawiaaApp.API.Models;
using Microsoft.EntityFrameworkCore;

namespace GrawiaaApp.API.Services
{
    public interface IPersonalityClusterFallbackService
    {
        Task<int> ApplyFallbackForUnassignedAsync(CancellationToken ct = default);
    }

    public sealed class PersonalityClusterFallbackService : IPersonalityClusterFallbackService
    {
        private readonly ApplicationDbContext _context;
        private readonly IChildIdentityService _childIdentity;
        private readonly ILogger<PersonalityClusterFallbackService> _logger;

        private static readonly (string Key, string Name, Func<ChildProfile, double> Signal)[] Rules =
        {
            ("cluster_helper",   "Helper",   p => p.KindnessTrait),
            ("cluster_explorer", "Explorer", p => p.VitalityTrait),
            ("cluster_leader",   "Leader",   p => p.HomeResponsibility_Pct),
            ("cluster_creator",  "Creator",  p => p.CreativityEngagement_Pct),
        };

        public PersonalityClusterFallbackService(
            ApplicationDbContext context,
            IChildIdentityService childIdentity,
            ILogger<PersonalityClusterFallbackService> logger)
        {
            _context = context;
            _childIdentity = childIdentity;
            _logger = logger;
        }

        public async Task<int> ApplyFallbackForUnassignedAsync(CancellationToken ct = default)
        {
            var recentWindow = DateTime.UtcNow.Date.AddDays(-14);
            var activeChildIds = await _context.DailyLogEntries
                .Where(l => l.LogDate.Date >= recentWindow && l.IsJournalSaved)
                .Select(l => l.ChildId)
                .Distinct()
                .ToListAsync(ct);

            if (activeChildIds.Count == 0)
                return 0;

            var assignedIds = await _context.AdminChildrenPersonalityClusters
                .Where(a => a.IsCurrent && activeChildIds.Contains(a.ChildId))
                .Select(a => a.ChildId)
                .ToListAsync(ct);

            var unassignedIds = activeChildIds.Except(assignedIds).ToList();
            if (unassignedIds.Count == 0)
                return 0;

            int assigned = 0;
            foreach (var childId in unassignedIds)
            {
                var profile = await _context.ChildProfiles.FirstOrDefaultAsync(p => p.UserId == childId, ct);
                if (profile is null) continue;

                var rule = Rules
                    .Select(r => (r.Key, r.Name, Score: r.Signal(profile)))
                    .OrderByDescending(r => r.Score)
                    .First();

                if (rule.Score <= 0)
                    continue;

                var cluster = await EnsureClusterAsync(rule.Key, rule.Name, ct);
                await AssignClusterAsync(profile, cluster, ct);
                assigned++;
            }

            if (assigned > 0)
            {
                await _context.SaveChangesAsync(ct);
                _logger.LogInformation("[ClusterFallback] Assigned {Count} children via rule-based fallback.", assigned);
            }

            return assigned;
        }

        private async Task<PersonalityCluster> EnsureClusterAsync(string key, string name, CancellationToken ct)
        {
            var cluster = await _context.PersonalityClusters
                .FirstOrDefaultAsync(c => c.ClusterKey == key, ct);

            if (cluster is null)
            {
                cluster = new PersonalityCluster
                {
                    ClusterKey = key,
                    Name = name,
                    Description = $"Fallback cluster — {name} (rule-based; AI clustering may override).",
                    DominantTraits = name,
                    CreatedAt = DateTime.UtcNow,
                };
                _context.PersonalityClusters.Add(cluster);
                await _context.SaveChangesAsync(ct);
            }

            return cluster;
        }

        private async Task AssignClusterAsync(ChildProfile profile, PersonalityCluster cluster, CancellationToken ct)
        {
            var previous = await _context.AdminChildrenPersonalityClusters
                .Where(a => a.ChildId == profile.UserId && a.IsCurrent)
                .ToListAsync(ct);
            foreach (var p in previous) p.IsCurrent = false;

            var assignment = new AdminChildrenPersonalityCluster
            {
                ChildId = profile.UserId,
                HashedChildId = _childIdentity.GetSafeChildKey(profile.UserId),
                PersonalityClusterId = cluster.Id,
                SimilarityScore = 100,
                IsCurrent = true,
                ClusteringRunId = "backend-fallback",
                AssignedAt = DateTime.UtcNow,
            };
            _context.AdminChildrenPersonalityClusters.Add(assignment);

            profile.LastPersonalityClusterId = cluster.Id;
            profile.PersonalityClusterAssignedAt = assignment.AssignedAt;
        }
    }
}

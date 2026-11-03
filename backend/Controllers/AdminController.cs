using GrawiaaApp.API.Data;
using GrawiaaApp.API.DTOs;
using GrawiaaApp.API.Models;
using GrawiaaApp.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GrawiaaApp.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICacheService _cache;
        private readonly ISystemIntegrityValidator _integrityValidator;

        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ICacheService cache, ISystemIntegrityValidator integrityValidator)
        {
            _context = context;
            _userManager = userManager;
            _cache = cache;
            _integrityValidator = integrityValidator;
        }

        #region Admin Dashboard

        [HttpGet("dashboard-stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                var result = await _cache.GetOrCreateAsync(CacheKeys.AdminDashboard, async () =>
                {
                    var totalUsers = await _userManager.Users.CountAsync();
                    var today = DateTime.Today;

                    var dailyJournals = await _context.DailyLogEntries
                        .CountAsync(d => d.LogDate.Date == today);

                    var avgTrust = await _context.ChildProfiles
                        .Where(c => c.TrustScore > 0)
                        .AverageAsync(c => (double?)c.TrustScore) ?? 0;

                    var last7Days = Enumerable.Range(0, 7)
                        .Select(i => DateTime.Today.AddDays(-i))
                        .ToList();

                    var logCounts = await _context.DailyLogEntries
                        .Where(l => l.LogDate.Date >= DateTime.Today.AddDays(-6))
                        .GroupBy(l => l.LogDate.Date)
                        .Select(g => new { Date = g.Key, Count = g.Count() })
                        .ToListAsync();

                    var newUserCounts = await _userManager.Users
                        .Where(u => u.CreatedAt.Date >= DateTime.Today.AddDays(-6))
                        .GroupBy(u => u.CreatedAt.Date)
                        .Select(g => new { Date = g.Key, Count = g.Count() })
                        .ToListAsync();

                    var trends = last7Days.Select(date => new
                    {
                        Date = date.ToString("MMM d, yyyy"),
                        JournalsCompleted = logCounts.FirstOrDefault(l => l.Date == date.Date)?.Count ?? 0,
                        NewUsers = newUserCounts.FirstOrDefault(u => u.Date == date.Date)?.Count ?? 0,
                        AvgTrustScore = Math.Round(avgTrust, 2)
                    }).ToList<object>();

                    return new
                    {
                        TotalUsers = totalUsers,
                        DailyJournals = dailyJournals,
                        AvgTrustScore = Math.Round(avgTrust, 2),
                        DailyStats = trends
                    };
                }, TimeSpan.FromMinutes(10));

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        #endregion

        #region Admin Content Engine

        [HttpGet("log-form-items")]
        public async Task<IActionResult> GetLogFormItems()
        {
            var items = await _cache.GetOrCreateAsync(CacheKeys.AllLogFormItems, async () =>
                await _context.LogFormItems
                    .Select(item => new LogFormItemDto
                    {
                        Id = item.Id,
                        Category = item.Category,
                        Value = item.Value,
                        Type = item.Type
                    })
                    .ToListAsync()
            );
            return Ok(items);
        }

        [HttpPost("log-form-items")]
        public async Task<IActionResult> AddLogFormItem([FromBody] LogFormItemDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Value)) return BadRequest("Value is required");

            _context.LogFormItems.Add(new LogFormItem
            {
                Category = dto.Category,
                Value = dto.Value,
                Type = dto.Type
            });
            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeys.AllLogFormItems);
            return Ok(new { message = "Item added successfully!" });
        }

        [HttpPut("log-form-items/{id}")]
        public async Task<IActionResult> UpdateLogFormItem(int id, [FromBody] LogFormItemDto dto)
        {
            var item = await _context.LogFormItems.FindAsync(id);
            if (item == null) return NotFound();
            if (string.IsNullOrWhiteSpace(dto.Value)) return BadRequest("Value is required");

            item.Category = dto.Category;
            item.Value = dto.Value;
            item.Type = dto.Type;

            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeys.AllLogFormItems);
            return Ok(new { message = "Item updated successfully!" });
        }

        [HttpDelete("log-form-items/{id}")]
        public async Task<IActionResult> DeleteLogFormItem(int id)
        {
            var item = await _context.LogFormItems.FindAsync(id);
            if (item == null) return NotFound("Item not found");

            _context.LogFormItems.Remove(item);
            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeys.AllLogFormItems);
            return Ok(new { message = "Item deleted successfully!" });
        }

        [HttpGet("leafy-tips")]
        public async Task<IActionResult> GetLeafyTips()
        {
            var tips = await _cache.GetOrCreateAsync(CacheKeys.AllLeafyTips, async () =>
                await _context.LeafyTips
                    .Select(t => new LeafyTipFullDto
                    {
                        Id = t.Id,
                        Side = t.Side,
                        Type = t.Type,
                        Purpose = t.Purpose,
                        Content = t.Content,
                        Tip = t.Tip,
                        Alert = t.Alert,
                        Motivation = t.Motivation
                    })
                    .ToListAsync()
            );
            return Ok(tips);
        }

        [HttpPost("leafy-tips")]
        public async Task<IActionResult> AddLeafyTip([FromBody] UpsertLeafyTipDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (string.IsNullOrWhiteSpace(dto.Content)) return BadRequest("Content is required");

            _context.LeafyTips.Add(new LeafyTip
            {
                Side = dto.Side,
                Type = dto.Type,
                Purpose = dto.Purpose,
                Content = dto.Content,
                Tip = dto.Tip,
                Alert = dto.Alert,
                Motivation = dto.Motivation
            });
            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeys.AllLeafyTips);
            return Ok(new { message = "Leafy tip added successfully! 🌱" });
        }

        [HttpPut("leafy-tips/{id}")]
        public async Task<IActionResult> UpdateLeafyTip(int id, [FromBody] UpsertLeafyTipDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var tip = await _context.LeafyTips.FindAsync(id);
            if (tip == null) return NotFound("Tip not found");
            if (string.IsNullOrWhiteSpace(dto.Content)) return BadRequest("Content is required");

            tip.Side = dto.Side; tip.Type = dto.Type; tip.Purpose = dto.Purpose;
            tip.Content = dto.Content; tip.Tip = dto.Tip; tip.Alert = dto.Alert; tip.Motivation = dto.Motivation;
            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeys.AllLeafyTips);
            return Ok(new { message = "Leafy tip updated successfully! 🌱✨" });
        }

        [HttpDelete("leafy-tips/{id}")]
        public async Task<IActionResult> DeleteLeafyTip(int id)
        {
            var tip = await _context.LeafyTips.FindAsync(id);
            if (tip == null) return NotFound("Tip not found");

            _context.LeafyTips.Remove(tip);
            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeys.AllLeafyTips);
            return Ok(new { message = "Leafy tip deleted successfully! 🌱🗑️" });
        }

        [HttpGet("badges")]
        public async Task<IActionResult> GetBadges()
        {
            var badges = await _cache.GetOrCreateAsync(CacheKeys.AllBadges, async () =>
                await _context.Badges
                    .Select(b => new BadgeDto
                    {
                        Id = b.Id,
                        Name = b.Name,
                        PersonalitySide = b.PersonalitySide,
                        Category = b.Category.ToString(),
                        Description = b.Description,
                        IconUrl = b.IconUrl
                        // تم حذف تعيين IsUnlocked من هنا لعدم وجودها في موديل Badge الأساسي
                    })
                    .ToListAsync()
            );
            return Ok(badges);
        }

        [HttpPost("badges")]
        public async Task<IActionResult> AddBadge([FromBody] BadgeDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest("Name is required");
            if (!Enum.TryParse<BadgeCategory>(dto.Category, true, out var parsedCategory))
                return BadRequest("Invalid Badge Category");

            var newBadge = new Badge
            {
                Name = dto.Name,
                PersonalitySide = dto.PersonalitySide ?? "Left",
                Category = parsedCategory,
                Description = dto.Description ?? "",
                IconUrl = dto.IconUrl ?? ""
                // تم حذف IsUnlocked = false من هنا لعدم وجود الخاصية في الكلاس
            };

            _context.Badges.Add(newBadge);
            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeys.AllBadges);
            return Ok(new { message = "New badge added successfully!", id = newBadge.Id });
        }


        [HttpPut("badges/{id}")]
        public async Task<IActionResult> UpdateBadge(int id, [FromBody] BadgeDto dto)
        {
            try
            {
                var badge = await _context.Badges.FirstOrDefaultAsync(b => b.Id == id);
                if (badge == null) return NotFound("Badge not found");
                if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest("Name required");
                if (!Enum.TryParse<BadgeCategory>(dto.Category, true, out var parsedCategory))
                    return BadRequest($"Invalid Category: {dto.Category}");

                badge.Name = dto.Name; badge.PersonalitySide = dto.PersonalitySide ?? "Left";
                badge.Category = parsedCategory; badge.Description = dto.Description ?? string.Empty;
                badge.IconUrl = dto.IconUrl ?? "";

                await _context.SaveChangesAsync();
                _cache.Remove(CacheKeys.AllBadges);
                return Ok(new { message = "Updated successfully!" });
            }
            catch (Exception ex) { return StatusCode(500, $"Internal error: {ex.Message}"); }
        }

        [HttpDelete("badges/{id}")]
        public async Task<IActionResult> DeleteBadge(int id)
        {
            var badge = await _context.Badges.FindAsync(id);
            if (badge == null) return NotFound("Badge not found");

            _context.Badges.Remove(badge);
            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeys.AllBadges);
            return Ok(new { message = "Badge deleted successfully! 🏆🗑️" });
        }

        [HttpGet("characters")]
        public async Task<IActionResult> GetCharacters()
        {
            var characters = await _cache.GetOrCreateAsync(CacheKeys.AllCharacters, async () =>
                await _context.Characters
                    .Select(c => new { c.Id, c.Avatar, c.Name, c.Description, c.Personality })
                    .ToListAsync()
            );
            return Ok(characters);
        }

        [HttpPost("characters")]
        public async Task<IActionResult> AddCharacter([FromBody] CharacterDto dto)
        {
            _context.Characters.Add(new Character { Avatar = dto.Avatar, Name = dto.Name, Description = dto.Description, Personality = dto.Personality });
            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeys.AllCharacters);
            return Ok(new { message = "Character added! 🎨" });
        }

        [HttpPut("characters/{id}")]
        public async Task<IActionResult> UpdateCharacter(int id, [FromBody] CharacterDto dto)
        {
            var character = await _context.Characters.FindAsync(id);
            if (character == null) return NotFound();
            character.Avatar = dto.Avatar; character.Name = dto.Name; character.Description = dto.Description; character.Personality = dto.Personality;
            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeys.AllCharacters);
            return Ok(new { message = "Character updated!" });
        }

        [HttpDelete("characters/{id}")]
        public async Task<IActionResult> DeleteCharacter(int id)
        {
            var character = await _context.Characters.FindAsync(id);
            if (character == null) return NotFound();
            _context.Characters.Remove(character);
            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeys.AllCharacters);
            return Ok(new { message = "Character deleted!" });
        }

        #endregion

        #region User Management

        [HttpGet("users/search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string? query)
        {
            var usersQuery = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                usersQuery = usersQuery.Where(user =>
                    (user.FullName != null && user.FullName.Contains(query)) ||
                    (user.Email != null && user.Email.Contains(query))
                );
            }

            var rawUsers = await (
                from user in usersQuery
                join cp in _context.ChildProfiles
                    on user.Id equals cp.UserId into childProfiles
                from cp in childProfiles.DefaultIfEmpty()
                select new
                {
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.LastActiveAt,
                    user.UserType,
                    TrustScore = cp != null ? cp.TrustScore : 0,
                    ProfileType = user.UserType
                }
            ).Take(20).ToListAsync();

            var now = DateTime.UtcNow;
            var result = rawUsers.Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.TrustScore,
                u.ProfileType,
                u.LastActiveAt,
                IsActive = u.LastActiveAt != default && (now - u.LastActiveAt).TotalMinutes <= 10
            });

            return Ok(result);
        }

        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound("User not found");

            try
            {
                if (!int.TryParse(id, out int numericId)) return BadRequest("Invalid user ID");

                string? deviceFingerprintToClear = null;

                var childProfile = await _context.ChildProfiles.FirstOrDefaultAsync(c => c.UserId == numericId);
                if (childProfile != null)
                {
                    deviceFingerprintToClear = childProfile.DeviceFingerprint;
                    var childLogs = _context.DailyLogEntries.Where(l => l.ChildId == childProfile.UserId);
                    _context.DailyLogEntries.RemoveRange(childLogs);
                    _context.ChildProfiles.Remove(childProfile);
                }

                var parentProfile = await _context.ParentProfiles.FirstOrDefaultAsync(p => p.UserId == numericId);
                if (parentProfile != null)
                {
                    var hasChildren = await _context.ChildProfiles.AnyAsync(c => c.ParentId == parentProfile.UserId);
                    if (hasChildren) return BadRequest("Parent account cannot be deleted because it is linked to active child accounts.");
                    _context.ParentProfiles.Remove(parentProfile);
                }

                var deleteResult = await _userManager.DeleteAsync(user);
                if (!deleteResult.Succeeded)
                    return StatusCode(500, "Failed to delete main account");

                await _context.SaveChangesAsync();

                _cache.RemoveByPrefix(CacheKeys.AdminPrefix);
                _cache.RemoveByPrefix(CacheKeys.ParentPrefix(numericId));
                _cache.RemoveByPrefix(CacheKeys.ChildPrefix(numericId));

                if (!string.IsNullOrEmpty(deviceFingerprintToClear))
                    _cache.Remove($"device:{deviceFingerprintToClear}:children");

                return Ok(new { message = "User deleted successfully" });
            }
            catch (Exception ex) { return StatusCode(500, $"Internal error: {ex.Message}"); }
        }

        #endregion

        #region System Integrity

        // GET /api/Admin/integrity
        // Runs SystemIntegrityValidator — read-only, never mutates data.
        [HttpGet("integrity")]
        public async Task<IActionResult> RunIntegrityCheck(CancellationToken ct)
        {
            var report = await _integrityValidator.ValidateAsync(ct);

            return Ok(new
            {
                isHealthy = report.IsHealthy,
                runAt = report.RunAt,
                totalChecks = report.TotalChecks,
                violationCount = report.ViolationCount,
                violations = report.Violations.Select(v => new
                {
                    rule = v.Rule,
                    message = v.Message,
                    childId = v.ChildId,
                    entityId = v.EntityId
                })
            });
        }

        #endregion

        #region Personality Clusters (Phase 12 — AI Output)

        // ─────────────────────────────────────────────────────────────
        // GET /api/Admin/personality-clusters
        // List of all AI-defined cluster catalog entries, with the
        // denormalized MemberCount bookkeeping field kept up to date by
        // PeriodicClusteringOrchestrationService (not re-clustering — just
        // a COUNT refresh; cluster membership itself is AI-written only).
        // ─────────────────────────────────────────────────────────────
        [HttpGet("personality-clusters")]
        public async Task<IActionResult> GetPersonalityClusters()
        {
            var clusters = await _cache.GetOrCreateAsync(CacheKeys.AllPersonalityClusters, async () =>
                await _context.PersonalityClusters
                    .OrderByDescending(c => c.MemberCount)
                    .Select(c => new PersonalityClusterAdminDto
                    {
                        Id = c.Id,
                        ClusterKey = c.ClusterKey,
                        Name = c.Name,
                        Description = c.Description,
                        DominantTraits = c.DominantTraits,
                        Characteristics = c.Characteristics,
                        RecommendedFocus = c.RecommendedFocus,
                        MemberCount = c.MemberCount,
                        LastComputedAt = c.LastComputedAt
                    })
                    .ToListAsync()
            );

            return Ok(clusters);
        }

        // ─────────────────────────────────────────────────────────────
        // PUT /api/Admin/personality-clusters/{id}
        // Admin relabels/re-describes a cluster for human consumption.
        // Does NOT touch Characteristics (the AI-owned feature vector) —
        // mirrors the existing LeafyTip content-curation pattern.
        // ─────────────────────────────────────────────────────────────
        [HttpPut("personality-clusters/{id}")]
        public async Task<IActionResult> UpdatePersonalityCluster(int id, [FromBody] UpsertPersonalityClusterDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var cluster = await _context.PersonalityClusters.FindAsync(id);
            if (cluster is null) return NotFound("Cluster not found");

            if (!string.IsNullOrWhiteSpace(dto.Name))
                cluster.Name = dto.Name;
            cluster.Description = dto.Description ?? cluster.Description;

            await _context.SaveChangesAsync();
            _cache.Remove(CacheKeys.AllPersonalityClusters);

            return Ok(new { message = "Personality cluster updated." });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Admin/personality-clusters/{id}/children
        // Drill-down: every child currently assigned to this cluster.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("personality-clusters/{id}/children")]
        public async Task<IActionResult> GetPersonalityClusterMembers(int id)
        {
            var clusterExists = await _context.PersonalityClusters.AnyAsync(c => c.Id == id);
            if (!clusterExists) return NotFound("Cluster not found");

            var members = await _context.AdminChildrenPersonalityClusters
                .Include(a => a.Child).ThenInclude(c => c.User)
                .Where(a => a.PersonalityClusterId == id && a.IsCurrent)
                .OrderByDescending(a => a.SimilarityScore)
                .Select(a => new ClusterMemberDto
                {
                    ChildId = a.ChildId,
                    FullName = a.Child.User != null ? a.Child.User.FullName : "Unknown",
                    Age = a.Child.Age,
                    SimilarityScore = a.SimilarityScore,
                    AssignedAt = a.AssignedAt
                })
                .ToListAsync();

            return Ok(members);
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Admin/children/{childId}/personality-cluster
        // Current + historical cluster assignments for one child
        // (admin child-detail drill-down).
        // ─────────────────────────────────────────────────────────────
        [HttpGet("children/{childId}/personality-cluster")]
        public async Task<IActionResult> GetChildPersonalityClusterHistory(int childId)
        {
            var childExists = await _context.ChildProfiles.AnyAsync(c => c.UserId == childId);
            if (!childExists) return NotFound("Child not found");

            var history = await _context.AdminChildrenPersonalityClusters
                .Include(a => a.Cluster)
                .Where(a => a.ChildId == childId)
                .OrderByDescending(a => a.AssignedAt)
                .Select(a => new PersonalityClusterResultDto
                {
                    ChildId = a.ChildId,
                    PersonalityClusterId = a.PersonalityClusterId,
                    ClusterKey = a.Cluster.ClusterKey,
                    ClusterName = a.Cluster.Name,
                    Description = a.Cluster.Description,
                    DominantTraits = a.Cluster.DominantTraits,
                    RecommendedFocus = a.Cluster.RecommendedFocus,
                    SimilarityScore = a.SimilarityScore,
                    AssignedAt = a.AssignedAt
                })
                .ToListAsync();

            return Ok(new { childId, count = history.Count, history });
        }

        #endregion

        #region Children Overview — Final Pillars + Cluster (Phase 13)

        // ─────────────────────────────────────────────────────────────
        // GET /api/Admin/children
        // Full list of all children with Final Hybrid Pillars, NLP
        // Pillars, current level, badge count, and personality cluster.
        // Used by the admin dashboard "Children" section.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("children")]
        public async Task<IActionResult> GetAllChildrenOverview()
        {
            var profiles = await _context.ChildProfiles
                .Include(cp => cp.User)
                .Include(cp => cp.LastPersonalityCluster)
                .ToListAsync();

            var childIds = profiles.Select(p => p.UserId).ToList();

            // Badge counts per child
            var badgeCounts = await _context.ChildBadges
                .Where(cb => childIds.Contains(cb.ChildId) && cb.IsActive)
                .GroupBy(cb => cb.ChildId)
                .Select(g => new { ChildId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ChildId, x => x.Count);

            // Current levels per child
            var userLevels = await _context.UserLevels
                .Where(ul => childIds.Contains(ul.ChildId))
                .ToDictionaryAsync(ul => ul.ChildId);

            var result = profiles.Select(cp => new AdminChildOverviewDto
            {
                ChildId = cp.UserId,
                FullName = cp.User?.FullName ?? "–",
                Age = cp.Age,
                Level = userLevels.TryGetValue(cp.UserId, out var ul) ? ul.LevelNumber : cp.Level,
                BadgeCount = badgeCounts.TryGetValue(cp.UserId, out var bc) ? bc : 0,
                TrustScore = cp.TrustScore,
                MeanScore = Math.Round(cp.MeanScore, 1),
                FinalMeanScore = Math.Round(cp.FinalMeanScore, 1),

                // ── 8 Structured Pillars ────────────────────────────
                SpiritualPct = Math.Round(cp.SpiritualConnection_Pct, 1),
                SocialPct = Math.Round(cp.SocialInteraction_Pct, 1),
                AcademicPct = Math.Round(cp.AcademicProgress_Pct, 1),
                EnvPct = Math.Round(cp.HomeResponsibility_Pct, 1),
                PhysicalPct = Math.Round(cp.PhysicalHealth_Pct, 1),
                SportPct = Math.Round(cp.PhysicalActivity_Pct, 1),
                CreaPct = Math.Round(cp.CreativityEngagement_Pct, 1),
                MentalPct = Math.Round(cp.MentalWellbeing_Pct, 1),

                // ── 8 NLP Pillars ───────────────────────────────────
                NlpSpiritualPct = Math.Round(cp.NlpSpiritualConnection_Pct, 1),
                NlpSocialPct = Math.Round(cp.NlpSocialInteraction_Pct, 1),
                NlpAcademicPct = Math.Round(cp.NlpAcademicProgress_Pct, 1),
                NlpEnvPct = Math.Round(cp.NlpHomeResponsibility_Pct, 1),
                NlpPhysicalPct = Math.Round(cp.NlpPhysicalHealth_Pct, 1),
                NlpSportPct = Math.Round(cp.NlpPhysicalActivity_Pct, 1),
                NlpCreaPct = Math.Round(cp.NlpCreativityEngagement_Pct, 1),
                NlpMentalPct = Math.Round(cp.NlpMentalWellbeing_Pct, 1),

                // ── 8 Final Hybrid Pillars (70% Structured + 30% NLP) ─
                FinalSpiritualPct = Math.Round(cp.FinalSpiritualConnection_Pct, 1),
                FinalSocialPct = Math.Round(cp.FinalSocialInteraction_Pct, 1),
                FinalAcademicPct = Math.Round(cp.FinalAcademicProgress_Pct, 1),
                FinalEnvPct = Math.Round(cp.FinalHomeResponsibility_Pct, 1),
                FinalPhysicalPct = Math.Round(cp.FinalPhysicalHealth_Pct, 1),
                FinalSportPct = Math.Round(cp.FinalPhysicalActivity_Pct, 1),
                FinalCreaPct = Math.Round(cp.FinalCreativityEngagement_Pct, 1),
                FinalMentalPct = Math.Round(cp.FinalMentalWellbeing_Pct, 1),

                // ── Personality Cluster ─────────────────────────────
                PersonalityClusterName = cp.LastPersonalityCluster?.Name,
                PersonalityClusterKey = cp.LastPersonalityCluster?.ClusterKey,
                PersonalityClusterDominantTraits = cp.LastPersonalityCluster?.DominantTraits,
                PersonalityClusterAssignedAt = cp.PersonalityClusterAssignedAt,

                // ── AI Recommendation Pointers ──────────────────────
                RecommendedBadgeId = cp.RecommendedBadgeId,
                RecommendedLeafyTipId = cp.RecommendedLeafyTipId,
                LeafyAlertText = cp.LeafyAlertText,
                LeafyTextSetAt = cp.LeafyTextSetAt,

                LastAggregatedAt = cp.LastAggregatedAt
            }).ToList();

            return Ok(new { count = result.Count, children = result });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Admin/children/{childId}/detail
        // Single child — full pillar breakdown (Structured + NLP + Final)
        // plus cluster history and badge list.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("children/{childId}/detail")]
        public async Task<IActionResult> GetChildDetail(int childId)
        {
            var cp = await _context.ChildProfiles
                .Include(p => p.User)
                .Include(p => p.LastPersonalityCluster)
                .Include(p => p.RecommendedBadge)
                .FirstOrDefaultAsync(p => p.UserId == childId);

            if (cp is null) return NotFound("Child not found.");

            // Cluster history
            var clusterHistory = await _context.AdminChildrenPersonalityClusters
                .Include(a => a.Cluster)
                .Where(a => a.ChildId == childId)
                .OrderByDescending(a => a.AssignedAt)
                .Take(5)
                .Select(a => new
                {
                    clusterKey = a.Cluster.ClusterKey,
                    clusterName = a.Cluster.Name,
                    similarity = a.SimilarityScore,
                    isCurrent = a.IsCurrent,
                    assignedAt = a.AssignedAt
                })
                .ToListAsync();

            // Earned badges
            var badges = await _context.ChildBadges
                .Include(cb => cb.Badge)
                .Where(cb => cb.ChildId == childId && cb.IsActive)
                .OrderByDescending(cb => cb.UnlockedAt)
                .Select(cb => new { cb.BadgeId, cb.Badge!.Name, cb.UnlockedAt })
                .ToListAsync();

            // Current level
            var userLevel = await _context.UserLevels
                .Include(ul => ul.Level)
                .FirstOrDefaultAsync(ul => ul.ChildId == childId);

            return Ok(new
            {
                childId = cp.UserId,
                fullName = cp.User?.FullName ?? "–",
                age = cp.Age,
                trustScore = cp.TrustScore,
                level = new
                {
                    levelNumber = userLevel?.LevelNumber ?? cp.Level,
                    levelName = userLevel?.Level?.Name ?? "–",
                    currentExp = userLevel?.CurrentExp ?? cp.CurrentExp,
                    totalExp = userLevel?.TotalExp ?? 0
                },
                badgeCount = badges.Count,
                badges,
                pillars = new
                {
                    structured = new
                    {
                        spiritual = Math.Round(cp.SpiritualConnection_Pct, 1),
                        social = Math.Round(cp.SocialInteraction_Pct, 1),
                        academic = Math.Round(cp.AcademicProgress_Pct, 1),
                        env = Math.Round(cp.HomeResponsibility_Pct, 1),
                        physical = Math.Round(cp.PhysicalHealth_Pct, 1),
                        sport = Math.Round(cp.PhysicalActivity_Pct, 1),
                        creativity = Math.Round(cp.CreativityEngagement_Pct, 1),
                        mental = Math.Round(cp.MentalWellbeing_Pct, 1),
                        mean = Math.Round(cp.MeanScore, 1)
                    },
                    nlp = new
                    {
                        spiritual = Math.Round(cp.NlpSpiritualConnection_Pct, 1),
                        social = Math.Round(cp.NlpSocialInteraction_Pct, 1),
                        academic = Math.Round(cp.NlpAcademicProgress_Pct, 1),
                        env = Math.Round(cp.NlpHomeResponsibility_Pct, 1),
                        physical = Math.Round(cp.NlpPhysicalHealth_Pct, 1),
                        sport = Math.Round(cp.NlpPhysicalActivity_Pct, 1),
                        creativity = Math.Round(cp.NlpCreativityEngagement_Pct, 1),
                        mental = Math.Round(cp.NlpMentalWellbeing_Pct, 1)
                    },
                    final = new
                    {
                        spiritual = Math.Round(cp.FinalSpiritualConnection_Pct, 1),
                        social = Math.Round(cp.FinalSocialInteraction_Pct, 1),
                        academic = Math.Round(cp.FinalAcademicProgress_Pct, 1),
                        env = Math.Round(cp.FinalHomeResponsibility_Pct, 1),
                        physical = Math.Round(cp.FinalPhysicalHealth_Pct, 1),
                        sport = Math.Round(cp.FinalPhysicalActivity_Pct, 1),
                        creativity = Math.Round(cp.FinalCreativityEngagement_Pct, 1),
                        mental = Math.Round(cp.FinalMentalWellbeing_Pct, 1),
                        mean = Math.Round(cp.FinalMeanScore, 1)
                    }
                },
                personalityCluster = new
                {
                    current = cp.LastPersonalityCluster == null ? null : new
                    {
                        clusterKey = cp.LastPersonalityCluster.ClusterKey,
                        clusterName = cp.LastPersonalityCluster.Name,
                        description = cp.LastPersonalityCluster.Description,
                        dominantTraits = cp.LastPersonalityCluster.DominantTraits,
                        recommendedFocus = cp.LastPersonalityCluster.RecommendedFocus,
                        assignedAt = cp.PersonalityClusterAssignedAt
                    },
                    history = clusterHistory
                },
                aiOutputs = new
                {
                    recommendedBadgeId = cp.RecommendedBadgeId,
                    recommendedBadgeName = cp.RecommendedBadge?.Name,
                    recommendedLeafyTipId = cp.RecommendedLeafyTipId,
                    leafyTipText = cp.LeafyTipText,
                    leafyMessageText = cp.LeafyMessageText,
                    leafyAlertText = cp.LeafyAlertText,
                    leafyTextSetAt = cp.LeafyTextSetAt
                },
                lastAggregatedAt = cp.LastAggregatedAt
            });
        }

        #endregion
    }
}
using GrawiaaApp.API.Data;
using GrawiaaApp.API.DTOs;
using GrawiaaApp.API.Models;
using GrawiaaApp.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GrawiaaApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RewardsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICacheService _cache;

        public RewardsController(ApplicationDbContext context, ICacheService cache)
        {
            _context = context;
            _cache = cache;
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/Rewards/add  [Parent]
        // الأب يضيف مكافأة للطفل
        // ─────────────────────────────────────────────────────────────
        [Authorize(Roles = "Parent")]
        [HttpPost("add")]
        public async Task<IActionResult> AddReward([FromBody] AddRewardDto dto)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int parentId))
                return Unauthorized("Invalid token");

            var childProfile = await _context.ChildProfiles
                .FirstOrDefaultAsync(cp => cp.UserId == dto.ChildId && cp.ParentId == parentId);
            if (childProfile == null) return Forbid();

            var reward = new Reward
            {
                ChildId = dto.ChildId,
                ParentId = parentId,
                Title = dto.Title,
                // FIX CS8601: dto.Description is nullable — default to empty string
                Description = dto.Description ?? string.Empty,
                Cost = dto.Cost,
                IsRedeemed = false,
                ParentApproved = true
            };

            _context.Rewards.Add(reward);
            await _context.SaveChangesAsync();

            _context.Notifications.Add(new Notification
            {
                ReceiverId = dto.ChildId,
                Message = $"🎁 New reward available: {dto.Title} ({dto.Cost} pts)",
                IsUrgent = false,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            _cache.RemoveByPrefix(CacheKeys.ChildPrefix(dto.ChildId));

            return Ok(new { message = "Reward added!", rewardId = reward.Id });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Rewards/my-rewards  [Child]
        // الطفل يشوف مكافآته
        // ─────────────────────────────────────────────────────────────
        [Authorize(Roles = "Child")]
        [HttpGet("my-rewards")]
        public async Task<IActionResult> GetMyRewards()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int childId))
                return Unauthorized("Invalid token");

            var rewards = await _context.Rewards
                .Where(r => r.ChildId == childId)
                .Select(r => new
                {
                    r.Id,
                    r.Title,
                    r.Description,
                    r.Cost,
                    r.IsRedeemed,
                    r.ParentApproved,
                    approvedAt = r.ApprovedAt.HasValue
                                 ? r.ApprovedAt.Value.ToString("yyyy-MM-dd")
                                 : null
                })
                .ToListAsync();

            return Ok(rewards);
        }

        // ─────────────────────────────────────────────────────────────
        // PATCH /api/Rewards/{id}/redeem  [Child]
        //
        // BUG FIX (Phase 14 Audit): IsRedeemed was previously write-only —
        // AddReward always set it to false, GetMyRewards/GetChildRewards
        // only ever read it, and heroprof.html's reward grid filters
        // `!isRedeemed` to decide which rewards are still "active" — but
        // there was no endpoint anywhere in the backend that ever set it
        // to true. A child could see an approved reward forever and never
        // had a way to mark it claimed; it would simply stay in the
        // active list indefinitely.
        //
        // This only flips the existing IsRedeemed flag (no new DB column,
        // no migration — the production database is locked per project
        // convention elsewhere in this codebase). It does not deduct any
        // points/currency: Reward.Cost is informational only here, matching
        // the existing design where a parent manually creates + approves
        // each reward as an incentive rather than running a strict point-
        // shop economy (CurrentExp already has a separate purpose — gating
        // level-ups — and repurposing it as spendable currency here would
        // conflict with that).
        // ─────────────────────────────────────────────────────────────
        [Authorize(Roles = "Child")]
        [HttpPatch("{id}/redeem")]
        public async Task<IActionResult> RedeemReward(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int childId))
                return Unauthorized("Invalid token");

            var reward = await _context.Rewards
                .FirstOrDefaultAsync(r => r.Id == id && r.ChildId == childId);

            if (reward == null) return NotFound("Reward not found");
            if (!reward.ParentApproved) return BadRequest(new { message = "This reward hasn't been approved by your parent yet." });
            if (reward.IsRedeemed) return BadRequest(new { message = "This reward has already been redeemed." });

            reward.IsRedeemed = true;
            await _context.SaveChangesAsync();

            if (reward.ParentId.HasValue)
            {
                var childUser = await _context.Users.FindAsync(childId);
                var childName = childUser?.FullName ?? "Your child";

                _context.Notifications.Add(new Notification
                {
                    ReceiverId = reward.ParentId.Value,
                    Message = $"🎁 {childName} redeemed the reward: \"{reward.Title}\"!",
                    IsUrgent = false,
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            _cache.RemoveByPrefix(CacheKeys.ChildPrefix(childId));

            return Ok(new { message = "Reward redeemed! 🎉", rewardId = reward.Id });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Rewards/parent-view/{childId}  [Parent]
        // الأب يشوف مكافآت طفله
        // ─────────────────────────────────────────────────────────────
        [Authorize(Roles = "Parent")]
        [HttpGet("parent-view/{childId}")]
        public async Task<IActionResult> GetChildRewards(int childId)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int parentId))
                return Unauthorized("Invalid token");

            var isHisChild = await _context.ChildProfiles
                .AnyAsync(cp => cp.UserId == childId && cp.ParentId == parentId);
            if (!isHisChild) return Forbid();

            var rewards = await _context.Rewards
                .Where(r => r.ChildId == childId)
                .Select(r => new
                {
                    r.Id,
                    r.Title,
                    r.Description,
                    r.Cost,
                    r.IsRedeemed,
                    r.ParentApproved,
                    approvedAt = r.ApprovedAt.HasValue
                                 ? r.ApprovedAt.Value.ToString("yyyy-MM-dd")
                                 : null
                })
                .ToListAsync();

            return Ok(rewards);
        }

        // ─────────────────────────────────────────────────────────────
        // DELETE /api/Rewards/{id}  [Parent]
        // الأب يمسح مكافأة
        // ─────────────────────────────────────────────────────────────
        [Authorize(Roles = "Parent")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReward(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int parentId))
                return Unauthorized("Invalid token");

            var reward = await _context.Rewards
                .FirstOrDefaultAsync(r => r.Id == id && r.ParentId == parentId);
            if (reward == null) return NotFound("Reward not found");

            _context.Rewards.Remove(reward);
            await _context.SaveChangesAsync();

            _cache.RemoveByPrefix(CacheKeys.ChildPrefix(reward.ChildId));

            return Ok(new { message = "Reward removed." });
        }
    }
}
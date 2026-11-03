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
    [Authorize(Roles = "Parent")]
    [Route("api/[controller]")]
    [ApiController]
    public class ParentController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICacheService _cache;
        private readonly IAlertGenerationService _alertService;
        private readonly IChildIdentityService _childIdentity;

        public ParentController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ICacheService cache,
            IAlertGenerationService alertService,
            IChildIdentityService childIdentity)
        {
            _context = context;
            _userManager = userManager;
            _cache = cache;
            _alertService = alertService;
            _childIdentity = childIdentity;
        }

        private int? GetCurrentParentId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int parentId)) return null;
            return parentId;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Phase 4 — GET /api/Parent/hero-alerts/{heroId}
        // Returns all active pillar alerts for a specific child.
        // Severity: Low / Medium / Critical (no alert when pillar >= 55%).
        // ─────────────────────────────────────────────────────────────────────────
        [HttpGet("hero-alerts/{heroId}")]
        public async Task<IActionResult> GetHeroAlerts(int heroId)
        {
            var parentId = GetCurrentParentId();
            if (!parentId.HasValue) return Unauthorized();

            // Ensure the child belongs to this parent
            var child = await _context.ChildProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(cp => cp.UserId == heroId && cp.ParentId == parentId.Value);

            if (child == null) return NotFound("Hero not found or not linked to this parent.");

            var alerts = await _alertService.GenerateAlertsAsync(heroId);
            return Ok(alerts);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Phase 4 — GET /api/Parent/hero-alert-summary/{heroId}
        // Returns aggregated alert summary (counts + overall status + full list).
        // ─────────────────────────────────────────────────────────────────────────
        [HttpGet("hero-alert-summary/{heroId}")]
        public async Task<IActionResult> GetHeroAlertSummary(int heroId)
        {
            var parentId = GetCurrentParentId();
            if (!parentId.HasValue) return Unauthorized();

            var child = await _context.ChildProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(cp => cp.UserId == heroId && cp.ParentId == parentId.Value);

            if (child == null) return NotFound("Hero not found or not linked to this parent.");

            var summary = await _alertService.GenerateAlertSummaryAsync(heroId);
            return Ok(summary);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Fix 3 — GET /api/Parent/emotion-safety/{heroId}
        //
        // Returns this child's current Emotion Safety alert state for the
        // parent dashboard. The AI team's Emotion Safety Engine writes
        // DailyLogEntry.EmotionSafetyAlertFlag/Text directly via SQL on
        // each day's row (see DailyLogEntry.cs — column name pending
        // confirmation with the AI team, see the UNCONFIRMED COLUMN NAME
        // note there). NlpPillarSyncService promotes that per-entry flag
        // up to ChildProfile.HasActiveEmotionSafetyAlert /
        // LastEmotionSafetyAlertAt on every tick (plus a separate
        // staleness pass — see NlpPillarSyncService.ClearStaleEmotionSafetyFlagsAsync)
        // so this endpoint only needs a single ChildProfile read, not a
        // DailyLogEntries scan, to answer "is there an active concern?".
        //
        // alertText is read fresh from the child's latest saved entry
        // (not cached on ChildProfile) since the free-text message is
        // per-entry, not a profile-level field.
        // ─────────────────────────────────────────────────────────────────────────
        [HttpGet("emotion-safety/{heroId}")]
        public async Task<IActionResult> GetEmotionSafety(int heroId)
        {
            var parentId = GetCurrentParentId();
            if (!parentId.HasValue) return Unauthorized();

            var child = await _context.ChildProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(cp => cp.UserId == heroId && cp.ParentId == parentId.Value);

            if (child == null) return NotFound("Hero not found or not linked to this parent.");

            string? alertText = null;
            if (child.HasActiveEmotionSafetyAlert)
            {
                var latestEntry = await _context.DailyLogEntries
                    .AsNoTracking()
                    .Where(e => e.ChildId == heroId && e.IsJournalSaved)
                    .OrderByDescending(e => e.LogDate)
                    .FirstOrDefaultAsync();

                alertText = latestEntry?.EmotionSafetyAlertText;
            }

            return Ok(new
            {
                heroId,
                hasActiveAlert = child.HasActiveEmotionSafetyAlert,
                alertText,
                lastAlertAt = child.LastEmotionSafetyAlertAt
            });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Phase 4 — GET /api/Parent/dashboard-alerts
        // Returns a combined alert summary for ALL children of this parent.
        // Used by the parent's main dashboard panel.
        // ─────────────────────────────────────────────────────────────────────────
        [HttpGet("dashboard-alerts")]
        public async Task<IActionResult> GetDashboardAlerts()
        {
            var parentId = GetCurrentParentId();
            if (!parentId.HasValue) return Unauthorized();

            var childIds = await _context.ChildProfiles
                .AsNoTracking()
                .Where(cp => cp.ParentId == parentId.Value)
                .Select(cp => cp.UserId)
                .ToListAsync();

            var allSummaries = new List<object>();
            foreach (var childId in childIds)
            {
                var summary = await _alertService.GenerateAlertSummaryAsync(childId);
                allSummaries.Add(summary);
            }

            return Ok(new
            {
                parentId = parentId.Value,
                childCount = childIds.Count,
                summaries = allSummaries,
                generatedAt = DateTime.UtcNow
            });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // NEW: GET /api/Parent/profile
        // Returns the logged-in parent's name and email so the dashboard greeting
        // can display the real name instead of the static "Hi, Parent!" placeholder.
        // ─────────────────────────────────────────────────────────────────────────
        [HttpGet("profile")]
        public async Task<IActionResult> GetParentProfile()
        {
            var parentId = GetCurrentParentId();
            if (!parentId.HasValue) return Unauthorized();

            var user = await _userManager.FindByIdAsync(parentId.Value.ToString());
            if (user == null) return NotFound(new { message = "Parent not found" });

            return Ok(new
            {
                id = user.Id,
                fullName = user.FullName ?? "Parent",
                email = user.Email ?? ""
            });
        }

        [HttpPatch("profile")]
        public async Task<IActionResult> UpdateParentProfile([FromBody] UpdateParentProfileDto dto)
        {
            var parentId = GetCurrentParentId();
            if (!parentId.HasValue) return Unauthorized();

            var user = await _userManager.FindByIdAsync(parentId.Value.ToString());
            if (user == null) return NotFound(new { message = "Parent not found" });

            if (!string.IsNullOrWhiteSpace(dto.FullName))
                user.FullName = dto.FullName;

            if (!string.IsNullOrWhiteSpace(dto.Email) && dto.Email != user.Email)
            {
                var emailTaken = await _userManager.FindByEmailAsync(dto.Email);
                if (emailTaken != null && emailTaken.Id != user.Id)
                    return BadRequest(new { message = "This email is already registered to another account." });
                user.Email = dto.Email;
                user.UserName = dto.Email;
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(new { message = result.Errors.First().Description });

            _cache.RemoveByPrefix(CacheKeys.ParentPrefix(parentId.Value));
            return Ok(new { message = "Profile updated successfully!" });
        }

        [HttpGet("main-dashboard")]
        public async Task<IActionResult> GetMainDashboard()
        {
            var parentId = GetCurrentParentId();
            if (!parentId.HasValue) return Unauthorized();

            var result = await _cache.GetOrCreateAsync(CacheKeys.ParentDashboard(parentId.Value), async () =>
            {
                var children = await _context.Users
                    .Include(u => u.ChildProfile)
                    .Where(u => u.ParentId == parentId.Value && u.UserType == "Child")
                    .ToListAsync();

                var today = DateTime.UtcNow.Date;
                var childIds = children.Select(c => c.Id).ToList();

                // FIX: جلب عدد أيام الصدق (IsHonest=true) لكل طفل ديناميكياً
                var honestCountsRaw = await _context.DailyLogEntries
                    .Where(l => childIds.Contains(l.ChildId) && l.IsHonest)
                    .GroupBy(l => l.ChildId)
                    .Select(g => new { ChildId = g.Key, Count = g.Count() })
                    .ToListAsync();
                var honestCounts = honestCountsRaw.ToDictionary(x => x.ChildId, x => x.Count);

                var heroes = children.Select(u => new
                {
                    id = u.Id,
                    fullName = u.FullName ?? "Hero",
                    age = u.ChildProfile?.Age ?? 0,
                    level = u.ChildProfile?.Level ?? 1,
                    characterType = u.ChildProfile?.CharacterType ?? "Explorer",
                    trustScore = u.ChildProfile?.TrustScore ?? 100,
                    // FIX: honestDays = أيام الصدق الفعلية من اليوميات
                    honestDays = honestCounts.TryGetValue(u.Id, out var hc) ? hc : 0,
                    academicPct = u.ChildProfile?.AcademicPct ?? 0,
                    mentalPct = u.ChildProfile?.MentalPct ?? 0,
                    socialPct = u.ChildProfile?.SocialPct ?? 0,
                    religiousPct = u.ChildProfile?.ReligiousPct ?? 0,
                    physicalPct = u.ChildProfile?.PhysicalPct ?? 0,
                    // FIX P2: إضافة الـ 5 Traits الفعلية حتى يعرضها parent-dashboard.html بدقة
                    kindnessTrait = u.ChildProfile?.KindnessTrait ?? 0,
                    vitalityTrait = u.ChildProfile?.VitalityTrait ?? 0,
                    curiosityTrait = u.ChildProfile?.CuriosityTrait ?? 0,
                    resilienceTrait = u.ChildProfile?.ResilienceTrait ?? 0,
                    spiritualTrait = u.ChildProfile?.SpiritualTrait ?? 0,
                    isDoubtState = u.ChildProfile != null && u.ChildProfile.TrustScore < 70,
                    // AUDIT FIX: the AI team's Emotion Safety Engine output
                    // (promoted to ChildProfile.HasActiveEmotionSafetyAlert by
                    // NlpPillarSyncService — see DailyLogEntry.cs / ParentController's
                    // existing GET /emotion-safety/{heroId}) was computed correctly
                    // end-to-end but never exposed on the main dashboard, so a
                    // parent had no way to discover an active concern unless they
                    // already knew to check a per-child endpoint that no page calls.
                    // Surfacing the flag here so the hero card can show an indicator.
                    hasActiveEmotionSafetyAlert = u.ChildProfile?.HasActiveEmotionSafetyAlert ?? false
                }).ToList<object>();

                var todayLogChildIds = await _context.DailyLogEntries
                    .Where(l => childIds.Contains(l.ChildId) && l.LogDate.Date == today)
                    .Select(l => l.ChildId)
                    .ToListAsync();

                var alerts = new List<object>();
                foreach (var child in children)
                {
                    // AUDIT FIX: highest-priority alert — an active Emotion Safety
                    // concern flagged by the AI team takes precedence over the
                    // routine trust/check-in alerts below, and was previously
                    // never added to this list at all.
                    if (child.ChildProfile?.HasActiveEmotionSafetyAlert == true)
                        alerts.Add(new { message = $"💛 {child.FullName} may need extra support right now — check their wellbeing.", type = "EmotionSafety" });

                    if (child.ChildProfile?.TrustScore < 70)
                        alerts.Add(new { message = $"⚠️ {child.FullName} needs a trust-building talk (Low Trust).", type = "Doubt" });

                    var hasTodayLogs = todayLogChildIds.Contains(child.Id);

                    if (hasTodayLogs)
                        alerts.Add(new { message = $"🌟 {child.FullName} updated their garden today! Check their progress.", type = "Success" });
                    else
                        alerts.Add(new { message = $"🔔 {child.FullName} hasn't checked in yet. Time for a reminder?", type = "Message" });
                }

                if (!alerts.Any())
                    alerts.Add(new { message = "All heroes are doing great today! ✨", type = "Success" });

                return new { heroes, alerts };
            }, TimeSpan.FromMinutes(5));

            return Ok(result);
        }


        [HttpGet("hero-insights/{heroId}")]
        public async Task<IActionResult> GetHeroInsights(int heroId)
        {
            var parentId = GetCurrentParentId();
            if (!parentId.HasValue) return Unauthorized();

            var childProfile = await _context.ChildProfiles
                .Include(cp => cp.User)
                .ThenInclude(u => u.Logs)
                .Include(cp => cp.RecommendedBadge)
                .Include(cp => cp.RecommendedLeafyTip)
                .Include(cp => cp.LastPersonalityCluster)
                .FirstOrDefaultAsync(cp => cp.UserId == heroId && cp.ParentId == parentId.Value);

            if (childProfile == null) return NotFound("Hero not found");

            // حساب عدد أيام الصدق الكلية للطفل
            var honestDaysCount = await _context.DailyLogEntries
                .Where(l => l.ChildId == heroId && l.IsHonest)
                .CountAsync();

            var weekAgo = DateTime.UtcNow.Date.AddDays(-7);
            var logs = childProfile.User?.Logs
                .Where(l => l.LogDate.Date >= weekAgo)
                .OrderBy(l => l.LogDate).ToList() ?? new List<DailyLogEntry>();

            var trends = new List<string>();
            if (logs.Any(l => !string.IsNullOrEmpty(l.CompletedSubjects) || !string.IsNullOrEmpty(l.LearningNote))) trends.Add("Reading");
            if (logs.Any(l => !string.IsNullOrEmpty(l.CompletedSubjects) || !string.IsNullOrEmpty(l.LearningNote))) trends.Add("Math Practice");
            if (logs.Any(l => !string.IsNullOrEmpty(l.PhysicalActivity) || !string.IsNullOrEmpty(l.ActivityNote))) trends.Add("Physical Activity");
            if (logs.Any(l => !string.IsNullOrEmpty(l.CompletedChores) || l.HelpedFamily)) trends.Add("Chore Completion");
            if (logs.Any(l => l.SleepHours >= 7)) trends.Add("Good Sleep");

            // حسابات النشاط والمشاعر
            // CATEGORIES FIX v3: متوسط أداء حقيقي من آخر 7 أيام فقط
            // consistent مع باقي عناصر الـ Weekly Summary card.
            // بنحسب average من الـ logs اللي أصلاً محدودة بـ weekAgo (7 أيام).
            // كل قيمة (0-100) مستقلة وبتمثل متوسط أداء الطفل في المجال ده خلال الأسبوع.
            var logsWithPillars = logs.Where(l => l.DailyPhysicalActivity_Pct + l.DailyAcademicProgress_Pct + l.DailyHomeResponsibility_Pct > 0).ToList();

            double sportsScore, studyScore, choresScore;
            if (logsWithPillars.Any())
            {
                // المسار الأساسي: متوسط الـ Daily pillar scores من آخر 7 أيام
                sportsScore = logsWithPillars.Average(l => l.DailyPhysicalActivity_Pct);
                studyScore = logsWithPillars.Average(l => l.DailyAcademicProgress_Pct);
                choresScore = logsWithPillars.Average(l => l.DailyHomeResponsibility_Pct);
            }
            else if (logs.Any())
            {
                // Fallback: لو الـ daily pillars لسه ما اتحسبتش — نحسب من presence
                sportsScore = logs.Count(l => !string.IsNullOrEmpty(l.PhysicalActivity)) * 100.0 / logs.Count;
                studyScore = logs.Count(l => !string.IsNullOrEmpty(l.CompletedSubjects) || !string.IsNullOrEmpty(l.LearningNote)) * 100.0 / logs.Count;
                choresScore = logs.Count(l => !string.IsNullOrEmpty(l.CompletedChores) || l.HelpedFamily) * 100.0 / logs.Count;
            }
            else
            {
                sportsScore = 0; studyScore = 0; choresScore = 0;
            }

            // totalActivityScore محتاجينه للـ redFlags fallback تحت
            var totalActivityScore = Math.Max(0.0001, sportsScore + studyScore + choresScore);

            var sentimentScores = logs.Where(l => l.SentimentScore.HasValue).Select(l => l.SentimentScore!.Value).ToList();
            var avgSentiment = sentimentScores.Any() ? sentimentScores.Average() : 1.0;

            // ── Sleep Analysis ────────────────────────────────────────────────────
            var logsWithSleep = logs.Where(l => l.SleepHours > 0).ToList();
            var avgSleepHours = logsWithSleep.Any() ? logsWithSleep.Average(l => l.SleepHours) : 0;
            var sleepAlert = avgSleepHours > 0 && avgSleepHours < 7;
            var criticalSleep = avgSleepHours > 0 && avgSleepHours < 5;

            var redFlags = new List<string>();
            var childName = childProfile.User?.FullName ?? "Your child";

            if (avgSentiment < 0.5) redFlags.Add($"{childName}'s mood has been lower than usual.");
            var last3Days = logs.OrderByDescending(l => l.LogDate).Take(3).ToList();
            // FIX: also check HelpedFamily (new field) — CompletedChores was already checked,
            // but a child can mark chores done via the boolean flag with no text list.
            if (last3Days.Any() && last3Days.All(l => string.IsNullOrEmpty(l.CompletedChores) && !l.HelpedFamily))
                redFlags.Add($"{childName} may be avoiding chores — check in with them.");
            if (sleepAlert)
                redFlags.Add($"{childName} is sleeping an average of {avgSleepHours:F1} hours/night (recommended: 7–9 hours).");

            // ── Real, performance-based alerts (driven by actual pillar scores) ──
            var pillarAlerts = await _alertService.GenerateAlertsAsync(heroId);

            var personalizedAlerts = new List<PersonalizedAlertDto>();

            // ── BUG FIX (audit 2026-06-21): Emotion Safety Engine alert ──────
            // EmotionSafetyAlertFlag/Text are written by the AI team (direct-DB)
            // but were never read anywhere — surfacing the most recent one in
            // this week's window here, same pattern as the other real alerts
            // below. Distinct from AnomalyFlag (Trust Engine, handled via
            // aiInsights.AnomalyDetected further down).
            //
            // AUDIT FIX (2026-06-21): reads the raw `Alert` column directly
            // rather than the derived EmotionSafetyAlertFlag. `Alert` is the
            // actual column the AI team's Emotion Safety Engine writes via
            // direct SQL; EmotionSafetyAlertFlag is only populated the next
            // time PillarPipelineService.ApplyDailyPillars runs on that entry
            // (journal save or the periodic NlpPillarSyncService tick), which
            // could lag a fresh direct-SQL write by up to the sync interval.
            // Reading `Alert` here means a parent sees a safety concern the
            // moment it's written, not after the next sync cycle.
            var emotionSafetyLog = logs
                .Where(l => !string.IsNullOrWhiteSpace(l.Alert))
                .OrderByDescending(l => l.LogDate)
                .FirstOrDefault();

            if (emotionSafetyLog != null)
                personalizedAlerts.Add(new PersonalizedAlertDto
                {
                    Severity = "Critical",
                    Emoji = "💛",
                    Message = string.IsNullOrWhiteSpace(emotionSafetyLog.Alert)
                        ? $"{childName}'s recent journal entry raised an emotional-safety concern."
                        : emotionSafetyLog.Alert!,
                    Suggestion = "This was flagged by Leafy's Emotion Safety check — consider a gentle, private check-in."
                });

            // Sleep alert first if critical — highest priority
            if (criticalSleep)
                personalizedAlerts.Add(new PersonalizedAlertDto
                {
                    Severity = "Critical",
                    Emoji = "😴",
                    Message = $"{childName} is getting only {avgSleepHours:F1} hrs/night — well below the 7-hour minimum.",
                    Suggestion = "Establish a consistent bedtime routine. Aim for lights-out by 9 PM."
                });
            else if (sleepAlert)
                personalizedAlerts.Add(new PersonalizedAlertDto
                {
                    Severity = "Medium",
                    Emoji = "😴",
                    Message = $"{childName} averages {avgSleepHours:F1} hrs of sleep — slightly below the recommended 7 hours.",
                    Suggestion = "Try moving bedtime 30 minutes earlier and reducing screen time before sleep."
                });

            // Add mood alert if sentiment is low
            if (avgSentiment < 0.5)
                personalizedAlerts.Add(new PersonalizedAlertDto
                {
                    Severity = avgSentiment < 0.3 ? "Critical" : "Medium",
                    Emoji = "💭",
                    Message = $"{childName}'s mood has been lower than usual this week.",
                    Suggestion = "Set aside quiet one-on-one time to check in — a short walk or chat can make a big difference."
                });

            // Top pillar alerts (Critical & Medium only — avoid noise)
            var importantPillarAlerts = pillarAlerts
                .Where(a => a.Severity == "Critical" || a.Severity == "Medium")
                .OrderByDescending(a => a.Severity == "Critical" ? 2 : 1)
                .Take(4)
                .Select(a => new PersonalizedAlertDto
                {
                    Severity = a.Severity,
                    Emoji = a.PillarEmoji ?? (a.Severity == "Critical" ? "🔴" : "🟡"),
                    Message = $"{childName}'s {a.PillarName} needs attention ({a.PillarScore:F0}%).",
                    Suggestion = a.Suggestion
                });
            personalizedAlerts.AddRange(importantPillarAlerts);

            // RedFlags also reflect real low-performing pillars for the Weekly Summary card.
            // FIX: previously added a flag for EVERY Critical/Medium pillar with no cap,
            // which could flood the card with 5-6+ flags. Now: Critical pillars first,
            // then Medium, capped at 3 pillar-based flags (on top of mood/chores/sleep
            // flags already added above) so the card stays scannable.
            const int maxPillarRedFlags = 3;
            foreach (var a in pillarAlerts
                .Where(a => a.Severity == "Critical" || a.Severity == "Medium")
                .OrderByDescending(a => a.Severity == "Critical" ? 1 : 0)
                .ThenBy(a => a.PillarScore)
                .Take(maxPillarRedFlags))
            {
                var flagText = $"{childName} may be falling behind on {a.PillarName} ({a.PillarScore:F0}%).";
                if (!redFlags.Contains(flagText)) redFlags.Add(flagText);
            }

            // ── Dynamic Activity Recommendations (child-specific, based on weak areas) ──
            var activityRecommendations = new List<ActivityRecommendationDto>();

            // Mood boost first if low sentiment
            if (avgSentiment < 0.5)
                activityRecommendations.Add(new ActivityRecommendationDto { Icon = "💛", Text = "Plan a fun family outing or movie night to boost morale" });

            // Sleep recommendation
            if (sleepAlert)
                activityRecommendations.Add(new ActivityRecommendationDto { Icon = "😴", Text = "Create a calm bedtime routine: no screens 30 min before sleep" });

            // Add personalized recs from top weak pillars (max 3)
            foreach (var a in pillarAlerts
                .Where(a => a.Severity == "Critical" || a.Severity == "Medium")
                .OrderByDescending(a => a.Severity == "Critical" ? 2 : 1)
                .Take(3))
            {
                activityRecommendations.Add(new ActivityRecommendationDto
                {
                    Icon = GetPillarRecommendationIcon(a.PillarKey),
                    Text = a.Suggestion
                });
            }

            // If everything is fine — positive fallback
            if (!activityRecommendations.Any())
            {
                activityRecommendations.Add(new ActivityRecommendationDto { Icon = "✅", Text = "Keep up the great routine — consistency is the key!" });
                activityRecommendations.Add(new ActivityRecommendationDto { Icon = "🎨", Text = "Try a fun creative project together this weekend" });
                activityRecommendations.Add(new ActivityRecommendationDto { Icon = "📖", Text = "Read together for 15 minutes before bedtime" });
            }

            var recommendations = activityRecommendations.Select(r => r.Text).ToList();

            var cp = childProfile;
            var strengthsWeaknesses = PillarInsightsHelper.BuildStrengthsWeaknesses(cp);

            var latestIllnessLog = await _context.DailyLogEntries
                .Where(l => l.ChildId == heroId && l.WasIllToday)
                .OrderByDescending(l => l.LogDate)
                .FirstOrDefaultAsync();

            var illnessTracking = new IllnessTrackingDto
            {
                PastIllnessSummary = cp.PastIllnessSummary,
                LatestIllnessType = latestIllnessLog?.IllnessType,
                LatestIllnessNote = latestIllnessLog?.IllnessNote,
                LatestIllnessDate = latestIllnessLog?.LogDate,
                AvoidanceTip = !string.IsNullOrWhiteSpace(cp.IllnessAvoidanceTip)
                    ? cp.IllnessAvoidanceTip
                    : latestIllnessLog?.IllnessAvoidanceTip
            };

            // ── Activity hours: always return 7 full days (Mon–Sun), pad missing days with 0 ──
            var today = DateTime.UtcNow.Date;
            // Build a lookup: date → total minutes logged that day
            var logsByDate = logs
                .GroupBy(l => l.LogDate.Date)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.LogDurationInMinutes));

            // Generate exactly 7 days ending today (oldest → today order)
            // FIX: Enumerable.Range(6, 7) produced offsets 6..12, which made
            // AddDays(-(6-offset)) compute *future* dates (today .. today+6)
            // instead of the past 7 days. Use Range(0, 7) so offsets are 0..6,
            // giving dates today-6 .. today (oldest first).
            var activityHours = Enumerable.Range(0, 7)
                .Select(offset => today.AddDays(-(6 - offset)))
                .Select(date =>
                {
                    var mins = logsByDate.TryGetValue(date, out var m) ? m : 0;
                    return new ActivityHourDto
                    {
                        Day = date == today
                            ? "Today"
                            : date == today.AddDays(-1)
                                ? "Yesterday"
                                : date.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture),
                        Hours = Math.Round(mins / 60.0, 1),
                        LogDuration = mins,
                        Date = date,
                        IsToday = date == today
                    };
                })
                .ToList();

            var daysLoggedThisWeek = logsByDate.Count;
            var summaryPeriodLabel = daysLoggedThisWeek <= 1 ? "Today" : "This Week";

            // ── PHASE 12: AI Insights (recommended badge, Leafy recommendation,
            //    personality cluster, anomaly/consistency signal) ──────────────
            // All values are read as-is from AI-written fields; nothing here
            // recomputes a badge pick, a tip selection, a cluster, or a score.
            var latestLog = logs.OrderByDescending(l => l.LogDate).FirstOrDefault();

            RecommendedBadgeDto? recommendedBadgeDto = null;
            if (childProfile.RecommendedBadge != null)
            {
                var alreadyUnlocked = await _context.ChildBadges
                    .AnyAsync(cbg => cbg.ChildId == heroId && cbg.BadgeId == childProfile.RecommendedBadgeId && cbg.IsActive);

                recommendedBadgeDto = new RecommendedBadgeDto
                {
                    BadgeId = childProfile.RecommendedBadge.Id,
                    BadgeName = childProfile.RecommendedBadge.Name,
                    BadgeIcon = childProfile.RecommendedBadge.IconUrl,
                    Description = childProfile.RecommendedBadge.Description,
                    AlreadyUnlocked = alreadyUnlocked
                };
            }

            LeafyRecommendationDto? leafyRecommendationDto = null;
            if (childProfile.RecommendedLeafyTip != null)
            {
                var t = childProfile.RecommendedLeafyTip;
                leafyRecommendationDto = new LeafyRecommendationDto
                {
                    TipId = t.Id,
                    Tip = t.Tip ?? t.Content,
                    Alert = t.Alert,
                    Motivation = t.Motivation,
                    Side = t.Side,
                    Source = "ai"
                };
            }

            PersonalityClusterResultDto? personalityClusterDto = null;
            if (childProfile.LastPersonalityCluster != null)
            {
                var currentSimilarity = await _context.AdminChildrenPersonalityClusters
                    .Where(a => a.ChildId == heroId && a.IsCurrent)
                    .Select(a => (double?)a.SimilarityScore)
                    .FirstOrDefaultAsync() ?? 0;

                personalityClusterDto = new PersonalityClusterResultDto
                {
                    ChildId = heroId,
                    PersonalityClusterId = childProfile.LastPersonalityCluster.Id,
                    ClusterKey = childProfile.LastPersonalityCluster.ClusterKey,
                    ClusterName = childProfile.LastPersonalityCluster.Name,
                    Description = childProfile.LastPersonalityCluster.Description,
                    DominantTraits = childProfile.LastPersonalityCluster.DominantTraits,
                    RecommendedFocus = childProfile.LastPersonalityCluster.RecommendedFocus,
                    SimilarityScore = currentSimilarity,
                    AssignedAt = childProfile.PersonalityClusterAssignedAt ?? DateTime.MinValue
                };
            }

            var aiInsights = new AiInsightsDto
            {
                RecommendedBadge = recommendedBadgeDto,
                LeafyRecommendation = leafyRecommendationDto,
                PersonalityCluster = personalityClusterDto,
                AnomalyDetected = latestLog?.AnomalyFlag ?? false,
                EmotionSafetyAlertFlag = emotionSafetyLog != null,
                EmotionSafetyAlertText = emotionSafetyLog?.Alert,
                ScoreVariance = latestLog?.ScoreVariance,
                SentimentScore = latestLog?.SentimentScore,
                NlpAnomalyScore = latestLog?.NlpAnomalyScore,
                DetectedEmotion = latestLog?.DetectedEmotion
            };

            return Ok(new HeroInsightsDto
            {
                Header = new HeroHeaderDto
                {
                    FullName = childProfile.User?.FullName ?? "Hero Without Name",
                    Age = childProfile.Age,
                    CharacterType = childProfile.CharacterType ?? "Hero",
                    SummaryType = summaryPeriodLabel == "Today" ? "Today's Summary" : "Weekly Summary"
                },
                Categories = new CategoryStats
                {
                    // أداء حقيقي مستقل لكل مجال (0-100) — مش نسبة من المجموع
                    Sports = (int)Math.Round(Math.Clamp(sportsScore, 0, 100)),
                    Study = (int)Math.Round(Math.Clamp(studyScore, 0, 100)),
                    Chores = (int)Math.Round(Math.Clamp(choresScore, 0, 100))
                },
                ActivityHours = activityHours,
                Trends = trends,
                // FIX: cap the total Red Flags shown on the Weekly Summary card.
                // Mood/chores/sleep flags (up to 3) plus pillar flags (up to 3)
                // could previously total 6 — overwhelming for a quick glance.
                // Keep at most 4, most relevant first (order they were added).
                RedFlags = redFlags.Take(4).ToList(),
                Recommendations = recommendations,
                PersonalizedAlerts = personalizedAlerts,
                ActivityRecommendations = activityRecommendations,
                Pillars = new PillarsDto
                {
                    // ── Structured Pillars ──────────────────────────────────
                    Spiritual = Math.Round(cp.ReligiousPct, 1),
                    Social = Math.Round(cp.SocialPct, 1),
                    Academic = Math.Round(cp.AcademicPct, 1),
                    Env = Math.Round(cp.EnvPct, 1),
                    Nutrition = Math.Round(cp.PhysicalPct, 1),
                    Sport = Math.Round(cp.SportPct, 1),
                    Hobbies = Math.Round(cp.CreaPct, 1),
                    Mental = Math.Round(cp.MentalPct, 1),
                    // ── NLP Pillars (AI text analysis) ─────────────────────
                    NlpSpiritual = Math.Round(cp.NlpSpiritualConnection_Pct, 1),
                    NlpSocial = Math.Round(cp.NlpSocialInteraction_Pct, 1),
                    NlpAcademic = Math.Round(cp.NlpAcademicProgress_Pct, 1),
                    NlpEnv = Math.Round(cp.NlpHomeResponsibility_Pct, 1),
                    NlpPhysicalHealth = Math.Round(cp.NlpPhysicalHealth_Pct, 1),
                    NlpPhysicalActivity = Math.Round(cp.NlpPhysicalActivity_Pct, 1),
                    NlpCreativity = Math.Round(cp.NlpCreativityEngagement_Pct, 1),
                    NlpMental = Math.Round(cp.NlpMentalWellbeing_Pct, 1),
                    // ── Final Hybrid Pillars (70% Structured + 30% NLP) ────
                    FinalSpiritual = Math.Round(cp.FinalSpiritualConnection_Pct, 1),
                    FinalSocial = Math.Round(cp.FinalSocialInteraction_Pct, 1),
                    FinalAcademic = Math.Round(cp.FinalAcademicProgress_Pct, 1),
                    FinalEnv = Math.Round(cp.FinalHomeResponsibility_Pct, 1),
                    FinalPhysicalHealth = Math.Round(cp.FinalPhysicalHealth_Pct, 1),
                    FinalPhysicalActivity = Math.Round(cp.FinalPhysicalActivity_Pct, 1),
                    FinalCreativity = Math.Round(cp.FinalCreativityEngagement_Pct, 1),
                    FinalMental = Math.Round(cp.FinalMentalWellbeing_Pct, 1),
                    FinalMeanScore = Math.Round(cp.FinalMeanScore, 1)
                },
                Traits = new TraitsDto
                {
                    Curiosity = Math.Round(cp.CuriosityTrait, 1),
                    Resilience = Math.Round(cp.ResilienceTrait, 1),
                    Kindness = Math.Round(cp.KindnessTrait, 1),
                    Spiritual = Math.Round(cp.SpiritualTrait, 1),
                    Vitality = Math.Round(cp.VitalityTrait, 1)
                },
                HonestPoints = honestDaysCount * 10,
                TrustScore = childProfile.TrustScore,
                DaysLoggedThisWeek = daysLoggedThisWeek,
                SummaryPeriodLabel = summaryPeriodLabel,
                AiInsights = aiInsights,
                StrengthsWeaknesses = strengthsWeaknesses,
                IllnessTracking = illnessTracking
            });
        }
        private static string GetPillarRecommendationIcon(string pillarKey) => pillarKey switch
        {
            "CreativityEngagement_Pct" => "🎨",
            "AcademicProgress_Pct" => "📚",
            "PhysicalHealth_Pct" => "🥗",
            "PhysicalActivity_Pct" => "🏃",
            "SpiritualConnection_Pct" => "🕌",
            "MentalWellbeing_Pct" => "💭",
            "SocialInteraction_Pct" => "🤝",
            "HomeResponsibility_Pct" => "🏠",
            _ => "💡"
        };

        // ─────────────────────────────────────────────────────────────
        // GET /api/Parent/child-last-message/{heroId}
        //
        // AUDIT FIX: ChildMessage table removed — reads MessageToParent
        // from the most recent DailyLogEntry row that actually has one,
        // instead of the old separate messages table.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("child-last-message/{heroId}")]
        public async Task<IActionResult> GetChildLastMessage(int heroId)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int parentId)) return Unauthorized("Invalid token");

            var isHisChild = await _context.ChildProfiles.AnyAsync(cp => cp.UserId == heroId && cp.ParentId == parentId);
            if (!isHisChild) return StatusCode(403);

            var lastMessage = await _context.DailyLogEntries
                .Where(l => l.ChildId == heroId && !string.IsNullOrEmpty(l.MessageToParent))
                .OrderByDescending(l => l.LogDate)
                .Select(l => new { content = l.MessageToParent, sentAt = (DateTime?)l.LogDate })
                .FirstOrDefaultAsync();

            return Ok(lastMessage ?? new { content = "", sentAt = (DateTime?)null });
        }


        [HttpGet("hero-history/{heroId}")]
        public async Task<IActionResult> GetHeroHistory(int heroId)
        {
            var parentId = GetCurrentParentId();
            if (!parentId.HasValue) return Unauthorized();

            var child = await _context.ChildProfiles
                .Include(u => u.User)
                .FirstOrDefaultAsync(c => c.UserId == heroId && c.ParentId == parentId.Value);
            if (child == null) return NotFound("Hero not found");

            var logs = await _context.DailyLogEntries
                .Where(l => l.ChildId == heroId)
                .OrderByDescending(l => l.LogDate)
                .Take(14)
                .ToListAsync();

            // BUG FIX: LogDate is stored in Egypt local time (set via nowEgypt in
            // JournalService / QuickCheckIn). Comparing l.LogDate.Date against
            // DateTime.UtcNow.Date was wrong because UTC can be up to 3 hours
            // BEHIND Egypt, so a log saved on "today" Egyptian time (e.g. 01:00 EGY
            // = 22:00 UTC previous day) produced a negative difference → "-1 Days Ago".
            // Fix: compare against today's date in Egypt time, not UTC.
            TimeZoneInfo _egyptZone;
            try { _egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
            catch { _egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
            var _todayEgypt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _egyptZone).Date;

            var historyData = logs.Select(l =>
            {
                var activities = new List<string>();
                var alarms = new List<string>();

                // AUDIT FIX: this used to be an if(IsJournalSaved)/else split —
                // alarms (low mood, low water, missed chores) and the structured
                // activities list were ONLY built in the else-branch, meaning a
                // normal day with a saved journal entry (the common case) never
                // showed any alarms at all, even when MoodEmoji was 😢 or 😡.
                // BuildActivitiesList/BuildAlarmsList read fields that exist
                // regardless of IsJournalSaved, so there's no reason to gate
                // them on it — build both unconditionally, then layer the
                // journal-only fields (EXP, level-up) on top when present.
                activities.AddRange(BuildActivitiesList(l));
                alarms.AddRange(BuildAlarmsList(l));

                if (l.IsJournalSaved)
                {
                    if (l.ExpAwarded > 0) activities.Add($"⭐ +{l.ExpAwarded} EXP");
                    if (l.TriggeredLevelUp) activities.Add("🎉 Level Up!");
                }

                // AUDIT FIX: the AI team's Emotion Safety Engine output
                // (DailyLogEntry.EmotionSafetyAlertText, written directly via
                // SQL — see DailyLogEntry.cs) was never surfaced in ANY
                // frontend page. This is the actual AI-detected concern signal
                // (distinct from the heuristic mood-emoji check in
                // BuildAlarmsList above) — surface it here so parents can see
                // it in the existing Alarms section instead of it sitting
                // unused in the database.
                if (l.EmotionSafetyAlertFlag && !string.IsNullOrWhiteSpace(l.EmotionSafetyAlertText))
                    alarms.Add($"💛 {l.EmotionSafetyAlertText}");

                if (l.WasIllToday)
                {
                    var illnessLabel = string.IsNullOrWhiteSpace(l.IllnessType) ? "felt unwell" : l.IllnessType;
                    alarms.Add($"🤒 Reported illness: {illnessLabel}");
                }

                return new DayActivityDto
                {
                    Date = l.LogDate.Date.ToString("dd/MM/yyyy"),
                    // BUG FIX: use Egypt-local "today" (_todayEgypt) for label,
                    // because LogDate is stored in Egypt time. UTC comparison caused
                    // entries saved late Egyptian-night to show "-1 Days Ago".
                    Label = l.LogDate.Date == _todayEgypt
                        ? "Today"
                        : l.LogDate.Date == _todayEgypt.AddDays(-1)
                            ? "Yesterday"
                            : $"{(int)(_todayEgypt - l.LogDate.Date).TotalDays} Days Ago",
                    Alarms = alarms,
                    Activities = activities,
                    MessageToParent = l.MessageToParent,
                    WasIllToday = l.WasIllToday,
                    IllnessType = l.IllnessType,
                    IllnessNote = l.IllnessNote,
                    IllnessAvoidanceTip = l.IllnessAvoidanceTip
                };
            }).ToList();

            return Ok(new ChildHistoryDto
            {
                ChildName = child.User?.FullName ?? "",
                CharacterType = child.CharacterType ?? "Explorer",
                History = historyData
            });
        }
        private List<string> BuildAlarmsList(DailyLogEntry log)
        {
            var list = new List<string>();
            if (log.WaterGlasses < 3) list.Add("Low water intake 💧");
            if (log.MoodEmoji == "😢" || log.MoodEmoji == "😡") list.Add("Low mood detected 😟");
            if (!string.IsNullOrEmpty(log.CompletedChores) && log.CompletedChores.Contains("❌")) list.Add("Missed chore 🏠");
            return list;
        }

        private List<string> BuildActivitiesList(DailyLogEntry log)
        {
            var list = new List<string>();
            // and are no longer written by the Phase 7 journal; use CompletedSubjects/PhysicalActivity/SpiritualActivities
            if (!string.IsNullOrEmpty(log.CompletedSubjects)) list.Add($"Studies: {log.CompletedSubjects}");
            if (!string.IsNullOrEmpty(log.PhysicalActivity)) list.Add($"Physical: {log.PhysicalActivity}");
            if (!string.IsNullOrEmpty(log.SpiritualActivities)) list.Add($"Spiritual: {log.SpiritualActivities}");
            return list;
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Parent/hero-management/{heroId}
        // Reads chores / subjects / sports from ChildTask rows
        // (Category = "Chore" | "Study" | "Sport", IsPlanTemplate = true).
        // Previously stored in DailyManagementJson on ChildProfile;
        // migrated to ChildTask so the AI team can read them from the
        // tasks table directly (Phase 13 AI integration requirement).
        // ─────────────────────────────────────────────────────────────
        [HttpGet("hero-management/{heroId}")]
        public async Task<IActionResult> GetHeroManagement(int heroId)
        {
            var parentId = GetCurrentParentId();
            if (!parentId.HasValue) return Unauthorized();

            var childProfile = await _context.ChildProfiles.Include(cp => cp.User)
                .FirstOrDefaultAsync(cp => cp.UserId == heroId && cp.ParentId == parentId.Value);

            if (childProfile == null) return Unauthorized("Not authorized");

            // Read plan-template tasks from ChildTask table
            var planTasks = await _context.ChildTasks
                .Where(t => t.ChildId == heroId
                         && t.ParentId == parentId.Value
                         && (t.Category == "Chore" || t.Category == "Study" || t.Category == "Sport")
                         && t.Description == "__plan_template__")
                .ToListAsync();

            var chores = planTasks.Where(t => t.Category == "Chore").Select(t => t.Title).ToList();
            var subjects = planTasks.Where(t => t.Category == "Study").Select(t => t.Title).ToList();
            var sports = planTasks.Where(t => t.Category == "Sport").Select(t => t.Title).ToList();

            return Ok(new
            {
                profile = new { fullName = childProfile.User?.FullName ?? "Unnamed Hero", characterType = childProfile.CharacterType ?? "Hero", age = childProfile.Age },
                chores = chores.Any() ? chores : new List<string> { "Make the bed", "Tidy up toys" },
                subjects = subjects.Any() ? subjects : new List<string> { "Math", "Reading" },
                sports = sports.Any() ? sports : new List<string> { "Swimming", "Football" }
            });
        }

        // ─────────────────────────────────────────────────────────────
        // PATCH /api/Parent/hero-management/{heroId}
        // Saves chores / subjects / sports as ChildTask rows
        // (Category = "Chore" | "Study" | "Sport", Description = "__plan_template__").
        // Deletes all previous plan-template tasks for this child first
        // (replace-all strategy — matches the old JSON overwrite behavior).
        // The AI team's Anomaly Detection Trust Engine reads these from
        // ChildTasks via GET /api/ai/tasks/{hashedChildId}.
        // ─────────────────────────────────────────────────────────────
        [HttpPatch("hero-management/{heroId}")]
        public async Task<IActionResult> UpdateHeroManagement(int heroId, [FromBody] DailyManagementDto dto)
        {
            var parentId = GetCurrentParentId();
            if (!parentId.HasValue) return Unauthorized();

            var childProfile = await _context.ChildProfiles.Include(cp => cp.User)
                .FirstOrDefaultAsync(cp => cp.UserId == heroId && cp.ParentId == parentId.Value);

            if (childProfile == null) return Unauthorized("Not authorized");

            // Remove all existing plan-template tasks for this child (replace-all strategy)
            var existing = await _context.ChildTasks
                .Where(t => t.ChildId == heroId
                         && t.ParentId == parentId.Value
                         && (t.Category == "Chore" || t.Category == "Study" || t.Category == "Sport")
                         && t.Description == "__plan_template__")
                .ToListAsync();
            _context.ChildTasks.RemoveRange(existing);

            // Insert new plan-template tasks
            var now = DateTime.UtcNow;
            var farDate = new DateTime(2099, 12, 31, 0, 0, 0, DateTimeKind.Utc);

            void AddPlanItems(IEnumerable<string>? items, string category)
            {
                if (items == null) return;
                foreach (var title in items.Where(s => !string.IsNullOrWhiteSpace(s)))
                {
                    _context.ChildTasks.Add(new ChildTask
                    {
                        ChildId = heroId,
                        HashedChildId = _childIdentity.GetSafeChildKey(heroId),
                        ParentId = parentId.Value,
                        Title = title.Trim(),
                        Description = "__plan_template__",
                        Category = category,
                        Points = 0,
                        IsParentAssigned = true,
                        IsCompleted = false,
                        DueDate = farDate,
                        CreatedAt = now
                    });
                }
            }

            AddPlanItems(dto.Chores, "Chore");
            AddPlanItems(dto.Subjects, "Study");
            AddPlanItems(dto.Sports, "Sport");

            await _context.SaveChangesAsync();
            _cache.RemoveByPrefix(CacheKeys.ParentPrefix(parentId.Value));
            _cache.RemoveByPrefix(CacheKeys.ChildPrefix(heroId));

            return Ok(new { message = "Hero plan updated!" });
        }

        [HttpPost("allow-child-reset")]
        public async Task<IActionResult> AllowChildReset([FromBody] ResetRequestDto model)
        {
            var parentIdInt = GetCurrentParentId();
            if (!parentIdInt.HasValue) return Unauthorized("Invalid token");

            var parent = await _userManager.FindByIdAsync(parentIdInt.Value.ToString());
            if (parent == null) return NotFound("Parent not found");

            var isPasswordValid = await _userManager.CheckPasswordAsync(parent, model.ParentPassword);
            if (!isPasswordValid) return BadRequest("Parent password is incorrect");

            var childProfile = await _context.ChildProfiles.FirstOrDefaultAsync(cp => cp.UserId == model.ChildId && cp.ParentId == parentIdInt.Value);
            if (childProfile == null) return NotFound("Hero not found or does not belong to your account.");

            var childUser = await _userManager.FindByIdAsync(model.ChildId.ToString());
            if (childUser != null)
            {
                var existingClaims = await _userManager.GetClaimsAsync(childUser);
                if (!existingClaims.Any(c => c.Type == "PasswordResetRequired"))
                    await _userManager.AddClaimAsync(childUser, new System.Security.Claims.Claim("PasswordResetRequired", "true"));
            }

            childProfile.PasswordResetPending = true;
            // FIX: Use cryptographically secure random (6 digits) instead of
            // System.Random with 4 digits which only had 9000 possible values.
            childProfile.ResetToken = System.Security.Cryptography.RandomNumberGenerator
                .GetInt32(100000, 999999).ToString();
            await _context.SaveChangesAsync();

            return Ok(new { message = "Permission activated successfully", code = childProfile.ResetToken });
        }
        [HttpPost("link-child-by-token")]
        public async Task<IActionResult> LinkChildByToken([FromBody] string token)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int parentId)) return Unauthorized("Invalid token");

            var childProfile = await _context.ChildProfiles.Include(cp => cp.User)
                .FirstOrDefaultAsync(cp => cp.PendingLinkToken == token.ToUpper() && cp.IsStandalone);

            if (childProfile == null) return BadRequest(new { message = "Invalid code or the child is already linked to another account" });

            childProfile.ParentId = parentId;
            childProfile.IsStandalone = false;
            childProfile.PendingLinkToken = null;

            var childUser = childProfile.User;
            childUser.ParentId = parentId;

            await _userManager.UpdateAsync(childUser);
            await _context.SaveChangesAsync();

            _cache.RemoveByPrefix(CacheKeys.ParentPrefix(parentId));

            return Ok(new { message = "Child linked successfully! 🎉" });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // GET /api/Parent/child-weekly-snapshot/{heroId}
        //
        // Returns the full WeeklySnapshotDto for a child the authenticated parent
        // is linked to. Exposes:
        //   • 8 raw weekly Pct averages (Tier 1 → Tier 2 bridge)
        //   • 8 processed pillar scores (engine-weighted)
        //   • Weekly AI metrics
        //   • 5 Core Personality Traits
        //
        // Security: parent must own the child (ParentId == parentId) to read data.
        // ─────────────────────────────────────────────────────────────────────────
        [HttpGet("child-weekly-snapshot/{heroId}")]
        public async Task<IActionResult> GetChildWeeklySnapshot(int heroId)
        {
            var parentId = GetCurrentParentId();
            if (!parentId.HasValue) return Unauthorized("Invalid token");

            // Verify the parent owns this child
            var profile = await _context.ChildProfiles
                .Include(cp => cp.User)
                .FirstOrDefaultAsync(cp => cp.UserId == heroId && cp.ParentId == parentId.Value);

            if (profile == null)
                return NotFound(new { message = "Hero not found or not linked to your account." });

            return Ok(new WeeklySnapshotDto
            {
                ChildId = heroId,
                FullName = profile.User?.FullName ?? "",
                Age = profile.Age,
                Level = profile.Level,

                // LEGACY / FROZEN (Phase 11): no longer updated by any pipeline.
                // Kept for backward-compatible API shape. Consumers should use
                // SpiritualConnection_Pct / SocialInteraction_Pct / etc. below.
#pragma warning disable CS0618
#pragma warning restore CS0618

                // ── Processed 8 Pillar Scores (engine-weighted) ───────────────
                SpiritualConnection_Pct = profile.SpiritualConnection_Pct,
                SocialInteraction_Pct = profile.SocialInteraction_Pct,
                AcademicProgress_Pct = profile.AcademicProgress_Pct,
                HomeResponsibility_Pct = profile.HomeResponsibility_Pct,
                PhysicalHealth_Pct = profile.PhysicalHealth_Pct,
                PhysicalActivity_Pct = profile.PhysicalActivity_Pct,
                CreativityEngagement_Pct = profile.CreativityEngagement_Pct,
                MentalWellbeing_Pct = profile.MentalWellbeing_Pct,

                // ── 5 Core Personality Traits ─────────────────────────────────
                KindnessTrait = profile.KindnessTrait,
                VitalityTrait = profile.VitalityTrait,
                CuriosityTrait = profile.CuriosityTrait,
                ResilienceTrait = profile.ResilienceTrait,
                SpiritualTrait = profile.SpiritualTrait,

                LastTraitSynthesisAt = profile.LastTraitSynthesisAt,
                LastAggregatedAt = profile.LastAggregatedAt,
                AggregationWeekStart = profile.AggregationWeekStart
            });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // GET /api/Parent/children
        // Returns a list of all children linked to the authenticated parent.
        // Used by parent-child-insights and parent-child-history pages to
        // verify heroId belongs to this parent.
        // ─────────────────────────────────────────────────────────────────────────
        [HttpGet("children")]
        public async Task<IActionResult> GetMyChildren()
        {
            var parentId = GetCurrentParentId();
            if (!parentId.HasValue) return Unauthorized();

            var children = await _context.Users
                .Where(u => u.ParentId == parentId.Value && u.UserType == "Child")
                .Include(u => u.ChildProfile)
                .Select(u => new
                {
                    id = u.Id,
                    fullName = u.FullName ?? "Hero",
                    age = u.ChildProfile != null ? u.ChildProfile.Age : 0,
                    level = u.ChildProfile != null ? u.ChildProfile.Level : 1,
                    characterType = u.ChildProfile != null ? u.ChildProfile.CharacterType : "Explorer",
                    trustScore = u.ChildProfile != null ? u.ChildProfile.TrustScore : 100
                })
                .ToListAsync();

            return Ok(children);
        }

    }
}
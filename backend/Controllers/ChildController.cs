using GrawiaaApp.API.Data;
using GrawiaaApp.API.DTOs;
using GrawiaaApp.API.Interfaces;
using GrawiaaApp.API.Models;
using GrawiaaApp.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GrawiaaApp.API.Controllers
{
    [Authorize(Roles = "Child")]
    [Route("api/[controller]")]
    [ApiController]
    public class ChildrenController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICacheService _cache;
        private readonly IGamingService _gamingService;
        private readonly IPillarPipelineService _pillarPipeline;  // PHASE 11: single source of truth
        private readonly ITipGenerationService _tipService;
        private readonly IMotivationGenerationService _motivationService;
        private readonly INewProgressionService _newProgression;   // PHASE 10
        private readonly INewLevelEngineService _newLevelEngine;    // PHASE 10
        private readonly IJournalService _journal;                  // PHASE 10 REFACTOR
        private readonly IChildIdentityService _childIdentity;       // PHASE 10 REFACTOR

        public ChildrenController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ICacheService cache,
            IGamingService gamingService,
            IPillarPipelineService pillarPipeline,    // PHASE 11
            ITipGenerationService tipService,
            IMotivationGenerationService motivationService,
            INewProgressionService newProgression,   // PHASE 10
            INewLevelEngineService newLevelEngine,    // PHASE 10
            IJournalService journal,                  // PHASE 10 REFACTOR
            IChildIdentityService childIdentity)      // PHASE 10 REFACTOR
        {
            _context = context;
            _userManager = userManager;
            _cache = cache;
            _gamingService = gamingService;
            _pillarPipeline = pillarPipeline;     // PHASE 11
            _tipService = tipService;
            _motivationService = motivationService;
            _newProgression = newProgression;     // PHASE 10
            _newLevelEngine = newLevelEngine;      // PHASE 10
            _journal = journal;                    // PHASE 10 REFACTOR
            _childIdentity = childIdentity;        // PHASE 10 REFACTOR
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Children/profile
        // ─────────────────────────────────────────────────────────────
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return Unauthorized("Invalid token claims");

            var result = await _cache.GetOrCreateAsync(CacheKeys.ChildProfile(userId), async () =>
            {
                var childProfile = await _context.ChildProfiles
                    .Include(cp => cp.User)
                    .Include(cp => cp.LastPersonalityCluster)
                    .FirstOrDefaultAsync(cp => cp.UserId == userId);

                if (childProfile == null || childProfile.User == null) return null;

                return (object)new
                {
                    id = userId,
                    fullName = childProfile.User.FullName,
                    username = childProfile.User.UserName,
                    age = childProfile.Age,
                    level = childProfile.Level,
                    currentExp = childProfile.CurrentExp,
                    trustScore = childProfile.TrustScore,
                    trustScoreSetAt = childProfile.TrustScoreSetAt,
                    characterName = childProfile.CharacterName ?? "Hero",
                    characterType = childProfile.CharacterType ?? "Sprout",
                    isStandalone = childProfile.IsStandalone,
                    // ── 8 Pillars ──────────────────────────────────────
                    academicPct = childProfile.AcademicPct,
                    religiousPct = childProfile.ReligiousPct,
                    physicalPct = childProfile.PhysicalPct,
                    sportPct = childProfile.SportPct,
                    socialPct = childProfile.SocialPct,
                    envPct = childProfile.EnvPct,
                    creaPct = childProfile.CreaPct,
                    mentalPct = childProfile.MentalPct,
                    emotionalPct = childProfile.MentalPct,    // legacy alias
                    resiliencePct = childProfile.ResilienceTrait,
                    // ── 8 NLP Pillars (AI-derived from text) ──────────
                    nlpAcademicPct = childProfile.NlpAcademicProgress_Pct,
                    nlpSpiritualPct = childProfile.NlpSpiritualConnection_Pct,
                    nlpPhysicalPct = childProfile.NlpPhysicalHealth_Pct,
                    nlpSportPct = childProfile.NlpPhysicalActivity_Pct,
                    nlpSocialPct = childProfile.NlpSocialInteraction_Pct,
                    nlpEnvPct = childProfile.NlpHomeResponsibility_Pct,
                    nlpCreaPct = childProfile.NlpCreativityEngagement_Pct,
                    nlpMentalPct = childProfile.NlpMentalWellbeing_Pct,
                    // ── 8 Final Hybrid Pillars (70% Structured + 30% NLP) ─
                    finalAcademicPct = childProfile.FinalAcademicProgress_Pct,
                    finalSpiritualPct = childProfile.FinalSpiritualConnection_Pct,
                    finalPhysicalPct = childProfile.FinalPhysicalHealth_Pct,
                    finalSportPct = childProfile.FinalPhysicalActivity_Pct,
                    finalSocialPct = childProfile.FinalSocialInteraction_Pct,
                    finalEnvPct = childProfile.FinalHomeResponsibility_Pct,
                    finalCreaPct = childProfile.FinalCreativityEngagement_Pct,
                    finalMentalPct = childProfile.FinalMentalWellbeing_Pct,
                    finalMeanScore = childProfile.FinalMeanScore,
                    meanScore = childProfile.MeanScore,
                    // ── 5 Traits ───────────────────────────────────────
                    kindnessTrait = childProfile.KindnessTrait,
                    vitalityTrait = childProfile.VitalityTrait,
                    curiosityTrait = childProfile.CuriosityTrait,
                    resilienceTrait = childProfile.ResilienceTrait,
                    spiritualTrait = childProfile.SpiritualTrait,
                    lastTraitSynthesis = childProfile.LastTraitSynthesisAt,
                    // ── PHASE 12: AI recommendation pointers ────────────
                    recommendedBadgeId = childProfile.RecommendedBadgeId,
                    recommendedLeafyTipId = childProfile.RecommendedLeafyTipId,
                    personalityClusterName = childProfile.LastPersonalityCluster?.Name,
                    personalityClusterAssignedAt = childProfile.PersonalityClusterAssignedAt,
                    // ── PHASE 13: Full personality cluster detail ───────
                    personalityClusterId = childProfile.LastPersonalityClusterId,
                    personalityClusterDescription = childProfile.LastPersonalityCluster?.Description,
                    personalityClusterDominantTraits = childProfile.LastPersonalityCluster?.DominantTraits,
                    personalityClusterRecommendedFocus = childProfile.LastPersonalityCluster?.RecommendedFocus,
                    // ── PHASE 13: Leafy RAG free-text outputs ───────────
                    leafyTipText = childProfile.LeafyTipText,
                    leafyMessageText = childProfile.LeafyMessageText,
                    leafyAlertText = childProfile.LeafyAlertText,
                    leafyTextSetAt = childProfile.LeafyTextSetAt,
                    pastIllnessSummary = childProfile.PastIllnessSummary,
                    illnessAvoidanceTip = childProfile.IllnessAvoidanceTip
                };
            }, TimeSpan.FromMinutes(3));

            if (result == null) return NotFound("Hero not found");
            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────
        // PATCH /api/Children/profile
        // ─────────────────────────────────────────────────────────────
        [HttpPatch("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateChildProfileDto dto)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId)) return Unauthorized("Invalid token");

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return NotFound("User not found");

            var childProfile = await _context.ChildProfiles.FirstOrDefaultAsync(cp => cp.UserId == userId);
            if (childProfile == null) return NotFound("Child profile not found");

            if (!string.IsNullOrWhiteSpace(dto.FullName))
                user.FullName = dto.FullName;

            if (dto.Age.HasValue && dto.Age >= 4 && dto.Age <= 18)
                childProfile.Age = dto.Age.Value;

            if (!string.IsNullOrWhiteSpace(dto.CharacterName))
                childProfile.CharacterName = dto.CharacterName;

            // FIX: Persist the selected avatar emoji to CharacterType.
            // CharacterType is the emoji chosen from the avatar grid (e.g. "🦁", "🦋", …).
            // This field exists in the DB (ChildProfiles.CharacterType) and was already
            // saved on sign-up — we just never updated it on edit. Now we do.
            if (!string.IsNullOrWhiteSpace(dto.CharacterType))
                childProfile.CharacterType = dto.CharacterType;

            await _userManager.UpdateAsync(user);
            await _context.SaveChangesAsync();

            _cache.RemoveByPrefix(CacheKeys.ChildPrefix(userId));

            return Ok(new
            {
                message = "Profile updated!",
                characterType = childProfile.CharacterType,
                characterName = childProfile.CharacterName,
                fullName = user.FullName,
                age = childProfile.Age
            });
        }

        // ─────────────────────────────────────────────────────────────
        // PATCH /api/Children/quick-check-in
        // Saves the daily log then triggers GamingService to update
        // TrustScore and fire badge/notification observers.
        // ─────────────────────────────────────────────────────────────
        [HttpPatch("quick-check-in")]
        public async Task<IActionResult> QuickCheckIn([FromBody] AutoSaveLogDto dto)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId)) return Unauthorized("Invalid token");

            // AUDIT FIX: was DateTime.UtcNow.Date. Every other "today" row
            // lookup for DailyLogEntry (GetOrCreateTodayEntryAsync,
            // SendMessageToParent, SubmitAnswer) uses Egypt-local calendar
            // day. Using UTC here meant QuickCheckIn could create/match a
            // DIFFERENT row near midnight Egypt time (UTC+2/+3) than the
            // row those other endpoints would use for "today" — silently
            // splitting GeneralNote/FeelingNote/MessageToParent across two
            // rows for what the child experiences as a single day.
            TimeZoneInfo egyptZone;
            try { egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
            catch { egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
            var nowEgypt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptZone);
            var today = nowEgypt.Date;

            var log = await _context.DailyLogEntries
                .FirstOrDefaultAsync(l => l.ChildId == userId && l.LogDate.Date == today);

            if (log == null)
            {
                log = new DailyLogEntry
                {
                    ChildId = userId,
                    HashedChildId = _childIdentity.GetSafeChildKey(userId),
                    LogDate = nowEgypt
                };
                _context.DailyLogEntries.Add(log);
            }
            else if (string.IsNullOrEmpty(log.HashedChildId))
            {
                log.HashedChildId = _childIdentity.GetSafeChildKey(userId);
            }

            // FIX: Write to the MODERN DailyLogEntry columns (read by PillarCalculationEngine,
            // history.html, and the weekly aggregation). Legacy raw fields removed in Phase 14.
            // SocialInteraction) are backward-compat only and no longer consumed by any engine.
            log.SpiritualActivities = string.Join(", ", dto.SpiritualActivities ?? new List<string>());
            log.SocialTimeWith = dto.SocialTimeWith;
            log.CompletedSubjects = string.Join(", ", dto.CompletedSubjects ?? new List<string>());
            log.HelpedFamily = dto.HelpedFamily;
            log.CompletedChores = dto.HelpedFamily ? "✅ help family" : "";
            log.NutritionItems = string.Join(", ", dto.NutritionItems ?? new List<string>());
            log.WaterGlasses = dto.WaterGlasses;
            log.PhysicalActivity = dto.PhysicalActivity;
            log.FunActivities = string.Join(", ", dto.FunActivities ?? new List<string>());
            log.MoodLevel = dto.MoodLevel;
            log.MoodEmoji = dto.MoodLevel switch
            {
                "great" => "😊",
                "good" => "🙂",
                "okay" => "😐",
                "bad" => "🙁",
                "terrible" => "😢",
                _ => dto.MoodLevel   // caller may pass an emoji directly
            };

            // ── PHASE 11: Single Pillars/Traits Pipeline ──────────────────────
            // Replaces the old ad-hoc additive pillar increments
            // (profile.AcademicPct += ..., etc.), ComputeTraits, and the
            // 90-day FromWeeklyEntries "normalize" block — all of which
            // competed with SaveJournal's own write path.
            //
            // A quick check-in IS today's journal entry (the child may not
            // submit the full /journal form separately), so it now marks
            // IsJournalSaved = true and runs through the exact same
            // pipeline as SaveJournal: Step 1 (daily pillars for this
            // entry) → Step 2 (30-day profile aggregate) → Step 3 (traits).
            log.IsJournalSaved = true;
            _pillarPipeline.ApplyDailyPillars(log);
            await _context.SaveChangesAsync(); // flush DailyXxx_Pct + IsJournalSaved to DB
            await _pillarPipeline.RecalculateProfileAndTraitsAsync(userId);
            await _context.SaveChangesAsync(); // flush updated ChildProfile pillars + traits

            // FIX: Trigger GamingService to update TrustScore and fire
            // badge/notification observers after each daily check-in.
            // Award +2 points for a completed check-in (capped at 100).
            await _gamingService.UpdateScore(userId, 2);

            // BUG FIX (Phase 11): QuickCheckIn now triggers NewProgressionService
            // so that challenge progress, badge unlocks, and level-up are evaluated
            // even when the child uses quick check-in instead of the full /journal form.
            // Without this, a child who only used quick check-in would never progress.
            await _newProgression.RunAsync(userId);

            _cache.RemoveByPrefix(CacheKeys.ChildPrefix(userId));

            return Ok(new { message = "Daily check-in saved! 🌟" });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Children/daily-question
        //
        // AUDIT FIX (AI direct-DB-read requirement): this "question" is
        // the fixed prompt "What do you want to tell Leafy about today?"
        // — the AI team's GeneralNote spec input. The answer used to be
        // stored in a separate DailyQuestionAnswer table (now removed);
        // it's now read/written directly on DailyLogEntry.GeneralNote
        // for today's Egypt-local calendar day, alongside FeelingNote
        // and MessageToParent on the same row.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("daily-question")]
        public async Task<IActionResult> GetDailyQuestion()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
                return Unauthorized("Invalid token claims");

            var questions = new[]
            {
                "What Do You Want Tell Leafy about Today? 😊",

            };

            TimeZoneInfo egyptZone;
            try { egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
            catch { egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
            var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptZone).Date;

            var questionIndex = today.DayOfYear % questions.Length;
            // FIX P1: questionId = index ثابت لليوم حتى يستطيع Frontend تتبع الإجابة
            var questionId = questionIndex + 1;

            // Already answered today if today's DailyLogEntry row exists
            // and already has a non-empty GeneralNote.
            var alreadyAnswered = await _context.DailyLogEntries
                .AnyAsync(l => l.ChildId == userId
                            && l.LogDate.Date == today
                            && !string.IsNullOrEmpty(l.GeneralNote));

            return Ok(new
            {
                questionId,
                questionText = questions[questionIndex],
                alreadyAnswered
            });
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/Children/submit-question-answer
        //
        // AUDIT FIX: write-through to DailyLogEntry.GeneralNote for
        // today's Egypt-local row (same pattern as send-message-to-
        // parent) instead of the removed DailyQuestionAnswer table, so
        // the AI team's direct-DB-read jobs find the value on the same
        // DailyLogEntry row they already read FeelingNote/MessageToParent
        // from.
        // ─────────────────────────────────────────────────────────────
        [HttpPost("submit-question-answer")]
        public async Task<IActionResult> SubmitAnswer([FromBody] DailyQuestionAnswerDto dto)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId)) return Unauthorized("Invalid token");

            TimeZoneInfo egyptZone;
            try { egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
            catch { egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
            var nowEgypt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptZone);
            var today = nowEgypt.Date;

            // Same-day row lookup regardless of IsJournalSaved — never
            // create a duplicate row for the same calendar day.
            var todayLog = await _context.DailyLogEntries
                .FirstOrDefaultAsync(l => l.ChildId == userId && l.LogDate.Date == today);

            if (todayLog == null)
            {
                todayLog = new DailyLogEntry
                {
                    ChildId = userId,
                    HashedChildId = _childIdentity.GetSafeChildKey(userId),
                    LogDate = nowEgypt
                    // IsJournalSaved defaults to false — answering this
                    // question alone never counts as "journal saved".
                };
                _context.DailyLogEntries.Add(todayLog);
            }
            else if (string.IsNullOrEmpty(todayLog.HashedChildId))
            {
                todayLog.HashedChildId = _childIdentity.GetSafeChildKey(userId);
            }

            todayLog.GeneralNote = dto.Answer;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Answer saved!" });
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/Children/leafy-chat
        // Presentation: "Write to Leafy" — multi-turn chat stored on
        // DailyLogEntry.GeneralNote (AI reads same column).
        // ─────────────────────────────────────────────────────────────
        [HttpPost("leafy-chat")]
        public async Task<IActionResult> SendLeafyChat([FromBody] LeafyChatDto dto)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
                return Unauthorized("Invalid token");
            if (string.IsNullOrWhiteSpace(dto.Message))
                return BadRequest(new { message = "Message is required." });

            TimeZoneInfo egyptZone;
            try { egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
            catch { egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
            var nowEgypt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptZone);
            var today = nowEgypt.Date;

            var todayLog = await _context.DailyLogEntries
                .FirstOrDefaultAsync(l => l.ChildId == userId && l.LogDate.Date == today);

            if (todayLog == null)
            {
                todayLog = new DailyLogEntry
                {
                    ChildId = userId,
                    HashedChildId = _childIdentity.GetSafeChildKey(userId),
                    LogDate = nowEgypt
                };
                _context.DailyLogEntries.Add(todayLog);
            }
            else if (string.IsNullOrEmpty(todayLog.HashedChildId))
            {
                todayLog.HashedChildId = _childIdentity.GetSafeChildKey(userId);
            }

            var line = dto.Message.Trim();
            todayLog.GeneralNote = string.IsNullOrWhiteSpace(todayLog.GeneralNote)
                ? line
                : $"{todayLog.GeneralNote}\n{line}";

            await _context.SaveChangesAsync();
            return Ok(new { message = "Leafy received your message! 🌱", chatText = todayLog.GeneralNote });
        }

        [HttpGet("leafy-chat")]
        public async Task<IActionResult> GetLeafyChat()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
                return Unauthorized("Invalid token");

            TimeZoneInfo egyptZone;
            try { egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
            catch { egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
            var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptZone).Date;

            var text = await _context.DailyLogEntries
                .Where(l => l.ChildId == userId && l.LogDate.Date == today)
                .OrderByDescending(l => l.LogDate)
                .Select(l => l.GeneralNote)
                .FirstOrDefaultAsync();

            var messages = string.IsNullOrWhiteSpace(text)
                ? Array.Empty<string>()
                : text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return Ok(new { messages, fullText = text ?? "" });
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/Children/send-message-to-parent
        //
        // AUDIT FIX (AI direct-DB-read requirement): the AI team's NLP
        // jobs (Honesty Validator, Trait Extractor, Emotion Safety)
        // read DailyLogEntry.MessageToParent by querying the database
        // directly — not through any backend API. The old ChildMessage
        // table (and the parent-dashboard chat logic that read from it)
        // has been removed entirely; the message now lives ONLY on
        // today's DailyLogEntry.MessageToParent row (Egypt-local
        // calendar day, same "today" used everywhere else in this
        // codebase — see JournalService / GetTodayLog). If no row
        // exists yet for today (child hasn't opened the journal today),
        // a new one is created with IsJournalSaved = false — same
        // default the model already uses for an unsaved entry — so
        // this can never mark a day as "journaled" on its own, and
        // existing IsJournalSaved-gated logic (EXP, progression,
        // anomaly detection, AI orchestration) is completely unaffected.
        // ─────────────────────────────────────────────────────────────
        [HttpPost("send-message-to-parent")]
        public async Task<IActionResult> SendMessageToParent([FromBody] ChildMessageDto dto)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int childId)) return Unauthorized("Invalid token");

            var childProfile = await _context.ChildProfiles
                .FirstOrDefaultAsync(cp => cp.UserId == childId);

            if (childProfile?.ParentId == null) return BadRequest("You need to link to a parent first");

            // ── Write-through to DailyLogEntry.MessageToParent ──────────
            TimeZoneInfo egyptZone;
            try { egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
            catch { egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
            var nowEgypt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptZone);
            var today = nowEgypt.Date;

            // Deliberately NOT filtering by IsJournalSaved here — we want
            // whatever row already represents "today" for this child,
            // saved or not, so we never create a duplicate row for the
            // same calendar day.
            var todayLog = await _context.DailyLogEntries
                .FirstOrDefaultAsync(l => l.ChildId == childId && l.LogDate.Date == today);

            if (todayLog == null)
            {
                todayLog = new DailyLogEntry
                {
                    ChildId = childId,
                    HashedChildId = _childIdentity.GetSafeChildKey(childId),
                    LogDate = nowEgypt
                    // IsJournalSaved defaults to false — this message
                    // alone never counts as "the journal was saved".
                };
                _context.DailyLogEntries.Add(todayLog);
            }
            else if (string.IsNullOrEmpty(todayLog.HashedChildId))
            {
                todayLog.HashedChildId = _childIdentity.GetSafeChildKey(childId);
            }

            // FIX: Append with timestamp prefix instead of overwriting —
            // لو الطفل بعت أكتر من رسالة في نفس اليوم، كل رسالة تتضاف
            // على نفس الصف (مفيش صف جديد) بـ timestamp واضح عشان الـ AI
            // يقدر يميّز الرسائل لما يقرأ من الداتابيز مباشرة.
            // Format: "[MSG 1 | HH:mm] نص\n[MSG 2 | HH:mm] نص"
            TimeZoneInfo egyptZoneMtp;
            try { egyptZoneMtp = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
            catch { egyptZoneMtp = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
            var msgTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptZoneMtp).ToString("HH:mm");

            if (string.IsNullOrWhiteSpace(todayLog.MessageToParent))
            {
                // أول رسالة النهاردة
                todayLog.MessageToParent = $"[MSG 1 | {msgTime}] {dto.Content}";
            }
            else
            {
                // رسالة إضافية — احسب الرقم التسلسلي من عدد الرسائل الموجودة
                var existingMsgCount = todayLog.MessageToParent
                    .Split("[MSG ", StringSplitOptions.RemoveEmptyEntries)
                    .Length;
                todayLog.MessageToParent += $"\n[MSG {existingMsgCount + 1} | {msgTime}] {dto.Content}";
            }

            await _context.SaveChangesAsync();

            _cache.RemoveByPrefix(CacheKeys.ParentPrefix(childProfile.ParentId.Value));

            return Ok(new { message = "Message sent!" });
        }


        // ─────────────────────────────────────────────────────────────
        // GET /api/Children/leafy-tip  (legacy — random selection)
        // ─────────────────────────────────────────────────────────────
        [HttpGet("leafy-tip")]
        public async Task<IActionResult> GetLeafyTip()
        {
            var tips = await _context.LeafyTips.ToListAsync();
            if (tips.Any())
            {
                var random = tips[new Random().Next(tips.Count)];
                return Ok(new { tip = random.Content, side = random.Side, type = random.Type, characterName = "Leafy" });
            }
            return Ok(new { tip = "Drink more water! 💧", characterName = "Leafy" });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Children/smart-tip
        // Phase 4 — Intelligent tip based on LOWEST pillar score.
        // Selects the best matching LeafyTip from the database.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("smart-tip")]
        public async Task<IActionResult> GetSmartTip()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return Unauthorized("Invalid token claims");

            var result = await _tipService.GenerateTipAsync(userId);
            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Children/motivation
        // Phase 4 — Positive reinforcement based on strongest pillar
        //            or highest improvement from previous week.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("motivation")]
        public async Task<IActionResult> GetMotivation()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return Unauthorized("Invalid token claims");

            var result = await _motivationService.GenerateMotivationAsync(userId);
            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Children/leafy-recommendation
        // PHASE 12 — AI Team's daily Leafy Recommendation output.
        // Priority order:
        //   1. Free-text RAG output (LeafyTipText/LeafyMessageText/
        //      LeafyAlertText) — the AI's directly-composed text, when
        //      present, takes priority since it's the most current/
        //      specific output the AI wrote for THIS child today.
        //   2. ChildProfile.RecommendedLeafyTipId resolved into the
        //      LeafyTip catalog row (AI selected an existing tip instead
        //      of composing free text).
        //   3. Backend-computed smart-tip / legacy random tip fallback —
        //      ONLY when the AI hasn't delivered anything yet. This never
        //      recomputes the AI's choice, it just keeps serving the
        //      pre-existing backend fallback chain for the gap before the
        //      AI's first run.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("leafy-recommendation")]
        public async Task<IActionResult> GetLeafyRecommendation()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int childId))
                return Unauthorized("Invalid token");

            var child = await _context.ChildProfiles
                .Include(c => c.RecommendedLeafyTip)
                .FirstOrDefaultAsync(c => c.UserId == childId);

            bool hasFreeText = child != null && (
                !string.IsNullOrEmpty(child.LeafyTipText) ||
                !string.IsNullOrEmpty(child.LeafyMessageText) ||
                !string.IsNullOrEmpty(child.LeafyAlertText));

            if (hasFreeText)
            {
                return Ok(new LeafyRecommendationDto
                {
                    TipId = child!.RecommendedLeafyTipId,
                    Tip = child.LeafyTipText,
                    Alert = child.LeafyAlertText,
                    Motivation = child.LeafyMessageText,
                    Side = child.RecommendedLeafyTip?.Side,
                    Source = "ai"
                });
            }

            if (child?.RecommendedLeafyTip != null)
            {
                var t = child.RecommendedLeafyTip;
                return Ok(new LeafyRecommendationDto
                {
                    TipId = t.Id,
                    Tip = t.Tip ?? t.Content,
                    Alert = t.Alert,
                    Motivation = t.Motivation,
                    Side = t.Side,
                    Source = "ai"
                });
            }

            // AI hasn't recommended a tip yet — fall back to the existing
            // backend-computed smart tip (same logic GET /smart-tip uses).
            var smart = await _tipService.GenerateTipAsync(childId);
            if (smart != null)
            {
                return Ok(new LeafyRecommendationDto
                {
                    TipId = null,
                    Tip = smart.Tip,
                    Side = smart.Side,
                    Source = "fallback"
                });
            }

            return Ok(new LeafyRecommendationDto { Tip = "Drink more water! 💧", Source = "fallback" });
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/Children/generate-link-token
        // ─────────────────────────────────────────────────────────────
        [HttpPost("generate-link-token")]
        public async Task<IActionResult> GenerateLinkToken()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId)) return Unauthorized("Invalid token");

            var childProfile = await _context.ChildProfiles.FirstOrDefaultAsync(c => c.UserId == userId);
            if (childProfile == null) return NotFound("Child profile not found");

            childProfile.PendingLinkToken = Guid.NewGuid().ToString("N")[..6].ToUpper();
            await _context.SaveChangesAsync();

            return Ok(new { message = "Link token generated!", linkToken = childProfile.PendingLinkToken });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Children/my-badges
        // ─────────────────────────────────────────────────────────────
        [HttpGet("my-badges")]
        public async Task<IActionResult> GetMyBadges()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId)) return Unauthorized("Invalid token");
            var badges = await _context.ChildBadges
                .Where(cb => cb.ChildId == userId && cb.IsActive)
                .Include(cb => cb.Badge)
                .Select(cb => new { cb.Badge.Name, cb.Badge.Description, cb.Badge.IconUrl, cb.UnlockedAt })
                .ToListAsync();
            return Ok(badges);
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Children/all-badges
        // PHASE 10: Returns EVERY badge in the system. Badges the child has
        // already unlocked are flagged IsUnlocked=true with UnlockedAt set.
        // Badges that are still locked include the live ChildChallengeProgress
        // (current value vs. target) for each of their BadgeChallenges, so the
        // frontend can render locked badges as grey orbs with progress bars
        // ("badge challenge tracking").
        // ─────────────────────────────────────────────────────────────
        [HttpGet("all-badges")]
        public async Task<IActionResult> GetAllBadgesWithChallenges()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
                return Unauthorized("Invalid token");

            var allBadges = await _context.Badges
                .Include(b => b.Challenges.OrderBy(c => c.SortOrder))
                .ToListAsync();

            var unlockedMap = await _context.ChildBadges
                .Where(cb => cb.ChildId == userId && cb.IsActive)
                .ToDictionaryAsync(cb => cb.BadgeId, cb => cb.UnlockedAt);

            var progressMap = await _context.ChildChallengeProgresses
                .Where(cp => cp.ChildId == userId)
                .ToDictionaryAsync(cp => cp.ChallengeId);

            var result = allBadges.Select(b =>
            {
                bool isUnlocked = unlockedMap.ContainsKey(b.Id);

                var dto = new BadgeWithChallengesDto
                {
                    Id = b.Id,
                    Name = b.Name,
                    Description = b.Description,
                    IconUrl = b.IconUrl,
                    IsUnlocked = isUnlocked,
                    UnlockedAt = isUnlocked ? unlockedMap[b.Id] : null,
                    // AI team spec pillar pct columns — read directly from Badge table
                    AcadPct = b.AcadPct,
                    SocPct = b.SocPct,
                    MentPct = b.MentPct,
                    PhysPct = b.PhysPct,
                    SpirPct = b.SpirPct,
                    EnvPct = b.EnvPct,
                    CreaPct = b.CreaPct,
                    ResPct = b.ResPct
                };

                if (!isUnlocked)
                {
                    dto.Challenges = b.Challenges
                        .OrderBy(c => c.SortOrder)
                        .Select(c =>
                        {
                            progressMap.TryGetValue(c.Id, out var prog);
                            return new BadgeChallengeProgressDto
                            {
                                ChallengeId = c.Id,
                                Emoji = c.Emoji,
                                Description = c.Description,
                                MetricType = c.MetricType,
                                MetricSubType = c.MetricSubType,
                                CurrentValue = prog?.CurrentValue ?? 0,
                                TargetValue = c.TargetValue,
                                IsCompleted = prog?.IsCompleted ?? false
                            };
                        })
                        .ToList();
                }

                return dto;
            }).ToList();

            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────
        // REMOVED: GET /api/Children/pillar-badges
        // Reason: endpoint queried for MetricType == "pillar_pct" challenges,
        // which do not exist in the seeded data (AI team CSV uses journal_text
        // challenges only — no Bronze/Silver/Gold pillar tier system). The
        // endpoint always returned an empty pillars array, making the 8-pillar
        // grid permanently broken. Frontend now uses /Children/all-badges for
        // all badge display. See ChildController audit notes, Phase 14.


        // ─────────────────────────────────────────────────────────────
        // GET /api/Children/stats
        // FIX: streak calculation uses a single bulk query instead of
        // N+1 per-day queries to avoid performance degradation.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
                return Unauthorized("Invalid token");

            // FIX: run queries sequentially — EF Core DbContext is not thread-safe
            // and Task.WhenAll on the same instance causes InvalidOperationException.
            int totalLogs = await _context.DailyLogEntries
                                  .Where(l => l.ChildId == userId)
                                  .CountAsync();

            int completedTasks = await _context.ChildTasks
                                  .Where(t => t.ChildId == userId && t.IsCompleted)
                                  .CountAsync();

            int totalBadges = await _context.ChildBadges
                                  .Where(cb => cb.ChildId == userId && cb.IsActive)
                                  .CountAsync();

            var profile = await _context.ChildProfiles
                                  .FirstOrDefaultAsync(cp => cp.UserId == userId);

            // fetch all log dates in one query, then compute streak in-memory
            var today = DateTime.UtcNow.Date;
            var logDateSet = (await _context.DailyLogEntries
                                  .Where(l => l.ChildId == userId && l.LogDate.Date <= today)
                                  .Select(l => l.LogDate.Date)
                                  .Distinct()
                                  .ToListAsync()).ToHashSet();

            // Compute streak in-memory (no extra DB calls)
            int streak = 0;
            var checkDate = today;
            while (logDateSet.Contains(checkDate))
            {
                streak++;
                checkDate = checkDate.AddDays(-1);
                if (streak >= 365) break;
            }

            double progressPct = 0;
            if (profile != null)
            {
                // FIX: Use the canonical 8 pillar columns from ChildProfile.
                //
                // Previous bugs in this calculation:
                //   1. EmotionalPct and MentalPct are the same field (EmotionalPct is a
                //      [NotMapped] alias of MentalWellbeing_Pct). Including both was
                //      double-counting MentalWellbeing and inflating the average.
                //   2. ResiliencePct is the standalone column for the Resilience Trait
                //      score (0–100, kept in sync with ResilienceTrait by
                //      PillarPipelineService.RecalculateTraits, R7 invariant) — it
                //      is NOT one of the 8 pillar columns and must not be averaged
                //      with them.
                //   3. SportPct (PhysicalActivity_Pct) and EnvPct (HomeResponsibility_Pct)
                //      were completely missing, meaning the 8-pillar average was computed
                //      over only 5 values and divided by 7, producing a systematically
                //      low result.
                //
                // Fix: average all 8 canonical pillar columns divided by 8.
                progressPct = Math.Round(
                    (profile.SpiritualConnection_Pct   // ReligiousPct alias
                   + profile.SocialInteraction_Pct     // SocialPct alias
                   + profile.AcademicProgress_Pct      // AcademicPct alias
                   + profile.HomeResponsibility_Pct    // EnvPct alias
                   + profile.PhysicalHealth_Pct        // PhysicalPct alias
                   + profile.PhysicalActivity_Pct      // SportPct alias
                   + profile.CreativityEngagement_Pct  // CreaPct alias
                   + profile.MentalWellbeing_Pct)      // MentalPct / EmotionalPct alias
                    / 8.0,
                    1);
            }

            return Ok(new
            {
                totalLogs,
                completedTasks,
                totalBadges,
                currentStreak = streak,
                progressPct,
                dimensions = profile == null ? null : new
                {
                    // FIX: Return all 8 canonical pillar values under clear names.
                    // legacy keys (academic, emotional, resilience…) are kept for
                    // backward-compat with heroprof.html radar chart.
                    spiritual = profile.SpiritualConnection_Pct,
                    academic = profile.AcademicProgress_Pct,
                    social = profile.SocialInteraction_Pct,
                    chores = profile.HomeResponsibility_Pct,
                    physical = profile.PhysicalHealth_Pct,
                    sport = profile.PhysicalActivity_Pct,
                    hobbies = profile.CreativityEngagement_Pct,
                    mental = profile.MentalWellbeing_Pct,
                    // legacy aliases expected by heroprof.html computeArchetype()
                    religious = profile.SpiritualConnection_Pct,
                    emotional = profile.MentalWellbeing_Pct,
                    resilience = profile.ResilienceTrait   // trait score, not a pillar
                }
            });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Children/daily-logs
        // ─────────────────────────────────────────────────────────────
        [HttpGet("daily-logs")]
        public async Task<IActionResult> GetDailyLogs()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
                return Unauthorized("Invalid token");

            var logs = await _cache.GetOrCreateAsync(
                CacheKeys.ChildLogs(userId),
                async () => (object)await _context.DailyLogEntries
                    .Where(l => l.ChildId == userId)
                    .OrderByDescending(l => l.LogDate)
                    .Take(90)
                    .Select(l => new
                    {
                        l.Id,
                        logDate = l.LogDate.ToString("yyyy-MM-dd"),
                        l.MoodEmoji,
                        l.SocialInteraction,
                        l.SleepHours,
                        l.IsFinalized,
                        l.IsHonest,
                        sentimentScore = l.SentimentScore.HasValue
                                           ? Math.Round(l.SentimentScore.Value, 2)
                                           : (double?)null
                    })
                    .ToListAsync(),
                TimeSpan.FromMinutes(3)
            );

            return Ok(logs);
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Children/journal-history
        //
        // AUDIT FIX: reads GeneralNote directly from DailyLogEntries
        // now that the separate DailyQuestionAnswer table has been
        // removed. Only rows with a non-empty GeneralNote are returned,
        // matching the previous "only answered days" behaviour.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("journal-history")]
        public async Task<IActionResult> GetJournalHistory()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
                return Unauthorized("Invalid token");

            var history = await _context.DailyLogEntries
                .Where(l => l.ChildId == userId && !string.IsNullOrEmpty(l.GeneralNote))
                .OrderByDescending(l => l.LogDate)
                .Take(60)
                .Select(l => new
                {
                    l.Id,
                    dateAnswered = l.LogDate.ToString("yyyy-MM-dd"),
                    Answer = l.GeneralNote
                })
                .ToListAsync();

            return Ok(history);
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Children/my-management
        // Returns the parent-configured daily plan (chores / subjects / sports)
        // for this child. Previously read from DailyManagementJson on ChildProfile;
        // now reads from ChildTask rows with Description == "__plan_template__"
        // so the AI team's Anomaly Detection Trust Engine sees the same data
        // from GET /api/ai/tasks/{hashedChildId} (Phase 13 AI integration).
        // ─────────────────────────────────────────────────────────────
        [HttpGet("my-management")]
        public async Task<IActionResult> GetMyManagement()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int childId))
                return Unauthorized("Invalid token");

            // Read plan-template tasks from ChildTask table
            var planTasks = await _context.ChildTasks
                .Where(t => t.ChildId == childId
                         && (t.Category == "Chore" || t.Category == "Study" || t.Category == "Sport")
                         && t.Description == "__plan_template__")
                .ToListAsync();

            var chores = planTasks.Where(t => t.Category == "Chore").Select(t => t.Title).ToList();
            var subjects = planTasks.Where(t => t.Category == "Study").Select(t => t.Title).ToList();
            var sports = planTasks.Where(t => t.Category == "Sport").Select(t => t.Title).ToList();

            bool hasParentPlan = chores.Any() || subjects.Any() || sports.Any();

            return Ok(new { chores, subjects, sports, hasParentPlan });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Children/suggested-badges
        // Returns active AI-suggested badges for the child.
        // Marks unseen suggestions as shown.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("suggested-badges")]
        public async Task<IActionResult> GetSuggestedBadges()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int childId))
                return Unauthorized("Invalid token");

            var now = DateTime.UtcNow;

            // Fetch active, non-dismissed, non-expired suggestions
            var suggestions = await _context.AiBadgeSuggestions
                .Where(s => s.ChildId == childId
                         && !s.IsDismissed
                         && s.ExpiresAt > now)
                .OrderByDescending(s => s.Urgency == "high" ? 2 :
                                        s.Urgency == "medium" ? 1 : 0)
                .ThenBy(s => s.ExpiresAt)
                .ToListAsync();

            // Mark unseen suggestions as shown
            var unseen = suggestions.Where(s => !s.IsShown).ToList();
            if (unseen.Any())
            {
                unseen.ForEach(s => s.IsShown = true);
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                count = suggestions.Count,
                suggestions = suggestions.Select(s => new
                {
                    s.Id,
                    s.Pillar,
                    s.BadgeName,
                    s.BadgeIcon,
                    s.Message,
                    s.Urgency,
                    s.CurrentPct,
                    s.TargetPct,
                    gapPct = Math.Max(0, s.TargetPct - s.CurrentPct),
                    progressToNext = s.TargetPct > 0
                        ? Math.Min(100, (int)Math.Round(s.CurrentPct / s.TargetPct * 100))
                        : 0,
                    isNew = !s.IsShown,
                    expiresAt = s.ExpiresAt,
                })
            });
        }

        // ─────────────────────────────────────────────────────────────
        // POST /api/Children/suggested-badges/{id}/dismiss
        // Child dismisses a suggestion so it won't show again
        // ─────────────────────────────────────────────────────────────
        [HttpPost("suggested-badges/{id}/dismiss")]
        public async Task<IActionResult> DismissSuggestedBadge(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int childId))
                return Unauthorized("Invalid token");

            var suggestion = await _context.AiBadgeSuggestions
                .FirstOrDefaultAsync(s => s.Id == id && s.ChildId == childId);

            if (suggestion is null) return NotFound("Suggestion not found.");

            suggestion.IsDismissed = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Suggestion dismissed." });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Children/recommended-badge
        // PHASE 12 — Weekly Badge Recommender's single best-pick output.
        // Resolves ChildProfile.RecommendedBadgeId (the AI's canonical
        // top pick) into Badge details. Distinct from /suggested-badges
        // above (the per-pillar AiBadgeSuggestion list), which is
        // unaffected by this endpoint.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("recommended-badge")]
        public async Task<IActionResult> GetRecommendedBadge()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int childId))
                return Unauthorized("Invalid token");

            var child = await _context.ChildProfiles
                .Include(c => c.RecommendedBadge)
                .FirstOrDefaultAsync(c => c.UserId == childId);

            if (child?.RecommendedBadge is null)
                return Ok(new RecommendedBadgeDto());

            var alreadyUnlocked = await _context.ChildBadges
                .AnyAsync(cb => cb.ChildId == childId && cb.BadgeId == child.RecommendedBadgeId && cb.IsActive);

            return Ok(new RecommendedBadgeDto
            {
                BadgeId = child.RecommendedBadge.Id,
                BadgeName = child.RecommendedBadge.Name,
                BadgeIcon = child.RecommendedBadge.IconUrl,
                Description = child.RecommendedBadge.Description,
                AlreadyUnlocked = alreadyUnlocked
            });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Children/pillar-detail
        // PHASE 13 — Returns all three pillar sets (Structured, NLP,
        // Final Hybrid), current level info, latest sentiment score,
        // personality cluster, recommended badge, and Leafy outputs.
        // This is the single source-of-truth read endpoint for the
        // child dashboard and parent insights pages.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("pillar-detail")]
        public async Task<IActionResult> GetPillarDetail()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int childId))
                return Unauthorized("Invalid token");

            var profile = await _context.ChildProfiles
                .Include(c => c.LastPersonalityCluster)
                .Include(c => c.RecommendedBadge)
                .Include(c => c.RecommendedLeafyTip)
                .FirstOrDefaultAsync(c => c.UserId == childId);

            if (profile is null)
                return NotFound("Child profile not found");

            // Latest sentiment from most recent saved journal entry
            var latestLog = await _context.DailyLogEntries
                .Where(l => l.ChildId == childId && l.IsJournalSaved)
                .OrderByDescending(l => l.LogDate)
                .Select(l => new { l.SentimentScore, l.DetectedEmotion, l.AnomalyFlag, l.AnomalyScore, l.ScoreVariance, l.NlpAnomalyScore })
                .FirstOrDefaultAsync();

            // Current level from UserLevels
            var userLevel = await _context.UserLevels
                .Include(ul => ul.Level)
                .FirstOrDefaultAsync(ul => ul.ChildId == childId);

            // Recommended badge check
            bool recommendedBadgeUnlocked = profile.RecommendedBadgeId.HasValue &&
                await _context.ChildBadges.AnyAsync(cb =>
                    cb.ChildId == childId &&
                    cb.BadgeId == profile.RecommendedBadgeId.Value &&
                    cb.IsActive);

            return Ok(new
            {
                // ── Structured Pillars (30-day average of daily inputs) ──
                structured = new
                {
                    spiritual = profile.SpiritualConnection_Pct,
                    social = profile.SocialInteraction_Pct,
                    academic = profile.AcademicProgress_Pct,
                    environmental = profile.HomeResponsibility_Pct,
                    physicalHealth = profile.PhysicalHealth_Pct,
                    physicalActivity = profile.PhysicalActivity_Pct,
                    creativity = profile.CreativityEngagement_Pct,
                    mental = profile.MentalWellbeing_Pct,
                    meanScore = profile.MeanScore
                },
                // ── NLP Pillars (AI-derived from free text) ──────────────
                nlp = new
                {
                    spiritual = profile.NlpSpiritualConnection_Pct,
                    social = profile.NlpSocialInteraction_Pct,
                    academic = profile.NlpAcademicProgress_Pct,
                    environmental = profile.NlpHomeResponsibility_Pct,
                    physicalHealth = profile.NlpPhysicalHealth_Pct,
                    physicalActivity = profile.NlpPhysicalActivity_Pct,
                    creativity = profile.NlpCreativityEngagement_Pct,
                    mental = profile.NlpMentalWellbeing_Pct
                },
                // ── Final Hybrid Pillars (70% Structured + 30% NLP) ─────
                final = new
                {
                    spiritual = profile.FinalSpiritualConnection_Pct,
                    social = profile.FinalSocialInteraction_Pct,
                    academic = profile.FinalAcademicProgress_Pct,
                    environmental = profile.FinalHomeResponsibility_Pct,
                    physicalHealth = profile.FinalPhysicalHealth_Pct,
                    physicalActivity = profile.FinalPhysicalActivity_Pct,
                    creativity = profile.FinalCreativityEngagement_Pct,
                    mental = profile.FinalMentalWellbeing_Pct,
                    meanScore = profile.FinalMeanScore
                },
                // ── Level ──────────────────────────────────────────────
                level = new
                {
                    levelNumber = userLevel?.LevelNumber ?? profile.Level,
                    levelName = userLevel?.Level?.Name ?? "The Sprout",
                    levelTitle = userLevel?.Level?.Title ?? "Sprout",
                    currentExp = userLevel?.CurrentExp ?? profile.CurrentExp,
                    totalExp = userLevel?.TotalExp ?? 0,
                    lastLevelUpAt = userLevel?.LastLevelUpAt
                },
                // ── Emotion Safety / Anomaly ────────────────────────────
                emotionSafety = new
                {
                    sentimentScore = latestLog?.SentimentScore,
                    detectedEmotion = latestLog?.DetectedEmotion,
                    anomalyFlag = latestLog?.AnomalyFlag ?? false,
                    anomalyScore = latestLog?.AnomalyScore ?? 0,
                    nlpAnomalyScore = latestLog?.NlpAnomalyScore ?? 0,
                    scoreVariance = latestLog?.ScoreVariance ?? 0
                },
                // ── Personality Cluster ─────────────────────────────────
                personalityCluster = profile.LastPersonalityCluster == null ? null : new
                {
                    id = profile.LastPersonalityClusterId,
                    name = profile.LastPersonalityCluster.Name,
                    description = profile.LastPersonalityCluster.Description,
                    dominantTraits = profile.LastPersonalityCluster.DominantTraits,
                    recommendedFocus = profile.LastPersonalityCluster.RecommendedFocus,
                    assignedAt = profile.PersonalityClusterAssignedAt
                },
                // ── Recommended Badge ───────────────────────────────────
                recommendedBadge = profile.RecommendedBadge == null ? null : new
                {
                    badgeId = profile.RecommendedBadge.Id,
                    badgeName = profile.RecommendedBadge.Name,
                    badgeIcon = profile.RecommendedBadge.IconUrl,
                    description = profile.RecommendedBadge.Description,
                    alreadyUnlocked = recommendedBadgeUnlocked,
                    setAt = profile.RecommendedBadgeSetAt
                },
                // ── Leafy RAG Outputs ───────────────────────────────────
                leafy = new
                {
                    tipText = profile.LeafyTipText,
                    messageText = profile.LeafyMessageText,
                    alertText = profile.LeafyAlertText,
                    setAt = profile.LeafyTextSetAt,
                    // Fallback to catalog tip if no free-text
                    catalogTip = profile.RecommendedLeafyTip?.Tip,
                    catalogAlert = profile.RecommendedLeafyTip?.Alert,
                    catalogMotivation = profile.RecommendedLeafyTip?.Motivation
                }
            });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Children/journey-progress  — PHASE 10: NEW SYSTEM ONLY
        // ─────────────────────────────────────────────────────────────
        [HttpGet("journey-progress")]
        public async Task<IActionResult> GetJourneyProgress()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int childId))
                return Unauthorized("Invalid token");

            var childProfile = await _context.ChildProfiles.FirstOrDefaultAsync(cp => cp.UserId == childId);
            if (childProfile == null) return NotFound("Profile not found");

            // ── NEW SYSTEM: journey data from NewLevelEngineService (DB-driven Levels + BadgeChallenges) ──
            var journeyData = await _newLevelEngine.GetJourneyProgressAsync(childId);

            // ── Banner stats: streak + total logs (last 90 days) + total badges ──
            // PATCH 02 FIX: Use Egypt Standard Time to match JournalService.GetOrCreateTodayEntryAsync
            // which stores LogDate in Egypt local time. Using UTC caused streak to break after
            // midnight UTC (= 2AM Egypt). Also filter IsJournalSaved=true to match NewProgressionService.
            TimeZoneInfo egyptZoneJP;
            try { egyptZoneJP = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
            catch { egyptZoneJP = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }

            var nowEgyptJP = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptZoneJP);
            var todayEgyptJP = nowEgyptJP.Date;
            var cutoff = todayEgyptJP.AddDays(-90);

            var logDates = await _context.DailyLogEntries
                .Where(l => l.ChildId == childId
                         && l.LogDate >= cutoff
                         && l.IsJournalSaved)         // FIX: only fully saved entries
                .Select(l => l.LogDate.Date)
                .Distinct()
                .ToListAsync();
            var logDateSet = logDates.ToHashSet();

            int streak = 0;
            var checkDay = todayEgyptJP;              // FIX: Egypt local date, not UTC
            while (logDateSet.Contains(checkDay)) { streak++; checkDay = checkDay.AddDays(-1); if (streak >= 365) break; }

            int totalBadges = await _context.ChildBadges.CountAsync(cb => cb.ChildId == childId && cb.IsActive);
            int totalLogs = logDateSet.Count;

            return Ok(new
            {
                currentLevel = journeyData.CurrentLevel,
                currentLevelName = journeyData.CurrentLevelName,
                progressPercentage = journeyData.ProgressPercentage,
                currentExp = journeyData.CurrentExp,
                totalExp = journeyData.TotalExp,
                expToNextLevel = childProfile.ExpToNextLevel,
                trustScore = journeyData.TrustScore,
                meanScore = journeyData.MeanScore,
                finalMeanScore = journeyData.FinalMeanScore,
                totalLogs,
                totalBadges,
                currentStreak = streak,
                nextLevelRequirements = journeyData.NextLevelRequirements,
                levelDefinitions = journeyData.LevelDefinitions
            });
        }

        [HttpPost("journal")]
        public async Task<IActionResult> SaveJournal([FromBody] SaveJournalDto dto)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
                return Unauthorized("Invalid token");

            var profile = await _context.ChildProfiles.FirstOrDefaultAsync(cp => cp.UserId == userId);
            if (profile == null) return NotFound("Child profile not found");

            // ── PHASE 10 REFACTOR: journal_entries abstraction ───────────────
            // GetOrCreateTodayEntryAsync wraps the existing DailyLogEntries
            // smart-upsert (find today's saved entry or create a new one).
            // No schema change — same underlying table/rows as before.
            var (log, isFirstSaveToday) = await _journal.GetOrCreateTodayEntryAsync(userId);

            bool hasChores = dto.CompletedChores?.Any() == true;

            // ApplyJournalFields performs the same field-mapping that used to
            // be inline here (including the new optional SleepHours/SleepNote
            // mapping onto the existing DailyLogEntry columns).
            _journal.ApplyJournalFields(log, dto);
            PillarInsightsHelper.UpdatePastIllnessSummary(profile, log);

            // ── EXP System ────────────────────────────────────────────────────────
            // Always calculate the FULL earned EXP based on what the child submitted.
            // On re-saves we only add the DELTA (newFullExp - previouslyAwarded) to
            // profile.CurrentExp so there's no double-counting, but we always store
            // the full amount in log.ExpAwarded so history.html shows an accurate
            // number that grows as the child fills in more items.
            //
            // BUG THAT WAS HERE BEFORE (EXP_HISTORY_BUG):
            // Re-saves used exp = Math.Max(5, fullExp/4) which both:
            //   (a) double-counted partial EXP already added to profile.CurrentExp, AND
            //   (b) stored the tiny reduced value into log.ExpAwarded, so history
            //       permanently showed a small number (e.g. +18) even after the child
            //       filled in everything — because ExpAwarded was overwritten with
            //       the penalty value, not the earned value.
            int exp = 10;
            bool leveledUp = false;

            if (dto.CompletedSubjects?.Any() == true) exp += 15;
            if (dto.SpiritualActivities?.Any() == true) exp += 10;
            if (!string.IsNullOrEmpty(dto.PhysicalActivity)) exp += 8;
            if (!string.IsNullOrEmpty(dto.SocialTimeWith)) exp += 8;
            if (dto.HelpedFamily || hasChores) exp += 10;
            if (!string.IsNullOrEmpty(dto.ReflectionText)) exp += 12;
            if (dto.WaterGlasses >= 4) exp += 5;
            // AUDIT FIX: journal.html's "Why do you feel this way?" textarea now
            // sends FeelingNote instead of MoodReason (see journal.html rename) —
            // updated here too so this XP bonus still triggers correctly.
            if (!string.IsNullOrEmpty(dto.FeelingNote)) exp += 7;
            if (dto.ChallengeHandling?.Any() == true) exp += 8;

            // How much was already credited for this log entry on a prior save?
            // On re-saves: only give the child the INCREMENTAL difference.
            // On first saves: previousExpAwarded = 0, so delta = full exp.
            int previousExpAwarded = isFirstSaveToday ? 0 : log.ExpAwarded;
            int expDelta = Math.Max(0, exp - previousExpAwarded);

            // حفظ السطر الحالي ليصبح متاحاً للـ Engine
            // PHASE 11: mark IsJournalSaved BEFORE calling the pipeline so
            // today's entry is included when RecalculateProfileAndTraitsAsync
            // queries DailyLogEntries WHERE IsJournalSaved = true.
            log.IsJournalSaved = true;
            await _context.SaveChangesAsync();

            // ── PHASE 11: Single Pillars/Traits Pipeline ──────────────────────
            // STEP 1 — daily pillars for THIS entry (stored on DailyXxx_Pct).
            // STEP 2 — ChildProfile 8 pillars = 30-day mean of DailyXxx_Pct.
            // STEP 3 — 5 traits re-derived from those aggregates only.
            // Replaces: PillarCalculationEngine.FromWeeklyEntries(90-day) +
            //           ApplyToProfile + WeeklyTraitSynthesisService.ComputeTraits.
            var dailyResult = _pillarPipeline.ApplyDailyPillars(log);
            await _context.SaveChangesAsync(); // flush DailyXxx_Pct to DB before aggregate query
            await _pillarPipeline.RecalculateProfileAndTraitsAsync(userId);

            // ── SYNC fixed tasks from SpiritualActivities ─────────────────────
            // Fixed tasks (IsFixedTask = true) represent the same spiritual
            // activities shown as toggle buttons in the journal (Fajr, Dhuhr,
            // Asr, Maghrib, Isha, Reading Quran, Athkar, Doaa). When the child
            // saves the journal, we sync IsCompletedToday on each matching
            // ChildTask row so that:
            //   1. TotalTasksCount / CompletedTasksCount on ChildProfile stay
            //      accurate for the AI team's Anomaly Detection Trust Engine.
            //   2. The nightly DailyLogClosureService can reset them via
            //      IsCompletedToday = false (same as before — no change there).
            var fixedTasks = await _context.ChildTasks
                .Where(t => t.ChildId == userId && t.IsFixedTask)
                .ToListAsync();

            if (fixedTasks.Any())
            {
                // Normalise the submitted list: trim + lowercase for comparison
                var selectedActivities = (dto.SpiritualActivities ?? new List<string>())
                    .Select(a => a.Trim().ToLowerInvariant())
                    .ToHashSet();

                foreach (var task in fixedTasks)
                {
                    task.IsCompletedToday = selectedActivities.Contains(task.Title.Trim().ToLowerInvariant());
                }
            }

            // Recompute TotalTasksCount / CompletedTasksCount after the sync above
            await _pillarPipeline.RecalculateTaskCountsAsync(userId);

            // ── Gamification metrics are now computed internally by
            // NewProgressionService.RunAsync() (called below) — streak,
            // totalLogs, prayer/quran/water/hobby counters, movement badges,
            // tasks_completed, and pillar %s are all re-derived there.

            // ── PHASE 10: New Progression Engine ──────────────────────────────
            // Replaces: LevelCheckContext, LevelRequirementsEngine.ComputeEarnedLevel,
            //           and the direct `profile.Level = earnedLevel` write.
            //
            // NewProgressionService internally re-derives all the metrics that
            // used to be packed into LevelCheckContext (streak, totalLogs,
            // tasks_completed, prayer/quran/water/hobby counters, pillar %s,
            // movement badges, etc.) directly from DailyLogEntries + ChildProfile,
            // then:
            //   1. updates ChildChallengeProgresses
            //   2. unlocks any badges whose challenges are now ALL complete
            //   3. awards EXP + evaluates level-up (writes UserLevels,
            //      mirrors the new level into profile.Level)
            var progressionResult = await _newProgression.RunAsync(userId);
            leveledUp = progressionResult.LeveledUp;

            if (leveledUp)
            {
                // TrustScore bonus for leveling up (kept for trust history)
                await _gamingService.UpdateScore(userId, 10 * progressionResult.CurrentLevel);
            }

            // Add only the incremental EXP to the running total.
            // expDelta = 0 if the child re-saved without adding any new items,
            //          = full exp on first save,
            //          = difference on re-save with more items filled.
            profile.CurrentExp += expDelta;

            // ── Audit Delta Fields ─────────────────────────────────────────────
            // PHASE 11: sourced from this entry's own daily pillars (Step 1
            // output), not the removed 90-day weekly engineResult.
            log.DeltaAcademic = dailyResult.AcademicProgress_Pct;
            log.DeltaReligious = dailyResult.SpiritualConnection_Pct;
            log.DeltaPhysical = dailyResult.PhysicalHealth_Pct;
            log.DeltaSocial = dailyResult.SocialInteraction_Pct;
            log.DeltaEmotional = dailyResult.MentalWellbeing_Pct;
            log.DeltaResilience = dailyResult.MentalWellbeing_Pct; // PHASE10-FIX-C: was HomeResponsibility_Pct

            // Store the FULL earned EXP (not a reduced penalty) so history.html
            // always displays an accurate, growing number.
            log.ExpAwarded = exp;
            log.TriggeredLevelUp = leveledUp;
            log.LevelAfter = profile.Level;

            await _context.SaveChangesAsync();

            // Fire ParentNotificationLogic observer (TrustScore alert check) even
            // when no level-up occurred — matches pre-Phase-10 behavior
            // (points=0 means no TrustScore change, but observers still run).
            if (!leveledUp)
                await _gamingService.UpdateScore(userId, 0);

            _cache.RemoveByPrefix(CacheKeys.ChildPrefix(userId));

            // ── PHASE 10: fetch full details for any badges unlocked this run ──
            var newlyUnlockedBadges = new List<NewBadgeDto>();
            if (progressionResult.NewlyUnlockedBadgeIds.Any())
            {
                newlyUnlockedBadges = await _context.Badges
                    .Where(b => progressionResult.NewlyUnlockedBadgeIds.Contains(b.Id))
                    .Select(b => new NewBadgeDto
                    {
                        Id = b.Id,
                        Name = b.Name,
                        Description = b.Description,
                        IconUrl = b.IconUrl
                    })
                    .ToListAsync();
            }

            var userLevel = await _newLevelEngine.GetOrCreateUserLevelAsync(userId);

            // Compute HashedChildId for spec compliance (journal_entries hashed_child_id)
            var hashedChildId = _childIdentity.GetSafeChildKey(userId);

            return Ok(new JournalSaveResultDto
            {
                Message = leveledUp
                    ? $"🎉 LEVEL UP! You're now Level {profile.Level} — {progressionResult.LevelName}!"
                    : isFirstSaveToday
                        ? "Your day has been saved! 🌸"
                        : "Your day's tracking has been successfully updated! ✏️",
                // Return the incremental delta earned THIS save for the toast/banner,
                // not the full cumulative exp for the day (which is stored in log.ExpAwarded).
                ExpAwarded = expDelta,
                TotalExp = userLevel.TotalExp,
                Level = profile.Level,
                LevelName = userLevel.Level?.Name ?? progressionResult.LevelName,
                ExpToNextLevel = profile.ExpToNextLevel,
                LeveledUp = leveledUp,
                IsFirstSave = isFirstSaveToday,
                NewlyUnlockedBadges = newlyUnlockedBadges,
                HashedChildId = hashedChildId,
                Aspects = new AspectProgressDto { /* المابينج بتاعك زي ما هو تماماً */ }
            });
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Children/today-journal
        // ─────────────────────────────────────────────────────────────
        // GET /api/Children/today-journal
        // ─────────────────────────────────────────────────────────────
        // ════════════════════════════════════════════════════════════════════
        // GET /api/Children/today-journal  —  مصحح
        // ════════════════════════════════════════════════════════════════════
        [HttpGet("today-journal")]
        public async Task<IActionResult> GetTodayJournal()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
                return Unauthorized("Invalid token");

            var egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
            var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptZone).Date;

            var log = await _context.DailyLogEntries
                .FirstOrDefaultAsync(l => l.ChildId == userId && l.LogDate.Date == today && l.IsJournalSaved);

            if (log == null)
                return NoContent();

            double moodPct = log.SentimentScore.HasValue
                ? (log.SentimentScore.Value + 1.0) * 50
                : 50;

            return Ok(new TodayJournalDto
            {
                SpiritualActivities = log.SpiritualActivities,
                SocialTimeWith = log.SocialTimeWith,
                CompletedSubjects = log.CompletedSubjects,
                CompletedChores = log.CompletedChores,
                HelpedFamily = log.HelpedFamily,
                NutritionItems = log.NutritionItems,
                PhysicalActivity = log.PhysicalActivity,
                FunActivities = log.FunActivities,
                WaterGlasses = log.WaterGlasses,
                MoodLevel = log.MoodLevel,
                MoodEmoji = log.MoodEmoji,
                MoodReason = log.MoodReason,
                ReflectionText = log.ReflectionText,
                FeelingNote = log.FeelingNote,
                MessageToParent = log.MessageToParent,
                StudyingPct = Math.Round(log.StudyingPct, 1),
                SpiritualPct = Math.Round(log.SpiritualPct, 1),
                MovingPct = Math.Round(log.MovingPct, 1),
                FoodPct = Math.Round(log.FoodPct, 1),
                ChorsPct = Math.Round(log.ChorsPct, 1),
                HobbiesPct = Math.Round(log.HobbiesPct, 1),
                SocialPct = Math.Round(log.SocialPct, 1),
                MoodPct = Math.Round(moodPct, 1),
                LogDuration = log.LogDuration,
                EntryCount = 1,
                HasEntry = true,

                // ── Sleep (Phase 10) ──────────────────────────────────
                SleepHours = log.SleepHours,
                SleepNote = log.SleepNote,
                WasIllToday = log.WasIllToday,
                IllnessType = log.IllnessType,
                IllnessNote = log.IllnessNote,

                // ── ✅ الحجات اللي كانت ناقصة — اتضافت ───────────────
                // Phase 7: جودة التواصل الاجتماعي
                SocialQualityScore = log.SocialQualityScore,

                // Phase 7: مواجهة التحديات
                ChallengeHandling = log.ChallengeHandling,
            });
        }
        // ─────────────────────────────────────────────────────────────
        // GET /api/Children/weekly-snapshot
        // ─────────────────────────────────────────────────────────────
        [HttpGet("weekly-snapshot")]
        public async Task<IActionResult> GetWeeklySnapshot()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
                return Unauthorized("Invalid token");

            var profile = await _context.ChildProfiles
                .Include(cp => cp.User)
                .FirstOrDefaultAsync(cp => cp.UserId == userId);

            if (profile == null) return NotFound("Child profile not found");

            return Ok(new WeeklySnapshotDto
            {
                ChildId = userId,
                FullName = profile.User?.FullName ?? "",
                Age = profile.Age,
                Level = profile.Level,

                // Processed 8 pillar scores
                SpiritualConnection_Pct = profile.SpiritualConnection_Pct,
                SocialInteraction_Pct = profile.SocialInteraction_Pct,
                AcademicProgress_Pct = profile.AcademicProgress_Pct,
                HomeResponsibility_Pct = profile.HomeResponsibility_Pct,
                PhysicalHealth_Pct = profile.PhysicalHealth_Pct,
                PhysicalActivity_Pct = profile.PhysicalActivity_Pct,
                CreativityEngagement_Pct = profile.CreativityEngagement_Pct,
                MentalWellbeing_Pct = profile.MentalWellbeing_Pct,

                // 5 Core Personality Traits
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

        // ─────────────────────────────────────────────────────────────
        // GET /api/Children/journal-history-v2
        //
        // Called by history.html to display the child's journal memory
        // cards. Returns the last 30 DailyLogEntry rows (multi-entry
        // aware: groups by date → picks the latest entry per day for
        // the primary card, sums ExpAwarded for the day).
        //
        // Response shape expected by history.html:
        //   [ { entryDate, moodEmoji, moodLevel, reflection,
        //       expAwarded, triggeredLevelUp }, … ]
        // ─────────────────────────────────────────────────────────────
        [HttpGet("journal-history-v2")]
        public async Task<IActionResult> GetJournalHistoryV2()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
                return Unauthorized("Invalid token");

            // Pull the last 60 entries (covers ~30 days with multi-save)
            var rawEntries = await _context.DailyLogEntries
                .Where(l => l.ChildId == userId && l.IsJournalSaved)
                .OrderByDescending(l => l.LogDate)
                .Take(60)
                .ToListAsync();

            // Group by calendar date (UTC). Per date: pick the LAST entry
            // for mood/reflection, SUM expAwarded, OR triggeredLevelUp.
            var grouped = rawEntries
                .GroupBy(e => e.LogDate.Date)
                .OrderByDescending(g => g.Key)
                .Take(30)
                .Select(g =>
                {
                    var latest = g.OrderByDescending(e => e.LogDate).First();
                    return new
                    {
                        entryDate = g.Key.ToString("yyyy-MM-dd"),
                        moodEmoji = latest.MoodEmoji ?? "",
                        moodLevel = latest.MoodLevel ?? "",
                        reflection = latest.ReflectionText ?? latest.FeelingNote ?? "",
                        expAwarded = g.Sum(e => e.ExpAwarded),
                        triggeredLevelUp = g.Any(e => e.TriggeredLevelUp),
                        entryCount = g.Count()
                    };
                })
                .ToList();

            return Ok(grouped);
        }

        // ─────────────────────────────────────────────────────────────
        // GET /api/Children/weekly-quests
        //
        // Called by heroprof.html to hydrate the 5-trait weekly quest
        // tracker. Derives quest completion from DailyLogEntries in the
        // current Mon–Sun week window.
        //
        // Response shape expected by heroprof.html:
        //   {
        //     quests: {
        //       spiritual:  [bool×7],   // true = logged on that weekday
        //       kindness:   [bool×7],
        //       curiosity:  [bool×7],
        //       vitality:   [bool×7],
        //       resilience: [bool×7]
        //     }
        //   }
        //
        // Quest rules (maps traits → pillars):
        //   spiritual  → SpiritualPct  > 0
        //   kindness   → SocialPct > 0 OR ChorsPct > 0
        //   curiosity  → StudyingPct > 0 OR HobbiesPct > 0
        //   vitality   → MovingPct > 0 OR FoodPct > 0
        //   resilience → MoodPct > 0 or MoodEmoji not null
        // ─────────────────────────────────────────────────────────────
        [HttpGet("weekly-quests")]
        public async Task<IActionResult> GetWeeklyQuests()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
                return Unauthorized("Invalid token");

            // Build Mon–Sun window for the current week (UTC)
            var today = DateTime.UtcNow.Date;
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            var weekStart = today.AddDays(-diff);   // Monday
            var weekEnd = weekStart.AddDays(6);   // Sunday

            var entries = await _context.DailyLogEntries
                .Where(l => l.ChildId == userId
                         && l.LogDate.Date >= weekStart
                         && l.LogDate.Date <= weekEnd)
                .ToListAsync();

            // Build bool[7] for each trait (index 0 = Monday … 6 = Sunday)
            var spiritual = new bool[7];
            var kindness = new bool[7];
            var curiosity = new bool[7];
            var vitality = new bool[7];
            var resilience = new bool[7];

            foreach (var entry in entries)
            {
                int dayIdx = (int)(entry.LogDate.Date - weekStart).TotalDays;
                if (dayIdx < 0 || dayIdx > 6) continue;

                // Spiritual: logged any spiritual activity (Pct may be 0 — use activity tags)
                if (entry.SpiritualPct > 0 || !string.IsNullOrWhiteSpace(entry.SpiritualActivities))
                    spiritual[dayIdx] = true;

                // Kindness: spent time with others OR helped with chores
                if (entry.SocialPct > 0 || entry.ChorsPct > 0
                    || !string.IsNullOrWhiteSpace(entry.SocialTimeWith)
                    || !string.IsNullOrWhiteSpace(entry.CompletedChores)
                    || entry.HelpedFamily)
                    kindness[dayIdx] = true;

                // Curiosity: studied subjects OR did hobbies/fun activities
                if (entry.StudyingPct > 0 || entry.HobbiesPct > 0
                    || !string.IsNullOrWhiteSpace(entry.CompletedSubjects)
                    || !string.IsNullOrWhiteSpace(entry.FunActivities))
                    curiosity[dayIdx] = true;

                // Vitality: did physical activity OR logged nutrition/water
                if (entry.MovingPct > 0 || entry.FoodPct > 0
                    || !string.IsNullOrWhiteSpace(entry.PhysicalActivity)
                    || !string.IsNullOrWhiteSpace(entry.NutritionItems)
                    || entry.WaterGlasses >= 4)
                    vitality[dayIdx] = true;

                // Resilience: logged mood OR journaled about challenges
                if (!string.IsNullOrEmpty(entry.MoodEmoji) || !string.IsNullOrEmpty(entry.MoodLevel)
                    || !string.IsNullOrWhiteSpace(entry.ChallengeHandling)
                    || !string.IsNullOrWhiteSpace(entry.ReflectionText))
                    resilience[dayIdx] = true;
            }

            return Ok(new
            {
                weekStart = weekStart.ToString("yyyy-MM-dd"),
                weekEnd = weekEnd.ToString("yyyy-MM-dd"),
                quests = new
                {
                    spiritual,
                    kindness,
                    curiosity,
                    vitality,
                    resilience
                }
            });
        }
    }
}
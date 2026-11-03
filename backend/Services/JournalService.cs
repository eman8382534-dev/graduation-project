// ============================================================
//  JournalService.cs — Phase 10 Refactor: Journal Abstraction Layer
//
//  PURPOSE:
//  The system spec calls for a `journal_entries` table/abstraction.
//  THE PRODUCTION DATABASE IS LOCKED — no migrations, no renamed
//  or new tables. Journal data continues to live in the existing
//  `DailyLogEntries` table/DbSet exactly as before.
//
//  This service exposes that same table behind a stable,
//  spec-aligned abstraction:
//
//      SaveJournalAsync(childId, dto)        -> upserts "today's"
//                                                journal_entries row
//      GetJournalEntriesAsync(childId, days) -> reads recent
//                                                journal_entries rows
//
//  Internally these map 1:1 onto DailyLogEntries (the
//  "journal_entries" name is a CODE-LAYER ALIAS ONLY — no DB
//  rename, no new DbSet, no schema change).
//
//  ChildController.SaveJournal / GetJournalHistory / GetDailyLogs
//  continue to own the response-shaping (DTOs, EXP, progression),
//  but now delegate the "find today's entry / upsert fields /
//  fetch recent rows" persistence steps to this service so there
//  is a single, named source of truth for "journal_entries" access.
// ============================================================
using GrawiaaApp.API.Data;
using GrawiaaApp.API.DTOs;
using GrawiaaApp.API.Models;
using Microsoft.EntityFrameworkCore;

namespace GrawiaaApp.API.Services
{
    public interface IJournalService
    {
        Task<(DailyLogEntry entry, bool isFirstSaveToday)> GetOrCreateTodayEntryAsync(int childId);
        void ApplyJournalFields(DailyLogEntry entry, SaveJournalDto dto);
        Task<List<DailyLogEntry>> GetJournalEntriesAsync(int childId, int lookbackDays = 90, bool onlySaved = true);
    }

    public class JournalService : IJournalService
    {
        private readonly ApplicationDbContext _context;
        private readonly IChildIdentityService _childIdentity;

        public JournalService(ApplicationDbContext context, IChildIdentityService childIdentity)
        {
            _context = context;
            _childIdentity = childIdentity;
        }

        public async Task<(DailyLogEntry entry, bool isFirstSaveToday)> GetOrCreateTodayEntryAsync(int childId)
        {
            // FIX (FEELINGNOTE_FIX): تحديد "اليوم" بناءً على توقيت مصر القياسي
            var egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
            var nowEgypt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptZone);
            var today = nowEgypt.Date;

            // FIX (FEELINGNOTE_FIX): البحث عن أي صف لليوم ده بدون شرط IsJournalSaved —
            // الشرط القديم كان بيسبب انفصال FeelingNote/GeneralNote/MessageToParent عن الـ journal:
            // لو الطفل فتح الـ journal قبل ما يكتب في leafy (اللي بتخلق صف IsJournalSaved=false)،
            // الـ GetOrCreate القديمة ما كانتش تلاقي الصف ده وكانت تعمل صف تاني — فكل الـ notes
            // بتتفرق على صفين مختلفين والـ AI ما يلاقيش كل البيانات في مكان واحد.
            //
            // الإصلاح: دور على آخر صف لليوم بـ LogDate.Date == today بس، بغض النظر عن IsJournalSaved.
            // لو ما فيش صف خالص → اعمل واحد جديد (isFirstSaveToday = true).
            // لو لاقيت صف موجود (سواء IsJournalSaved=false أو =true) → حدّث عليه (isFirstSaveToday = !IsJournalSaved).
            // كده الـ journal والـ daily question والـ message to parent كلهم بيكتبوا على نفس الصف مضمون.
            var entry = await _context.DailyLogEntries
                .Where(l => l.ChildId == childId && l.LogDate.Date == today)
                .OrderByDescending(l => l.LogDate)   // آخر صف لليوم (لو في multi-entries)
                .FirstOrDefaultAsync();

            // BUG FIX (EXP_RESAVE_BUG): isFirstSaveToday used to check !IsJournalSaved,
            // but QuickCheckIn (leafy.html) sets IsJournalSaved = true WITHOUT setting
            // ExpAwarded. So when journal.html later called SaveJournal on the same day,
            // it found IsJournalSaved = true and treated the save as a re-edit →
            // exp = Math.Max(5, total/4) instead of full EXP. The child saw tiny EXP
            // (e.g. +18 instead of the full amount they deserved).
            //
            // Fix: use ExpAwarded == 0 as the "journal.html hasn't saved yet" signal.
            // QuickCheckIn never sets ExpAwarded, so it stays 0 after check-in.
            // The first time journal.html saves → isFirstSaveToday = true → full EXP.
            // The second time journal.html saves same day → ExpAwarded > 0 → reduced EXP.
            bool isFirstSaveToday = entry == null || entry.ExpAwarded == 0;

            if (entry == null)
            {
                entry = new DailyLogEntry
                {
                    ChildId = childId,
                    HashedChildId = _childIdentity.GetSafeChildKey(childId),
                    LogDate = nowEgypt   // توقيت مصر الفعلي
                };
                _context.DailyLogEntries.Add(entry);
            }
            else
            {
                // الصف موجود → حدّث وقت التعديل فقط (LogDate بدلاً من إنشاء صف جديد)
                entry.LogDate = nowEgypt;
                if (string.IsNullOrEmpty(entry.HashedChildId))
                    entry.HashedChildId = _childIdentity.GetSafeChildKey(childId);
            }

            return (entry, isFirstSaveToday);
        }

        public void ApplyJournalFields(DailyLogEntry entry, SaveJournalDto dto)
        {
            bool hasChores = dto.CompletedChores?.Any() == true;

            entry.SpiritualActivities = string.Join(", ", dto.SpiritualActivities ?? new List<string>());
            entry.PhysicalActivity = dto.PhysicalActivity;
            entry.WaterGlasses = dto.WaterGlasses;
            entry.SocialTimeWith = dto.SocialTimeWith;
            entry.CompletedSubjects = string.Join(", ", dto.CompletedSubjects ?? new List<string>());
            entry.MoodLevel = dto.MoodLevel;

            entry.MoodEmoji = dto.MoodEmoji;
            if (string.IsNullOrEmpty(entry.MoodEmoji))
            {
                if (dto.MoodPct >= 80) entry.MoodEmoji = "😊";
                else if (dto.MoodPct >= 60) entry.MoodEmoji = "🙂";
                else if (dto.MoodPct >= 40) entry.MoodEmoji = "😐";
                else if (dto.MoodPct >= 20) entry.MoodEmoji = "🙁";
                else entry.MoodEmoji = "😢";
            }

            entry.MoodReason = dto.MoodReason;
            entry.NutritionItems = string.Join(", ", dto.NutritionItems ?? new List<string>());
            entry.HelpedFamily = dto.HelpedFamily || hasChores;
            entry.CompletedChores = string.Join(", ", dto.CompletedChores ?? new List<string>());
            entry.FunActivities = string.Join(", ", dto.FunActivities ?? new List<string>());
            entry.ReflectionText = dto.ReflectionText;

            entry.ChallengeHandling = dto.ChallengeHandling?.Count > 0
                ? string.Join(", ", dto.ChallengeHandling)
                : null;

            if (dto.SocialQualityScore > 0)
            {
                entry.SocialQualityScore = dto.SocialQualityScore;
            }
            else
            {
                bool hasSocialContact = !string.IsNullOrWhiteSpace(dto.SocialTimeWith);
                bool spentAlone = dto.SocialTimeWith?.Contains("Alone", StringComparison.OrdinalIgnoreCase) == true;
                bool askedForHelp = dto.ChallengeHandling?.Any(c =>
                    c.Contains("Asked for help", StringComparison.OrdinalIgnoreCase)) == true;

                entry.SocialQualityScore = hasSocialContact
                    ? (spentAlone ? 40.0 : (askedForHelp ? 85.0 : 70.0))
                    : 30.0;
            }

            entry.LogDuration = dto.LogDuration;
            entry.StudyingPct = dto.StudyingPct;
            entry.SpiritualPct = dto.SpiritualPct;
            entry.MovingPct = dto.MovingPct;
            entry.FoodPct = dto.FoodPct;
            entry.ChorsPct = dto.ChorsPct;
            entry.HobbiesPct = dto.HobbiesPct;
            entry.SocialPct = dto.SocialPct;
            entry.FeelingNote = dto.FeelingNote;

            // GUARD: journal.html does not have a messageToParent field at
            // all, so dto.MessageToParent is always null/empty when this
            // runs from the normal journal-save flow. Without this guard,
            // saving the journal would silently overwrite — and erase —
            // any message the child already sent today via leafy.html's
            // send-message-to-parent (ChildController.SendMessageToParent
            // writes directly to this same column). Only overwrite when
            // this particular save actually carried a real message, so
            // the two write paths can never stomp on each other.
            if (!string.IsNullOrWhiteSpace(dto.MessageToParent))
                entry.MessageToParent = dto.MessageToParent;

            if (dto.SleepHours.HasValue)
                entry.SleepHours = dto.SleepHours.Value;

            if (!string.IsNullOrWhiteSpace(dto.SleepNote))
                entry.SleepNote = dto.SleepNote;

            entry.WasIllToday = dto.WasIllToday;
            entry.IllnessType = dto.IllnessType;
            entry.IllnessNote = dto.IllnessNote;
        }

        public async Task<List<DailyLogEntry>> GetJournalEntriesAsync(int childId, int lookbackDays = 90, bool onlySaved = true)
        {
            // 👇 التعديل هنا: حساب الـ Cutoff التاريخي بناءً على تاريخ مصر اليوم
            var egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
            var cutoff = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptZone).Date.AddDays(-lookbackDays);

            var query = _context.DailyLogEntries
                .Where(l => l.ChildId == childId && l.LogDate >= cutoff);

            if (onlySaved)
                query = query.Where(l => l.IsJournalSaved);

            return await query
                .OrderByDescending(l => l.LogDate)
                .ToListAsync();
        }
    }
}
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace GrawiaaApp.API.DTOs
{
    // ==================== 1. AUTHENTICATION ====================

    public class ParentSignUpDto
    {
        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string Password { get; set; } = string.Empty;

        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class RegisterChildStandaloneDto
    {
        [Required(ErrorMessage = "Username is required")]
        [StringLength(20, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full name is required")]
        public string FullName { get; set; } = string.Empty;

        [Range(4, 15)]
        public int Age { get; set; }

        [Required]
        public string CharacterType { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string CharacterName { get; set; } = string.Empty;

        public List<string> Hobbies { get; set; } = new();

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        public IFormFile? ImageFile { get; set; }
        public string? ParentEmail { get; set; }
    }

    public class LoginDto
    {
        [Required(ErrorMessage = "Username or email is required")]
        public string UsernameOrEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int UserId { get; set; }
        public bool PasswordResetRequired { get; set; }
        public string CharacterName { get; set; } = string.Empty;
        public bool IsStandalone { get; set; }
    }

    // ==================== 2. DAILY LOGS ====================

    public class AutoSaveLogDto
    {
        public List<string> SpiritualActivities { get; set; } = new();
        public string? SocialTimeWith { get; set; }
        public List<string> CompletedSubjects { get; set; } = new();
        public bool HelpedFamily { get; set; }
        public List<string> NutritionItems { get; set; } = new();
        public int WaterGlasses { get; set; }
        public string? PhysicalActivity { get; set; }
        public List<string> FunActivities { get; set; } = new();
        public string? MoodLevel { get; set; }
    }

    public class CreateDailyLogDto
    {
        public string? MoodEmoji { get; set; }
        public string? FoodNote { get; set; }
        public string? ActivityNote { get; set; }
        public string? LearningNote { get; set; }
        public string? SleepNote { get; set; }
        public bool IsFinalized { get; set; } = false;
    }

    // ==================== 3. PROFILE & JOURNEY ====================

    public class ChildProfileDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public int Level { get; set; }
        public int CurrentExp { get; set; }
        public int TrustScore { get; set; }
        public string CharacterName { get; set; } = "Hero";
        public string CharacterType { get; set; } = string.Empty;
        public bool IsStandalone { get; set; }
        public List<BadgeDto> Badges { get; set; } = new();

        // Radar / progress chart data
        public double AcademicPct { get; set; }
        public double EmotionalPct { get; set; }
        public double MentalPct { get; set; }
        public double ReligiousPct { get; set; }
        public double PhysicalPct { get; set; }
        public double SocialPct { get; set; }
    }

    public class BadgeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PersonalitySide { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public string? IconUrl { get; set; }
        public bool IsUnlocked { get; set; }
    }


    public class UserCountDto
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int ChildUsers { get; set; }
        public int ParentUsers { get; set; }
        public int AdminUsers { get; set; }
    }

    public class ChildSignupDto
    {
        [Required(ErrorMessage = "Full name is required, hero!")]
        [MinLength(3, ErrorMessage = "Full name must be at least 3 chars")]
        [MaxLength(30, ErrorMessage = "Full name must not exceed 30 chars")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Age is required")]
        [Range(5, 18, ErrorMessage = "Age must be between 5 and 18")]
        public int Age { get; set; }

        [Required(ErrorMessage = "Username is required")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Only letters, numbers, and _ allowed")]
        public string Username { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Parent email format is invalid")]
        public string? ParentEmail { get; set; }

        [EmailAddress(ErrorMessage = "Your email format is invalid")]
        public string? ChildPersonalEmail { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [MinLength(5, ErrorMessage = "Password must be at least 5 characters")]
        public string Password { get; set; } = string.Empty;

        public string? ConfirmPassword { get; set; }

        [Required(ErrorMessage = "You must choose your character!")]
        public string CharacterType { get; set; } = string.Empty;

        public string? CharacterName { get; set; }
        public List<string> Hobbies { get; set; } = new();
    }

    // ==================== 4. STATISTICS & CHARTS ====================

    public class StatsResponseDto
    {
        public int TotalLogs { get; set; }
        public int TotalTasks { get; set; }
        public PieChartData PieChartData { get; set; } = new();
    }

    public class PieChartData
    {
        public double Academic { get; set; }
        public double Physical { get; set; }
        public double Religious { get; set; }
        public double Social { get; set; }
    }

    // ==================== 5. DAILY QUESTION ====================

    public class DailyQuestionAnswerDto
    {
        [Required, StringLength(500)]
        public string Answer { get; set; } = string.Empty;
    }

    public class LeafyChatDto
    {
        [Required, StringLength(500)]
        public string Message { get; set; } = string.Empty;
    }

    // ==================== 6. MESSAGING ====================

    public class ChildMessageDto
    {
        [Required, StringLength(500)]
        public string Content { get; set; } = string.Empty;
    }

    // ==================== 7. PARENT LINKING ====================

    public class RequestParentLinkDto
    {
        [Required(ErrorMessage = "Parent email is required")]
        [EmailAddress]
        public string ParentEmail { get; set; } = string.Empty;
    }

    public class ConfirmChildLinkDto
    {
        public string LinkToken { get; set; } = string.Empty;
        public string? ParentEmail { get; set; }
    }

    // ==================== 8. TASKS & REWARDS ====================

    public class AddTaskDto
    {
        [Required]
        public int ChildId { get; set; }

        [Required, StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Range(1, 1000)]
        public int Points { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        [Required]
        public string Category { get; set; } = "General";
    }

    public class AddRewardDto
    {
        [Required, StringLength(100)]
        public string Title { get; set; } = string.Empty;

        // FIX: Changed minimum from 10 to 1.
        // The old [Range(10, 10000)] caused a silent 400 error when the parent
        // entered any cost between 1 and 9 (e.g. "5 points"). The frontend
        // shows no specific validation for this range, so the user would see a
        // generic failure with no explanation. Lowering to 1 matches reasonable
        // UX expectations (a 1-point reward is valid). The upper bound stays
        // at 10 000 to prevent data corruption.
        [Range(1, 10000, ErrorMessage = "Points cost must be between 1 and 10,000.")]
        public int Cost { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public int ChildId { get; set; }
    }

    public class ClaimRewardDto
    {
        [Required]
        public int RewardId { get; set; }
    }

    // ==================== 9. HERO MANAGEMENT (DTO ALIGNMENT FIX) ====================

    public class HeroManagementDto
    {
        public string? Name { get; set; }
        public int? Age { get; set; }
        public List<string>? Chores { get; set; }
        public List<string>? Subjects { get; set; }
        public List<string>? Sports { get; set; }
    }

    public class DailyManagementDto : HeroManagementDto { }

    // ==================== 10. PARENT DASHBOARD ====================

    public class ChildHeroDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public int Age { get; set; }
        public string CharacterType { get; set; } = "The Caring Explorer";
        public string? AvatarUrl { get; set; }
        public int TrustScore { get; set; }
        public int Level { get; set; }
        public double CuriosityPct { get; set; }
        public double PersistencePct { get; set; }
        public double KindnessPct { get; set; }
        public double SpiritualPct { get; set; }
        public double VitalityPct { get; set; }
    }

    public class ParentDashboardDto
    {
        public List<AlertDto> Alerts { get; set; } = new();
        public List<ChildHeroDto> Heroes { get; set; } = new();
    }

    public class AlertDto
    {
        public string Type { get; set; } = "Success";
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    // ==================== 11. NLP & ANALYSIS ====================

    public class AnalyzeTextDto
    {
        public string Emotion { get; set; } = string.Empty;
        public double Sentiment { get; set; }
        public bool IsHonest { get; set; }
        public string? DetectedText { get; set; }
    }

    public class RecommendationDto
    {
        public string Category { get; set; } = string.Empty;
        public string Suggestion { get; set; } = string.Empty;
    }

    // ==================== 12. HERO INSIGHTS ====================

    public class HeroInsightsDto
    {
        public HeroHeaderDto Header { get; set; } = new();
        public CategoryStats Categories { get; set; } = new();
        public List<ActivityHourDto> ActivityHours { get; set; } = new();
        public List<string> Trends { get; set; } = new();
        public List<string> RedFlags { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public List<PersonalizedAlertDto> PersonalizedAlerts { get; set; } = new();
        public List<ActivityRecommendationDto> ActivityRecommendations { get; set; } = new();
        public PillarsDto Pillars { get; set; } = new();
        public TraitsDto Traits { get; set; } = new();
        public int HonestPoints { get; set; }
        public double TrustScore { get; set; }

        /// <summary>Number of distinct days the child has logged in the last 7 days (1–7).</summary>
        public int DaysLoggedThisWeek { get; set; }

        /// <summary>"Today" if only today was logged, "This Week" otherwise.</summary>
        public string SummaryPeriodLabel { get; set; } = "This Week";

        /// <summary>
        /// Phase 12 — AI team outputs surfaced for parent transparency:
        /// recommended badge, Leafy recommendation, personality cluster,
        /// and anomaly/consistency signal. All sourced as-is from AI-written
        /// fields; never recomputed here.
        /// </summary>
        public AiInsightsDto AiInsights { get; set; } = new();

        /// <summary>Top and bottom life-aspect pillars (presentation: strengths &amp; weaknesses).</summary>
        public StrengthsWeaknessesDto StrengthsWeaknesses { get; set; } = new();

        /// <summary>Past illness summary and latest avoidance tip for parent awareness.</summary>
        public IllnessTrackingDto IllnessTracking { get; set; } = new();
    }

    /// <summary>Explicit strengths and weaknesses from Final Hybrid pillar scores.</summary>
    public class StrengthsWeaknessesDto
    {
        public List<PillarScoreItemDto> Strengths { get; set; } = new();
        public List<PillarScoreItemDto> Weaknesses { get; set; } = new();
    }

    public class PillarScoreItemDto
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string Emoji { get; set; } = "";
        public double Score { get; set; }
    }

    public class IllnessTrackingDto
    {
        public string? PastIllnessSummary { get; set; }
        public string? LatestIllnessType { get; set; }
        public string? LatestIllnessNote { get; set; }
        public DateTime? LatestIllnessDate { get; set; }
        public string? AvoidanceTip { get; set; }
    }

    /// <summary>Phase 12 — AI-generated outputs surfaced on the parent insights page.</summary>
    public class AiInsightsDto
    {
        public RecommendedBadgeDto? RecommendedBadge { get; set; }
        public LeafyRecommendationDto? LeafyRecommendation { get; set; }
        public PersonalityClusterResultDto? PersonalityCluster { get; set; }

        /// <summary>True if today's (or the latest) entry was flagged anomalous by the AI.</summary>
        public bool AnomalyDetected { get; set; }

        /// <summary>Latest ScoreVariance value written by the AI (0–100).</summary>
        public double? ScoreVariance { get; set; }

        /// <summary>Latest sentiment score from the AI's Emotion Safety module (–1.0 to +1.0).</summary>
        public double? SentimentScore { get; set; }

        /// <summary>
        /// True when the Emotion Safety Engine flagged the latest entry.
        /// BUG FIX (audit 2026-06-21): this column existed on DailyLogEntry and
        /// was written by the AI team, but nothing in the backend ever read it
        /// back out to a parent-facing screen. Distinct from AnomalyDetected
        /// (Trust Engine) above — do not conflate the two.
        /// </summary>
        public bool EmotionSafetyAlertFlag { get; set; }

        /// <summary>Free-text description of the emotional concern, when EmotionSafetyAlertFlag is true.</summary>
        public string? EmotionSafetyAlertText { get; set; }

        /// <summary>Latest NLP honesty anomaly score (0–100). High = suspicious text patterns.</summary>
        public int? NlpAnomalyScore { get; set; }

        /// <summary>Latest detected emotion label from NLP (e.g. "Happy", "Sad", "Anxious").</summary>
        public string? DetectedEmotion { get; set; }
    }

    /// <summary>A real, performance-based alert for the parent (driven by the child's pillar scores).</summary>
    public class PersonalizedAlertDto
    {
        public string Severity { get; set; } = string.Empty;   // Critical / Medium / Low
        public string Emoji { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;    // e.g. "Heba's Home Responsibility is critically low at 12.0%."
        public string Suggestion { get; set; } = string.Empty; // e.g. "Assign a simple daily chore with a reward."
    }

    /// <summary>A real, performance-based activity recommendation for the parent.</summary>
    public class ActivityRecommendationDto
    {
        public string Icon { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    public class HeroHeaderDto
    {
        public string FullName { get; set; } = string.Empty;
        public int Age { get; set; }
        public string CharacterType { get; set; } = "Hero";
        public string SummaryType { get; set; } = "Weekly Summary";
    }

    public class PillarsDto
    {
        // ── Structured Pillars (computed from journal inputs) ─────────
        public double Spiritual { get; set; }
        public double Social { get; set; }
        public double Academic { get; set; }
        public double Env { get; set; }
        public double Nutrition { get; set; }
        public double Sport { get; set; }
        public double Hobbies { get; set; }
        public double Mental { get; set; }

        // ── NLP Pillars (AI-derived from free text) ───────────────────
        public double NlpSpiritual { get; set; }
        public double NlpSocial { get; set; }
        public double NlpAcademic { get; set; }
        public double NlpEnv { get; set; }
        public double NlpPhysicalHealth { get; set; }
        public double NlpPhysicalActivity { get; set; }
        public double NlpCreativity { get; set; }
        public double NlpMental { get; set; }

        // ── Final Hybrid Pillars (70% Structured + 30% NLP) ──────────
        public double FinalSpiritual { get; set; }
        public double FinalSocial { get; set; }
        public double FinalAcademic { get; set; }
        public double FinalEnv { get; set; }
        public double FinalPhysicalHealth { get; set; }
        public double FinalPhysicalActivity { get; set; }
        public double FinalCreativity { get; set; }
        public double FinalMental { get; set; }
        public double FinalMeanScore { get; set; }
    }

    public class TraitsDto
    {
        public double Curiosity { get; set; }
        public double Resilience { get; set; }
        public double Kindness { get; set; }
        public double Spiritual { get; set; }
        public double Vitality { get; set; }
    }

    public class DailyLogSummaryDto
    {
        public DateTime Date { get; set; }
        public List<string> Alarms { get; set; } = new();
        public List<string> Activities { get; set; } = new();
        public ChildMessageDto? Message { get; set; }
    }

    public class CategoryStats
    {
        public int Sports { get; set; }
        public int Study { get; set; }
        public int Chores { get; set; }
    }

    public class ActivityHourDto
    {
        public string Day { get; set; } = string.Empty;
        public double Hours { get; set; }
        public int LogDuration { get; set; }   // raw minutes for bar chart display
        public DateTime Date { get; set; }
        public bool IsToday { get; set; }
    }

    // ==================== 13. ADMIN ====================

    public class AdminStatsDto
    {
        public int TotalUsers { get; set; }
        public int TotalJournals { get; set; }
        // FIX P1: DailyJournals مطلوب من Frontend في dashboard-stats
        public int DailyJournals { get; set; }
        public double AvgTrustScore { get; set; }
        public List<AdminDailyStats> DailyStats { get; set; } = new();
    }

    public class AdminDailyStats
    {
        public DateTime Date { get; set; }
        public int JournalsCompleted { get; set; }
        public int NewUsers { get; set; }
        public double AvgTrustScore { get; set; }
    }

    public class LogFormItemDto
    {
        public int Id { get; set; }
        public string Category { get; set; } = "";
        public string Value { get; set; } = "";
        public string Type { get; set; } = "constant";
    }

    public class CharacterDto
    {
        public int Id { get; set; }
        public string Avatar { get; set; } = "";
        public string Name { get; set; } = "";
        [StringLength(200)] public string Description { get; set; } = "";
        [StringLength(100)] public string Personality { get; set; } = "";
    }

    // ==================== 14. HISTORY / ACTIVITY ====================

    public class ChildHistoryDto
    {
        public string ChildName { get; set; } = string.Empty;
        public string CharacterType { get; set; } = "Explorer";
        public List<DayActivityDto> History { get; set; } = new();
    }

    public class ActivityHistoryDto
    {
        public string? ChildName { get; set; }
        public string? CharacterType { get; set; }
        public List<LegacyDayActivityDto> Days { get; set; } = new();
    }

    public class DayActivityDto
    {
        public string Date { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public List<string> Alarms { get; set; } = new();
        public List<string> Activities { get; set; } = new();
        public string? MessageToParent { get; set; }
        public bool WasIllToday { get; set; }
        public string? IllnessType { get; set; }
        public string? IllnessNote { get; set; }
        public string? IllnessAvoidanceTip { get; set; }
    }

    public class LegacyDayActivityDto
    {
        public DateTime Date { get; set; }
        public List<string> Alarms { get; set; } = new();
        public List<string> Activities { get; set; } = new();
        public string? ParentMessage { get; set; }
    }


    // ==================== 15. MISC ====================

    public class SubjectDto
    {
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "📚";
        public string Category { get; set; } = "Academic";
    }

    public class EditHeroProfileDto
    {
        public string? Name { get; set; }
        public int? Age { get; set; }
    }

    public class UpdateHeroCategoriesDto
    {
        public List<string> Chores { get; set; } = new();
        public List<string> StudySubjects { get; set; } = new();
        public List<string> Sports { get; set; } = new();
    }

    public class AddHobbyDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? Category { get; set; }
    }

    public class HobbyDto
    {
        public string Name { get; set; } = "";
        public string? Icon { get; set; }
        public string Category { get; set; } = "General";
    }

    public class UpdateUserDto
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public double? TrustScore { get; set; }
    }

    public class ForgotPasswordDto
    {
        public string Email { get; set; } = "";
    }

    public class AdminLoginDto
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class InitiateChildPasswordDto
    {
        public int ChildId { get; set; }
        public string ParentPassword { get; set; } = "";
        public string NewInitialPassword { get; set; } = "";
    }

    public class CompleteResetDto
    {
        [Required(ErrorMessage = "Hero name is required")]
        public string UserName { get; set; } = "";

        [Required(ErrorMessage = "Secret code is required")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Code must be exactly 6 digits")]
        public string Code { get; set; } = "";

        [Required(ErrorMessage = "New password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string NewPassword { get; set; } = "";
    }

    public class ResetRequestDto
    {
        public int ChildId { get; set; }
        public string ParentPassword { get; set; } = "";
    }

    public class ChildLoginRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class FinalResetDto
    {
        public string Code { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }

    public class ResetPasswordDto
    {
        [Required] public string Email { get; set; } = "";
        [Required] public string Token { get; set; } = "";

        [Required]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        public string NewPassword { get; set; } = "";

        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = "";
    }

    public class UpdateChildProfileDto
    {
        public string? FullName { get; set; }
        public int? Age { get; set; }
        public string? CharacterName { get; set; }

        /// <summary>
        /// The emoji avatar chosen by the child (e.g. "🦁", "🦋", "🌸", …).
        /// Stored in ChildProfile.CharacterType in the database.
        /// </summary>
        public string? CharacterType { get; set; }
    }

    public class UpdateParentProfileDto
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
    }

    public class ResetPasswordModel
    {
        public int UserId { get; set; }
        public string NewPassword { get; set; } = "";
    }

    // ── Journal DTOs ─────────────────────────────────────────────────────

    /// <summary>Payload sent from journal.html to POST /api/Children/journal</summary>
    public class SaveJournalDto
    {
        public List<string>? SpiritualActivities { get; set; }
        public string? PhysicalActivity { get; set; }
        public int WaterGlasses { get; set; }
        public string? SocialTimeWith { get; set; }
        public List<string>? CompletedSubjects { get; set; }
        public string? MoodLevel { get; set; }
        public string? MoodEmoji { get; set; }
        public List<string>? NutritionItems { get; set; }
        public bool HelpedFamily { get; set; }
        public List<string>? CompletedChores { get; set; }
        public List<string>? FunActivities { get; set; }
        public string? ReflectionText { get; set; }
        public string? MoodReason { get; set; }

        // ── Phase 1 & Spec Updates: New journal data fields ─────────────────────────

        /// <summary>Total active minutes logged by the child for this entry.</summary>
        public int LogDuration { get; set; }

        /// <summary>% of LogDuration spent on studying (0–100).</summary>
        [Range(0, 100)]
        public double StudyingPct { get; set; }

        /// <summary>% of LogDuration spent on spiritual/religious activities (0–100).</summary>
        [Range(0, 100)]
        public double SpiritualPct { get; set; }

        /// <summary>% of LogDuration spent on physical movement/sport (0–100).</summary>
        [Range(0, 100)]
        public double MovingPct { get; set; }

        /// <summary>% of LogDuration spent on food/nutrition care (0–100).</summary>
        [Range(0, 100)]
        public double FoodPct { get; set; }

        /// <summary>% of LogDuration spent on home chores / family responsibilities (0–100).</summary>
        [Range(0, 100)]
        public double ChorsPct { get; set; } // FIX: Renamed ChoresPct to ChorsPct for spec alignment

        /// <summary>% of LogDuration spent on hobbies / creative activities (0–100).</summary>
        [Range(0, 100)]
        public double HobbiesPct { get; set; }

        /// <summary>% of LogDuration spent on social interaction (0–100).</summary>
        [Range(0, 100)]
        public double SocialPct { get; set; }

        /// <summary>% of LogDuration spent on emotional/mental state regulation (0–100).</summary>
        [Range(0, 100)]
        public double MoodPct { get; set; } // FIX: Added missing MoodPct field for mental health tracking

        /// <summary>Child's own description of how they feel today (NLP source text).</summary>
        public string? FeelingNote { get; set; }

        /// <summary>Private message from the child to their parent.</summary>
        public string? MessageToParent { get; set; }

        /// <summary>
        /// How the child handled challenges today (e.g. "Stayed calm, Asked for help").
        /// Feeds MentalWellbeing_Pct and contributes to Resilience Trait.
        /// </summary>
        public List<string>? ChallengeHandling { get; set; }

        /// <summary>
        /// How positive/enjoyable the child's social interaction was today (0-100).
        /// Auto-calculated if not provided, based on SocialTimeWith and ChallengeHandling.
        /// </summary>
        [Range(0, 100)]
        public double SocialQualityScore { get; set; }

        // ── Phase 10 Refactor: Journal/Sleep alignment ──────────────────────
        // DailyLogEntry.SleepHours / SleepNote already exist as DB columns
        // (no schema change). These DTO fields are NEW and OPTIONAL so older
        // frontend builds that don't send them remain fully compatible
        // (nullable -> defaults to "not provided" -> JournalService leaves
        // the existing stored value / 0 untouched).

        /// <summary>Hours of sleep the child reported for last night (0–24). Optional.</summary>
        [Range(0, 24)]
        public int? SleepHours { get; set; }

        /// <summary>Optional free-text note about sleep quality.</summary>
        public string? SleepNote { get; set; }

        /// <summary>Whether the child felt sick today.</summary>
        public bool WasIllToday { get; set; }

        /// <summary>Illness category (e.g. cold, fever, allergy).</summary>
        [MaxLength(100)]
        public string? IllnessType { get; set; }

        /// <summary>Free-text note about today's illness.</summary>
        [MaxLength(500)]
        public string? IllnessNote { get; set; }
    }

    /// <summary>Response returned after saving a journal entry</summary>
    public class JournalSaveResultDto
    {
        public string Message { get; set; } = "";
        public int ExpAwarded { get; set; }
        public int TotalExp { get; set; }
        public int Level { get; set; }
        public string LevelName { get; set; } = "";                       // PHASE 10
        public int ExpToNextLevel { get; set; }
        public bool LeveledUp { get; set; }
        public bool IsFirstSave { get; set; }
        public List<NewBadgeDto> NewlyUnlockedBadges { get; set; } = new(); // PHASE 10

        /// <summary>
        /// AI team spec: "hashed_child_id" — privacy-safe identifier returned
        /// in journal save response so the AI team can correlate this journal
        /// entry with child_challenge_progress and user_levels records.
        /// </summary>
        public string HashedChildId { get; set; } = "";                   // PHASE 10.1 SPEC

        public AspectProgressDto Aspects { get; set; } = new();
    }

    /// <summary>PHASE 10: details of a badge unlocked via the new challenge engine.</summary>
    public class NewBadgeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string IconUrl { get; set; } = "";
    }

    /// <summary>
    /// PHASE 10: a single badge plus its unlock status and (for locked badges)
    /// the child's live progress on every challenge required to unlock it.
    /// Used by GET /api/Children/all-badges so the frontend can render
    /// earned badges as colored orbs and locked badges as grey orbs with
    /// progress bars.
    /// </summary>
    public class BadgeWithChallengesDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string IconUrl { get; set; } = "";
        public bool IsUnlocked { get; set; }
        public DateTime? UnlockedAt { get; set; }

        // ── AI Team Spec: Pillar Percentage Columns ──────────────────────
        // acad_pct, soc_pct, ment_pct, phys_pct, spir_pct, env_pct, crea_pct, res_pct
        // Non-zero only for pillar badges (IDs 28-51).
        public decimal AcadPct { get; set; } = 0;
        public decimal SocPct { get; set; } = 0;
        public decimal MentPct { get; set; } = 0;
        public decimal PhysPct { get; set; } = 0;
        public decimal SpirPct { get; set; } = 0;
        public decimal EnvPct { get; set; } = 0;
        public decimal CreaPct { get; set; } = 0;
        public decimal ResPct { get; set; } = 0;

        public List<BadgeChallengeProgressDto> Challenges { get; set; } = new();
    }

    /// <summary>PHASE 10: a child's live progress on a single badge challenge.</summary>
    public class BadgeChallengeProgressDto
    {
        public int ChallengeId { get; set; }
        public string Emoji { get; set; } = "⭐";
        public string Description { get; set; } = "";
        public string MetricType { get; set; } = "";
        public string? MetricSubType { get; set; }
        public double CurrentValue { get; set; }
        public double TargetValue { get; set; }
        public bool IsCompleted { get; set; }
    }

    /// <summary>Current snapshot of all 8 pillars + 5 traits</summary>
    public class AspectProgressDto
    {
        // ── 8 Pillars ──────────────────────────────────────────────────
        public double Academic { get; set; }   // Acad_Pct
        public double Religious { get; set; }   // Spir_Pct
        public double Physical { get; set; }   // Phys_Pct (Nutrition)
        public double Sport { get; set; }   // Res_Pct  (Movement)
        public double Social { get; set; }   // Soc_Pct
        public double Env { get; set; }   // Env_Pct  (Family Chores)
        public double Crea { get; set; }   // Crea_Pct (Hobbies)
        public double Mental { get; set; }   // Ment_Pct (Mood)
        public double Emotional { get; set; }   // legacy alias for Mental
        public double Threshold { get; set; }   // % needed to unlock level-up
        // ── 5 Core Traits (weekly synthesis) ──────────────────────────
        public double KindnessTrait { get; set; }
        public double VitalityTrait { get; set; }
        public double CuriosityTrait { get; set; }
        public double ResilienceTrait { get; set; }
        public double SpiritualTrait { get; set; }
        // ── legacy field (kept for backward compat) ───────────────────
        public double Resilience { get; set; }
    }

    /// <summary>Single journal history item returned by GET /api/Children/journal-history-v2</summary>
    public class JournalHistoryItemDto
    {
        public int Id { get; set; }
        public string EntryDate { get; set; } = "";
        public string? MoodLevel { get; set; }
        public string? MoodEmoji { get; set; }
        public string? Reflection { get; set; }
        public int ExpAwarded { get; set; }
        public bool TriggeredLevelUp { get; set; }
        public string? MoodReason { get; set; }
    }
}
// ════════════════════════════════════════════════════════════════
//  AI Team DTOs  (Phase 9)
//  Used by POST /api/ai/log-result
// ════════════════════════════════════════════════════════════════

/// <summary>
/// Payload sent by the external AI team to store analysis results
/// for a single DailyLogEntry.
/// </summary>
/// <summary>
/// AI team writes challenge-completion decisions for "journal_text" type
/// challenges (the 13-badge spec library) — the backend has no numeric
/// metric for free-text journal challenges, so the AI's NLP pipeline reads
/// the journal entry text and decides completion here.
/// </summary>
public class AiChallengeProgressDto
{
    public int ChallengeId { get; set; }

    /// <summary>True if the AI determined this challenge's text requirement was met.</summary>
    public bool IsCompleted { get; set; }
}

public class AiChallengeProgressBatchDto
{
    public int ChildId { get; set; }
    public List<AiChallengeProgressDto> Challenges { get; set; } = new();
}

public class AiLogResultDto
{
    /// <summary>The DailyLogEntry.Id to update.</summary>
    public int LogId { get; set; }

    /// <summary>Sentiment score: –1.0 (very negative) → +1.0 (very positive).</summary>
    public double SentimentScore { get; set; }

    /// <summary>Primary detected emotion (e.g. "Happy", "Sad", "Anxious").</summary>
    public string DetectedEmotion { get; set; } = string.Empty;

    /// <summary>Consistency/honesty anomaly score: 0 (normal) – 100 (anomalous).</summary>
    public int AnomalyScore { get; set; }

    /// <summary>NLP-level anomaly score: 0 (normal) – 100 (anomalous).</summary>
    public int NlpAnomalyScore { get; set; }

    /// <summary>True when the AI determined an anomaly exists in this entry.
    /// TRUST ENGINE ONLY — do NOT use for Emotion Safety alerts (use EmotionSafetyAlertFlag).</summary>
    public bool AnomalyFlag { get; set; }

    // ── PHASE 12 ADDITIONS (all optional / nullable for backward compatibility
    //    with the AI team's existing Phase 9 payload shape — omit any of these
    //    and the corresponding stored value is left untouched) ──────────────

    /// <summary>Dispersion/consistency variance for this entry. 0–100. Optional.</summary>
    public double? ScoreVariance { get; set; }

    /// <summary>NLP Pillar 1 — Spiritual Connection (0–100), derived from text. Optional.</summary>
    public double? NlpSpiritualConnection_Pct { get; set; }

    /// <summary>NLP Pillar 2 — Social Interaction (0–100), derived from text. Optional.</summary>
    public double? NlpSocialInteraction_Pct { get; set; }

    /// <summary>NLP Pillar 3 — Academic Progress (0–100), derived from text. Optional.</summary>
    public double? NlpAcademicProgress_Pct { get; set; }

    /// <summary>NLP Pillar 4 — Home Responsibility (0–100), derived from text. Optional.</summary>
    public double? NlpHomeResponsibility_Pct { get; set; }

    /// <summary>NLP Pillar 5 — Physical Health (0–100), derived from text. Optional.</summary>
    public double? NlpPhysicalHealth_Pct { get; set; }

    /// <summary>NLP Pillar 6 — Physical Activity (0–100), derived from text. Optional.</summary>
    public double? NlpPhysicalActivity_Pct { get; set; }

    /// <summary>NLP Pillar 7 — Creativity Engagement (0–100), derived from text. Optional.</summary>
    public double? NlpCreativityEngagement_Pct { get; set; }

    /// <summary>NLP Pillar 8 — Mental Wellbeing (0–100), derived from text. Optional.</summary>
    public double? NlpMentalWellbeing_Pct { get; set; }

    /// <summary>Leafy RAG avoidance suggestion when child reported illness. Optional.</summary>
    public string? IllnessAvoidanceTip { get; set; }

    /// <summary>
    /// Emotion Safety Engine output (AI team spec column name: Alert on daily_log_entries).
    /// Free-text safety alert for this entry, or null/empty when no concern detected.
    /// Written via POST /api/ai/log-result OR direct SQL — PillarPipelineService derives
    /// EmotionSafetyAlertFlag/Text from this column automatically.
    /// </summary>
    public string? Alert { get; set; }

    // ── FIX (FEELINGNOTE_FIX): EMOTION SAFETY ENGINE — أعمدة منفصلة تماماً عن Trust Engine ──
    //
    //  ⚠️  INTEGRATION NOTE FOR AI TEAM (توثيق للفريق):
    //
    //  الـ LeafyTip table عندها أعمدة Tip/Alert/Motivation الإضافية — لكن
    //  الـ AI team يجب يستخدم حصريًا:
    //    • ChildProfile.LeafyTipText     → النص المباشر للـ tip
    //    • ChildProfile.LeafyAlertText   → نص الـ alert
    //    • ChildProfile.LeafyMessageText → نص الـ motivation/message
    //  عبر POST /api/ai/leafy-recommendation.
    //  الأعمدة LeafyTip.Tip / .Alert / .Motivation في catalog table موجودة
    //  كـ reference content للـ RAG فقط — النتيجة النهائية تُكتب على ChildProfile.
    //
    //  AnomalyFlag / AnomalyScore → Trust Engine حصريًا (Anomaly Detection).
    //  EmotionSafetyAlertFlag / EmotionSafetyAlertText → Emotion Safety Engine حصريًا.
    //  لا تخلط بينهم.

    /// <summary>
    /// Emotion Safety Engine: true when the engine detected an emotional concern.
    /// DISTINCT from AnomalyFlag (Trust Engine). Optional — omit to leave stored value untouched.
    /// </summary>
    public bool? EmotionSafetyAlertFlag { get; set; }

    /// <summary>
    /// Emotion Safety Engine: free-text description of the detected emotional concern.
    /// Shown to parents. Null/omit when EmotionSafetyAlertFlag = false.
    /// DISTINCT from AnomalyFlag/AnomalyScore (Trust Engine). Optional.
    /// </summary>
    public string? EmotionSafetyAlertText { get; set; }
}

/// <summary>
/// Batch payload — allows the AI team to submit results for multiple
/// log entries in a single request (more efficient than one call per entry).
/// </summary>
public class AiLogResultBatchDto
{
    public List<AiLogResultDto> Results { get; set; } = new();
}

// ── AI Badge Suggestion DTOs ──────────────────────────────────────────────

/// <summary>
/// Single badge suggestion pushed by the AI/NLP team.
/// </summary>
public class AiBadgeSuggestionDto
{
    /// <summary>Child's user ID (from JWT claims or direct lookup).</summary>
    public int ChildId { get; set; }

    /// <summary>Pillar key: spiritual | social | academic | home |
    /// physicalHealth | physicalActivity | creativity | mental</summary>
    public string Pillar { get; set; } = string.Empty;

    /// <summary>Badge display name. e.g. "Spiritual Silver"</summary>
    public string BadgeName { get; set; } = string.Empty;

    /// <summary>Emoji icon. e.g. "🕋"</summary>
    public string BadgeIcon { get; set; } = "🏅";

    /// <summary>Motivational message shown to the child.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Internal reason for the suggestion (parent dashboard only).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>low | medium | high</summary>
    public string Urgency { get; set; } = "medium";

    /// <summary>Child's current pillar % at time of analysis.</summary>
    public double CurrentPct { get; set; }

    /// <summary>Target pillar % needed to earn the badge.</summary>
    public double TargetPct { get; set; }

    /// <summary>Days until suggestion expires (default: 7).</summary>
    public int ExpiresInDays { get; set; } = 7;
}

/// <summary>
/// Batch payload — AI team can push multiple suggestions at once.
/// </summary>
public class AiBadgeSuggestionBatchDto
{
    public List<AiBadgeSuggestionDto> Suggestions { get; set; } = new();
}

// ════════════════════════════════════════════════════════════════
//  AI Team DTOs  (Phase 12)
//  Personality Clustering · Badge Recommender · Leafy Recommendation
// ════════════════════════════════════════════════════════════════

// ── Personality Clustering ────────────────────────────────────────────────

/// <summary>
/// Single child→cluster assignment pushed by the AI team's periodic
/// clustering job. POST /api/ai/personality-clusters.
/// The cluster definition fields (ClusterName/Description/...) are
/// upserted into PersonalityCluster keyed by ClusterKey; the assignment
/// itself is appended to AdminChildrenPersonalityClusters and the child's
/// previous current assignment is flipped to IsCurrent = false.
/// </summary>
public class AiPersonalityClusterAssignmentDto
{
    /// <summary>Child's user ID.</summary>
    public int ChildId { get; set; }

    /// <summary>Stable cluster key, e.g. "cluster_steady_achiever". Required — used to upsert the cluster definition.</summary>
    public string ClusterKey { get; set; } = string.Empty;

    /// <summary>Human-friendly cluster name (only used the FIRST time a ClusterKey is seen — admin may edit it afterward).</summary>
    public string ClusterName { get; set; } = string.Empty;

    /// <summary>AI-generated narrative description of the cluster (overwritten on every run by the AI's latest description).</summary>
    public string? Description { get; set; }

    /// <summary>Which traits/pillars dominate this cluster.</summary>
    public string? DominantTraits { get; set; }

    /// <summary>Opaque JSON feature vector / centroid stats, stored as-is.</summary>
    public string? Characteristics { get; set; }

    /// <summary>AI guidance on what this cluster's children should focus on.</summary>
    public string? RecommendedFocus { get; set; }

    /// <summary>AI confidence this child belongs to this cluster. 0–100.</summary>
    public double SimilarityScore { get; set; }

    /// <summary>Identifier shared by every assignment produced in this same clustering run.</summary>
    public string? ClusteringRunId { get; set; }
}

/// <summary>Batch payload — the clustering job typically re-clusters every active child in one run.</summary>
public class AiPersonalityClusterBatchDto
{
    public List<AiPersonalityClusterAssignmentDto> Assignments { get; set; } = new();
}

/// <summary>Returned to the AI team (and Admin/Parent APIs) describing a child's current cluster.</summary>
public class PersonalityClusterResultDto
{
    public int ChildId { get; set; }
    public int PersonalityClusterId { get; set; }
    public string ClusterKey { get; set; } = string.Empty;
    public string ClusterName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DominantTraits { get; set; }
    public string? RecommendedFocus { get; set; }
    public double SimilarityScore { get; set; }
    public DateTime AssignedAt { get; set; }
}

/// <summary>Admin dashboard list view — one card per cluster definition.</summary>
public class PersonalityClusterAdminDto
{
    public int Id { get; set; }
    public string ClusterKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DominantTraits { get; set; }
    public string? Characteristics { get; set; }
    public string? RecommendedFocus { get; set; }
    public int MemberCount { get; set; }
    public DateTime? LastComputedAt { get; set; }
}

/// <summary>Admin payload to relabel/re-describe a cluster definition for human consumption (does not touch AI-owned Characteristics).</summary>
public class UpsertPersonalityClusterDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>Admin drill-down — one row per child currently in a given cluster.</summary>
public class ClusterMemberDto
{
    public int ChildId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
    public double SimilarityScore { get; set; }
    public DateTime AssignedAt { get; set; }
}

// ── Badge Recommender ─────────────────────────────────────────────────────

/// <summary>
/// Weekly Badge Recommender single best-pick output. POST /api/ai/badge-recommendation.
/// Sets ChildProfile.RecommendedBadgeId directly — distinct from the existing
/// per-pillar AiBadgeSuggestion list (POST /api/ai/badge-suggestions), which is untouched.
/// </summary>
public class AiBadgeRecommendationDto
{
    public int ChildId { get; set; }

    /// <summary>The Badge.Id the AI recommends as this child's single best next badge.</summary>
    public int RecommendedBadgeId { get; set; }
}

/// <summary>Batch payload — the weekly job typically recommends for every active child in one run.</summary>
public class AiBadgeRecommendationBatchDto
{
    public List<AiBadgeRecommendationDto> Recommendations { get; set; } = new();
}

/// <summary>Resolved recommended-badge details returned to child/parent-facing APIs.</summary>
public class RecommendedBadgeDto
{
    public int? BadgeId { get; set; }
    public string? BadgeName { get; set; }
    public string? BadgeIcon { get; set; }
    public string? Description { get; set; }
    public bool AlreadyUnlocked { get; set; }
}

// ── Leafy Recommendation ──────────────────────────────────────────────────

/// <summary>
/// Daily Leafy Recommendation output. POST /api/ai/leafy-recommendation.
/// Sets ChildProfile.RecommendedLeafyTipId — the AI's pointer into the
/// existing LeafyTip catalog table (admin-managed content). The backend
/// never generates or rewrites LeafyTip content here; it only stores which
/// existing tip the AI selected for this child today.
/// </summary>
public class AiLeafyRecommendationDto
{
    public int ChildId { get; set; }

    /// <summary>
    /// The LeafyTip.Id the AI selected as today's best-fit catalog
    /// tip/message/alert for this child. Optional — omit (or send null)
    /// when the AI's RAG step composed free text instead of selecting an
    /// existing catalog row (see TipText/MessageText/AlertText below).
    /// </summary>
    public int? RecommendedLeafyTipId { get; set; }

    /// <summary>AI-generated free-text child-facing tip (Leafy RAG output). Optional — omit to leave the previously stored value untouched.</summary>
    public string? TipText { get; set; }

    /// <summary>AI-generated free-text child-facing message (Leafy RAG output). Optional — omit to leave the previously stored value untouched.</summary>
    public string? MessageText { get; set; }

    /// <summary>AI-generated free-text parent-facing alert (Leafy RAG output). Optional — omit to leave the previously stored value untouched.</summary>
    public string? AlertText { get; set; }
}

/// <summary>Batch payload — the daily job typically recommends for every active child in one run.</summary>
public class AiLeafyRecommendationBatchDto
{
    public List<AiLeafyRecommendationDto> Recommendations { get; set; } = new();
}

/// <summary>Resolved Leafy recommendation returned to child-facing APIs (leafy.html).</summary>
public class LeafyRecommendationDto
{
    public int? TipId { get; set; }
    public string? Tip { get; set; }
    public string? Alert { get; set; }
    public string? Motivation { get; set; }
    public string? Side { get; set; }
    public string Source { get; set; } = "none"; // "ai" | "fallback" | "none"
}

// ════════════════════════════════════════════════════════════════════
//  PHASE 13 — ADMIN CHILDREN OVERVIEW (Final Pillars + Cluster)
// ════════════════════════════════════════════════════════════════════

/// <summary>
/// Single child row returned by GET /api/Admin/children.
/// Contains all 24 pillar values (8 Structured + 8 NLP + 8 Final),
/// current level, badge count, and personality cluster.
/// </summary>
public class AdminChildOverviewDto
{
    public int ChildId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
    public int Level { get; set; }
    public int BadgeCount { get; set; }
    public int TrustScore { get; set; }
    public double MeanScore { get; set; }
    public double FinalMeanScore { get; set; }

    // ── 8 Structured Pillars ────────────────────────────────────────
    public double SpiritualPct { get; set; }
    public double SocialPct { get; set; }
    public double AcademicPct { get; set; }
    public double EnvPct { get; set; }
    public double PhysicalPct { get; set; }
    public double SportPct { get; set; }
    public double CreaPct { get; set; }
    public double MentalPct { get; set; }

    // ── 8 NLP Pillars ───────────────────────────────────────────────
    public double NlpSpiritualPct { get; set; }
    public double NlpSocialPct { get; set; }
    public double NlpAcademicPct { get; set; }
    public double NlpEnvPct { get; set; }
    public double NlpPhysicalPct { get; set; }
    public double NlpSportPct { get; set; }
    public double NlpCreaPct { get; set; }
    public double NlpMentalPct { get; set; }

    // ── 8 Final Hybrid Pillars (70% Structured + 30% NLP) ───────────
    public double FinalSpiritualPct { get; set; }
    public double FinalSocialPct { get; set; }
    public double FinalAcademicPct { get; set; }
    public double FinalEnvPct { get; set; }
    public double FinalPhysicalPct { get; set; }
    public double FinalSportPct { get; set; }
    public double FinalCreaPct { get; set; }
    public double FinalMentalPct { get; set; }

    // ── Personality Cluster ─────────────────────────────────────────
    public string? PersonalityClusterName { get; set; }
    public string? PersonalityClusterKey { get; set; }
    public string? PersonalityClusterDominantTraits { get; set; }
    public DateTime? PersonalityClusterAssignedAt { get; set; }

    // ── AI Recommendation Pointers ──────────────────────────────────
    public int? RecommendedBadgeId { get; set; }
    public int? RecommendedLeafyTipId { get; set; }
    public string? LeafyAlertText { get; set; }
    public DateTime? LeafyTextSetAt { get; set; }
    public DateTime? LastAggregatedAt { get; set; }
}
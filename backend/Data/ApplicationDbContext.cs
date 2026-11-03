// ============================================================
//  ApplicationDbContext.cs — Grawiaa / Leafy AI System
//
//  ╔══════════════════════════════════════════════════════════╗
//  ║  PHASE 5 — FINAL CONFIGURATION (COMPLETE REPLACEMENT)   ║
//  ╚══════════════════════════════════════════════════════════╝
//
//  CHANGES vs. Phase 4:
//  ─────────────────────────────────────────────────────────
//  [A1] DailyLogEntries unique constraint REMOVED.
//       Index on (ChildId, LogDate) is now NON-UNIQUE so a child
//       can submit multiple journal entries per day (multi-save).
//       The composite index is kept for query performance only.
//
//  [A2] ChildProfile — WeeklyXxxPct columns explicitly configured
//       as float precision so SQL Server does not map them as
//       decimal(18,2). All 8 raw pillar averages + the 5 core
//       trait scores are covered.
//
//  [A3] LeafyTip — three dedicated content columns (Tip, Alert,
//       Motivation) configured with MaxLength(500). The legacy
//       Content column is kept for backward compatibility.
//       A composite filtered index (Purpose, Side) accelerates
//       the tip/alert/motivation lookup queries.
//
//  [A4] AdminController must call UpsertLeafyTipDto (Phase5Dtos)
//       instead of the old LeafyTipDto — see AdminController.cs.
//
//  HOW TO APPLY:
//  ─────────────────────────────────────────────────────────
//  Replace your existing Data/ApplicationDbContext.cs with this
//  file, then run:
//
//    dotnet ef migrations add Phase5_FinalConfig
//    dotnet ef database update
//
//  The migration will:
//    • Drop the old UNIQUE INDEX on DailyLogEntries(ChildId, LogDate)
//    • Re-create it as a non-unique index
//    • Add nvarchar(500) columns Tip, Alert, Motivation to LeafyTips
//      (nullable — existing rows get NULL; Content is still present)
//    • Ensure ChildProfile float columns are correct
// ============================================================

using GrawiaaApp.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GrawiaaApp.API.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        // ── DbSets ────────────────────────────────────────────────────
        public DbSet<ChildProfile> ChildProfiles { get; set; }
        public DbSet<ParentProfile> ParentProfiles { get; set; }
        public DbSet<AdminProfile> AdminProfiles { get; set; }
        public DbSet<DailyLogEntry> DailyLogEntries { get; set; }
        public DbSet<ParentFeedback> ParentFeedbacks { get; set; }
        public DbSet<ChildTask> ChildTasks { get; set; }
        public DbSet<TrustScoreHistory> TrustScoreHistories { get; set; }
        public DbSet<Hobby> Hobbies { get; set; }
        public DbSet<ChildHobby> ChildHobbies { get; set; }
        public DbSet<Badge> Badges { get; set; }
        public DbSet<Reward> Rewards { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Subject> Subjects { get; set; } = null!;
        public DbSet<LogFormItem> LogFormItems { get; set; }
        public DbSet<LeafyTip> LeafyTips { get; set; }
        public DbSet<Character> Characters { get; set; }
        public DbSet<ChildBadge> ChildBadges { get; set; }
        public DbSet<AiBadgeSuggestion> AiBadgeSuggestions { get; set; }

        // ── PHASE 10: New Badge + Level System ──────────────────────────
        public DbSet<BadgeChallenge> BadgeChallenges { get; set; }
        public DbSet<ChildChallengeProgress> ChildChallengeProgresses { get; set; }
        public DbSet<Level> Levels { get; set; }
        public DbSet<UserLevel> UserLevels { get; set; }

        // ── PHASE 12: AI Personality Clustering ──────────────────────────
        public DbSet<PersonalityCluster> PersonalityClusters { get; set; }
        public DbSet<AdminChildrenPersonalityCluster> AdminChildrenPersonalityClusters { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ── 1. Strip unused IdentityUser columns ──────────────────
            builder.Entity<ApplicationUser>(b =>
            {
                b.Ignore(u => u.PhoneNumberConfirmed);
                b.Ignore(u => u.TwoFactorEnabled);
                b.Ignore(u => u.LockoutEnd);
                b.Ignore(u => u.LockoutEnabled);
                b.Ignore(u => u.AccessFailedCount);
            });

            // ── 2. User ↔ Profile (1:1) ───────────────────────────────
            builder.Entity<ApplicationUser>()
                .HasOne(u => u.ChildProfile)
                .WithOne(c => c.User)
                .HasForeignKey<ChildProfile>(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ApplicationUser>()
                .HasOne(u => u.ParentProfile)
                .WithOne(p => p.User)
                .HasForeignKey<ParentProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ApplicationUser>()
                .HasOne(u => u.AdminProfile)
                .WithOne(a => a.User)
                .HasForeignKey<AdminProfile>(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ── 3. Self-referencing Parent → Children ─────────────────
            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Parent)
                .WithMany(u => u.Children)
                .HasForeignKey(u => u.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── 4. Child ↔ ParentProfile ──────────────────────────────
            builder.Entity<ChildProfile>()
                .HasOne(c => c.Parent)
                .WithMany(p => p.Children)
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── 5. (REMOVED) ChildMessage ────────────────────────────
            // ChildMessage table removed — child→parent messages now
            // live directly on DailyLogEntry.MessageToParent so the AI
            // team's direct-DB-read jobs find the value where they
            // expect it, with no separate table or join required.

            // ── 6. Many-to-Many Child ↔ Hobby ────────────────────────
            builder.Entity<ChildHobby>()
                .HasKey(ch => new { ch.ChildId, ch.HobbyId });

            builder.Entity<ChildHobby>()
                .HasOne(ch => ch.Child)
                .WithMany(u => u.ChildHobbies)
                .HasForeignKey(ch => ch.ChildId);

            builder.Entity<ChildHobby>()
                .HasOne(ch => ch.Hobby)
                .WithMany(h => h.ChildHobbies)
                .HasForeignKey(ch => ch.HobbyId);

            // ── 7. Other FK relationships ─────────────────────────────
            builder.Entity<ChildTask>()
                .HasOne(t => t.Child)
                .WithMany(u => u.Tasks)
                .HasForeignKey(t => t.ChildId);

            builder.Entity<DailyLogEntry>()
                .HasOne(l => l.Child)
                .WithMany(u => u.Logs)
                .HasForeignKey(l => l.ChildId);

            builder.Entity<ParentFeedback>()
                .HasOne(f => f.DailyLogEntry)
                .WithMany(l => l.Feedbacks)
                .HasForeignKey(f => f.DailyLogEntryId);

            builder.Entity<Notification>()
                .HasOne(n => n.Receiver)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.ReceiverId);

            // ── (REMOVED) DailyQuestionAnswer ───────────────────────────
            // Table removed — the daily "What do you want to tell Leafy
            // about today?" answer now lives directly on
            // DailyLogEntry.GeneralNote so the AI team's direct-DB-read
            // jobs find it on the same row as FeelingNote/MessageToParent,
            // with no separate table needed.

            // ── (REMOVED) ParentLinkRequest ─────────────────────────────
            // Table removed (dead code audit — zero references in any
            // Controller/Service, confirmed by grep across the whole
            // backend before deletion). The actual parent↔child linking
            // flow has always run through ChildProfile.PendingParentEmail
            // / PendingLinkToken (see AuthController, ParentController,
            // ChildController) — this table + its FK config were leftover
            // scaffolding from an earlier design that was never wired up.

            // ── 8. Indexes ────────────────────────────────────────────
            //
            // [A1] PHASE 5: DailyLogEntries index is NON-UNIQUE.
            //      The old Phase 4 configuration used .IsUnique() which
            //      crashed on the second journal save of the same day.
            //      IsUnique(false) keeps the composite index for performance
            //      while allowing unlimited entries per (ChildId, date).
            builder.Entity<DailyLogEntry>()
                .HasIndex(l => new { l.ChildId, l.LogDate })
                .IsUnique(false);                           // ← CHANGED from IsUnique()

            builder.Entity<ChildProfile>()
                .HasIndex(c => c.ParentId);

            // ── 9. Decimal / Float Precision ──────────────────────────
            builder.Entity<Reward>()
                .Property(r => r.BudgetLimit)
                .HasPrecision(18, 2);

            // [A2] ChildProfile — trait scores and pillar aggregates stored as SQL float.
            //      EF Core maps C# double to SQL float by default when the
            //      column has no HasColumnType override, so these calls
            //      make the intent explicit and prevent decimal mismatch.
            //      (Weekly raw pct averages removed — Phase 14 cleanup.)
            builder.Entity<ChildProfile>(cp =>
            {
                // Processed 8-pillar scores (engine-weighted, stored on profile)
                cp.Property(p => p.SpiritualConnection_Pct).HasColumnType("float");
                cp.Property(p => p.SocialInteraction_Pct).HasColumnType("float");
                cp.Property(p => p.AcademicProgress_Pct).HasColumnType("float");
                cp.Property(p => p.HomeResponsibility_Pct).HasColumnType("float");
                cp.Property(p => p.PhysicalHealth_Pct).HasColumnType("float");
                cp.Property(p => p.PhysicalActivity_Pct).HasColumnType("float");
                cp.Property(p => p.CreativityEngagement_Pct).HasColumnType("float");
                cp.Property(p => p.MentalWellbeing_Pct).HasColumnType("float");

                // 5 Core Personality Traits
                cp.Property(p => p.KindnessTrait).HasColumnType("float");
                cp.Property(p => p.VitalityTrait).HasColumnType("float");
                cp.Property(p => p.CuriosityTrait).HasColumnType("float");
                cp.Property(p => p.ResilienceTrait).HasColumnType("float");
                cp.Property(p => p.SpiritualTrait).HasColumnType("float");

                // AI signal aggregation field
                cp.Property(p => p.ResiliencePct).HasColumnType("float");

                // ── Phase 13: 30-day NLP pillar averages (AI team writes via AiController) ──
                cp.Property(p => p.NlpSpiritualConnection_Pct).HasColumnType("float");
                cp.Property(p => p.NlpSocialInteraction_Pct).HasColumnType("float");
                cp.Property(p => p.NlpAcademicProgress_Pct).HasColumnType("float");
                cp.Property(p => p.NlpHomeResponsibility_Pct).HasColumnType("float");
                cp.Property(p => p.NlpPhysicalHealth_Pct).HasColumnType("float");
                cp.Property(p => p.NlpPhysicalActivity_Pct).HasColumnType("float");
                cp.Property(p => p.NlpCreativityEngagement_Pct).HasColumnType("float");
                cp.Property(p => p.NlpMentalWellbeing_Pct).HasColumnType("float");

                // ── Phase 13: Final Hybrid Pillars (70% Structured + 30% NLP) ──
                cp.Property(p => p.FinalSpiritualConnection_Pct).HasColumnType("float");
                cp.Property(p => p.FinalSocialInteraction_Pct).HasColumnType("float");
                cp.Property(p => p.FinalAcademicProgress_Pct).HasColumnType("float");
                cp.Property(p => p.FinalHomeResponsibility_Pct).HasColumnType("float");
                cp.Property(p => p.FinalPhysicalHealth_Pct).HasColumnType("float");
                cp.Property(p => p.FinalPhysicalActivity_Pct).HasColumnType("float");
                cp.Property(p => p.FinalCreativityEngagement_Pct).HasColumnType("float");
                cp.Property(p => p.FinalMentalWellbeing_Pct).HasColumnType("float");
                cp.Property(p => p.FinalMeanScore).HasColumnType("float");
            });

            // ── 10. ChildBadge ────────────────────────────────────────
            builder.Entity<ChildBadge>()
                .HasOne(cb => cb.Child)
                .WithMany(u => u.ChildBadges)
                .HasForeignKey(cb => cb.ChildId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ChildBadge>()
                .HasOne(cb => cb.Badge)
                .WithMany(b => b.ChildBadges)
                .HasForeignKey(cb => cb.BadgeId)
                .OnDelete(DeleteBehavior.Cascade);

            // ── AiBadgeSuggestion ────────────────────────────────────────
            builder.Entity<AiBadgeSuggestion>()
                .HasOne(s => s.Child)
                .WithMany()
                .HasForeignKey(s => s.ChildId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AiBadgeSuggestion>()
                .HasIndex(s => new { s.ChildId, s.IsDismissed, s.ExpiresAt });

            // ═══════════════════════════════════════════════════════════════════
            //  PHASE 10 — NEW BADGE + LEVEL SYSTEM
            // ═══════════════════════════════════════════════════════════════════

            // ── A. Badge → Challenges (1:many) ───────────────────────────────
            builder.Entity<BadgeChallenge>()
                .HasOne(bc => bc.Badge)
                .WithMany(b => b.Challenges)
                .HasForeignKey(bc => bc.BadgeId)
                .OnDelete(DeleteBehavior.Cascade);

            // ── B. ChildChallengeProgress ─────────────────────────────────────
            builder.Entity<ChildChallengeProgress>()
                .HasOne(cp => cp.Child)
                .WithMany()
                .HasForeignKey(cp => cp.ChildId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ChildChallengeProgress>()
                .HasOne(cp => cp.Challenge)
                .WithMany(c => c.ChildProgresses)
                .HasForeignKey(cp => cp.ChallengeId)
                .OnDelete(DeleteBehavior.Cascade);

            // AI team spec: CompletedAt maps to UnlockedAt (same column)
            builder.Entity<ChildChallengeProgress>()
                .Ignore(cp => cp.CompletedAt);

            // Unique index: one progress record per (child, challenge)
            builder.Entity<ChildChallengeProgress>()
                .HasIndex(cp => new { cp.ChildId, cp.ChallengeId })
                .IsUnique();

            // Index on HashedChildId for AI team lookups
            builder.Entity<ChildChallengeProgress>()
                .HasIndex(cp => cp.HashedChildId);

            // ── C. Levels ──────────────────────────────────────────────────────
            builder.Entity<Level>()
                .HasIndex(l => l.LevelNumber)
                .IsUnique();

            // Title maps to Name (alias column)
            builder.Entity<Level>()
                .Property(l => l.Title).HasMaxLength(100);

            // min_trust_score stored as float
            builder.Entity<Level>()
                .Property(l => l.MinTrustScore).HasColumnType("float");

            // ── D. UserLevels ─────────────────────────────────────────────────
            builder.Entity<UserLevel>()
                .HasOne(ul => ul.Child)
                .WithMany()
                .HasForeignKey(ul => ul.ChildId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserLevel>()
                .HasOne(ul => ul.Level)
                .WithMany(l => l.UserLevels)
                .HasForeignKey(ul => ul.LevelId)
                .OnDelete(DeleteBehavior.Restrict);

            // One UserLevel record per child
            builder.Entity<UserLevel>()
                .HasIndex(ul => ul.ChildId)
                .IsUnique();

            // Index on HashedChildId for AI team lookups
            builder.Entity<UserLevel>()
                .HasIndex(ul => ul.HashedChildId);

            // ── E. DailyLogEntry HashedChildId ───────────────────────────────
            // EntryText, SubmissionDate are NotMapped aliases — ignored automatically.
            builder.Entity<DailyLogEntry>()
                .HasIndex(l => l.HashedChildId);

            // ═══════════════════════════════════════════════════════════════════
            //  PHASE 12 — AI INTEGRATION: PERSONALITY CLUSTERING,
            //  BADGE RECOMMENDER, LEAFY RECOMMENDATION
            // ═══════════════════════════════════════════════════════════════════

            // ── F1. PersonalityCluster catalog ────────────────────────────────
            builder.Entity<PersonalityCluster>()
                .HasIndex(pc => pc.ClusterKey)
                .IsUnique();

            // ── F2. AdminChildrenPersonalityCluster (per-child assignment history) ──
            builder.Entity<AdminChildrenPersonalityCluster>(acc =>
            {
                acc.Property(a => a.SimilarityScore).HasColumnType("float");

                acc.HasOne(a => a.Child)
                   .WithMany()
                   .HasForeignKey(a => a.ChildId)
                   .OnDelete(DeleteBehavior.Cascade);

                acc.HasOne(a => a.Cluster)
                   .WithMany(c => c.Assignments)
                   .HasForeignKey(a => a.PersonalityClusterId)
                   .OnDelete(DeleteBehavior.Restrict);

                // Fast "what is this child's current cluster?" lookup.
                acc.HasIndex(a => new { a.ChildId, a.IsCurrent });
                // Group assignments produced by the same AI clustering run.
                acc.HasIndex(a => a.ClusteringRunId);
                // AI team retrieval by privacy-safe key (matches DailyLogEntry/
                // ChildChallengeProgress/UserLevel HashedChildId index convention).
                acc.HasIndex(a => a.HashedChildId);
            });

            // ── F3. ChildProfile AI recommendation pointers (all nullable) ────
            builder.Entity<ChildProfile>(cp =>
            {
                cp.HasOne(c => c.RecommendedBadge)
                  .WithMany()
                  .HasForeignKey(c => c.RecommendedBadgeId)
                  .OnDelete(DeleteBehavior.SetNull);

                cp.HasOne(c => c.RecommendedLeafyTip)
                  .WithMany()
                  .HasForeignKey(c => c.RecommendedLeafyTipId)
                  .OnDelete(DeleteBehavior.SetNull);

                cp.HasOne(c => c.LastPersonalityCluster)
                  .WithMany()
                  .HasForeignKey(c => c.LastPersonalityClusterId)
                  .OnDelete(DeleteBehavior.SetNull);
            });

            // ═══════════════════════════════════════════════════════════════════
            //  PHASE 10 — LEVELS SEED DATA
            //  6 levels with AI team required_badges + min_trust_score columns.
            //  required_badges = badge count per level milestone badge.
            //  min_trust_score = 0.0–1.0 scale (spec-compliant).
            //  LevelUpBadgeId links to existing Badge IDs (8-13 = level milestone badges)
            // ═══════════════════════════════════════════════════════════════════

            builder.Entity<Level>().HasData(
                // ── SPEC: 5 levels exactly ───────────────────────────────────
                // Level 1 — Sprout:      0 badges, 0%
                // Level 2 — Seedling:    3 badges, 40%
                // Level 3 — Sapling:     6 badges, 60%
                // Level 4 — Branch:      9 badges, 75%
                // Level 5 — Mighty Tree: 12 badges, 90%
                new Level
                {
                    Id = 1,
                    LevelNumber = 1,
                    Name = "The Sprout",
                    Title = "Sprout",
                    RequiredBadges = 0,
                    MinTrustScore = 0.00,
                    MaxAnomalyScore = 100,
                    Emoji = "🌱",
                    Quote = "Every journey starts with a single step!",
                    CardBgColor = "#fffbe6",
                    CardTitleColor = "#e8a800",
                    BarColor = "linear-gradient(90deg,#a8e063,#56ab2f)",
                    ExpReward = 0,
                    LevelUpBadgeId = null
                },
                new Level
                {
                    Id = 2,
                    LevelNumber = 2,
                    Name = "The Seedling",
                    Title = "Seedling",
                    RequiredBadges = 3,
                    MinTrustScore = 0.40,
                    MaxAnomalyScore = 35,
                    Emoji = "🌿",
                    Quote = "You were so resilient this week!",
                    CardBgColor = "#e4f6ff",
                    CardTitleColor = "#4a8e9a",
                    BarColor = "linear-gradient(90deg,#4DB6AC,#26a69a)",
                    ExpReward = 50,
                    LevelUpBadgeId = 9
                },
                new Level
                {
                    Id = 3,
                    LevelNumber = 3,
                    Name = "The Sapling",
                    Title = "Sapling",
                    RequiredBadges = 6,
                    MinTrustScore = 0.60,
                    MaxAnomalyScore = 30,
                    Emoji = "🌸",
                    Quote = "Your kindness made the garden grow!",
                    CardBgColor = "#ffe4ec",
                    CardTitleColor = "#e6536f",
                    BarColor = "linear-gradient(90deg,#FFD97D,#ffa040)",
                    ExpReward = 100,
                    LevelUpBadgeId = 10
                },
                new Level
                {
                    Id = 4,
                    LevelNumber = 4,
                    Name = "The Branch",
                    Title = "Branch",
                    RequiredBadges = 9,
                    MinTrustScore = 0.75,
                    MaxAnomalyScore = 25,
                    Emoji = "🌳",
                    Quote = "You shine brighter every day!",
                    CardBgColor = "#fffef0",
                    CardTitleColor = "#d0900a",
                    BarColor = "linear-gradient(90deg,#FFD97D,#ff9800)",
                    ExpReward = 150,
                    LevelUpBadgeId = 11
                },
                new Level
                {
                    Id = 5,
                    LevelNumber = 5,
                    Name = "The Mighty Tree",
                    Title = "Mighty Tree",
                    RequiredBadges = 12,
                    MinTrustScore = 0.90,
                    MaxAnomalyScore = 20,
                    Emoji = "🌲",
                    Quote = "A true champion never stops growing!",
                    CardBgColor = "#f5f0ff",
                    CardTitleColor = "#7c4dff",
                    BarColor = "linear-gradient(90deg,#9575CD,#7c4dff)",
                    ExpReward = 200,
                    LevelUpBadgeId = 12
                }
            );

            // ═══════════════════════════════════════════════════════════════════
            //  PHASE 10 — BADGE CHALLENGES SEED DATA
            //
            //  Each level's milestone badge (IDs 8-13) gets its challenges defined here.
            //  Other badges (pillar badges IDs 28-51) get pillar_pct challenges.
            //
            //  BadgeId references:
            //  8  = Sprout Scout (Level 1)        9  = Blossom Buddy (Level 2)
            //  10 = Curiosity Captain (Level 3)   11 = Resilience Ranger (Level 4)
            //  12 = Master Guardian (Level 5)     13 = Grawiaa Legend (Level 6)
            //  2/3/4 = Consistency3/7/30 streak badges
            //  5/6/7 = TaskMaster5/20/50 task badges
            //  1     = Supportive (First Journal)
            //  28-51 = 8 Pillar Badges, 3 tiers each (Bronze/Silver/Gold)
            // ═══════════════════════════════════════════════════════════════════

            builder.Entity<BadgeChallenge>().HasData(

                // ── Badge 8: Sprout Scout (Level 1 milestone) ────────────────
                new BadgeChallenge { Id = 1, BadgeId = 8, SortOrder = 0, Emoji = "📖", Description = "Complete your first journal entry", MetricType = "total_logs", TargetValue = 1 },
                new BadgeChallenge { Id = 2, BadgeId = 8, SortOrder = 1, Emoji = "🦸", Description = "Set your hero name", MetricType = "has_hero_name", TargetValue = 1 },
                new BadgeChallenge { Id = 3, BadgeId = 8, SortOrder = 2, Emoji = "👨‍👩‍👧", Description = "Link to a parent", MetricType = "has_parent_link", TargetValue = 1 },

                // ── Badge 9: Blossom Buddy (Level 2 milestone) ───────────────
                new BadgeChallenge { Id = 4, BadgeId = 9, SortOrder = 0, Emoji = "🔥", Description = "Log 5 days in a row", MetricType = "streak", TargetValue = 5 },
                new BadgeChallenge { Id = 5, BadgeId = 9, SortOrder = 1, Emoji = "🕌", Description = "Pray all 5 prayers in one day", MetricType = "prayer_day", TargetValue = 1 },
                new BadgeChallenge { Id = 6, BadgeId = 9, SortOrder = 2, Emoji = "🎨", Description = "Try a new hobby", MetricType = "has_hobby", TargetValue = 1 },

                // ── Badge 10: Curiosity Captain (Level 3 milestone) ──────────
                new BadgeChallenge { Id = 7, BadgeId = 10, SortOrder = 0, Emoji = "🏠", Description = "Help family 3 times", MetricType = "helped_family", TargetValue = 3 },
                new BadgeChallenge { Id = 8, BadgeId = 10, SortOrder = 1, Emoji = "💬", Description = "Share a kind word", MetricType = "kind_word", TargetValue = 1 },
                new BadgeChallenge { Id = 9, BadgeId = 10, SortOrder = 2, Emoji = "📖", Description = "Read Quran for 7 days", MetricType = "quran_days", TargetValue = 7 },

                // ── Badge 11: Resilience Ranger (Level 4 milestone) ──────────
                new BadgeChallenge { Id = 10, BadgeId = 11, SortOrder = 0, Emoji = "⚽", Description = "Exercise 5 days", MetricType = "movement_days", TargetValue = 5 },
                new BadgeChallenge { Id = 11, BadgeId = 11, SortOrder = 1, Emoji = "📚", Description = "Complete all subjects (1+ day)", MetricType = "subjects_day", TargetValue = 1 },
                new BadgeChallenge { Id = 12, BadgeId = 11, SortOrder = 2, Emoji = "💧", Description = "Drink 6 glasses of water (1 day)", MetricType = "water_six", TargetValue = 1 },

                // ── Badge 12: Master Guardian (Level 5 milestone) ────────────
                new BadgeChallenge { Id = 13, BadgeId = 12, SortOrder = 0, Emoji = "🔥", Description = "30-day streak", MetricType = "streak", TargetValue = 30 },
                new BadgeChallenge { Id = 14, BadgeId = 12, SortOrder = 1, Emoji = "🏅", Description = "Earn all movement badges", MetricType = "movement_badges", TargetValue = 1 },
                new BadgeChallenge { Id = 15, BadgeId = 12, SortOrder = 2, Emoji = "🎯", Description = "Master 3 hobbies", MetricType = "hobby_count", TargetValue = 3 },

                // ── Badge 13: Grawiaa Legend (Level 6 — auto at level 6) ─────
                new BadgeChallenge { Id = 16, BadgeId = 13, SortOrder = 0, Emoji = "🌟", Description = "Complete all Champion challenges", MetricType = "total_logs", TargetValue = 30 },

                // ── Streak Badges (2=3-day, 3=7-day, 4=30-day) ───────────────
                new BadgeChallenge { Id = 17, BadgeId = 2, SortOrder = 0, Emoji = "🔥", Description = "Log 3 days in a row", MetricType = "streak", TargetValue = 3 },
                new BadgeChallenge { Id = 18, BadgeId = 3, SortOrder = 0, Emoji = "⚡", Description = "Log 7 days in a row", MetricType = "streak", TargetValue = 7 },
                new BadgeChallenge { Id = 19, BadgeId = 4, SortOrder = 0, Emoji = "🏅", Description = "Log 30 days in a row", MetricType = "streak", TargetValue = 30 },

                // ── Task Badges (5=5tasks, 6=20tasks, 7=50tasks) ──────────────
                new BadgeChallenge { Id = 20, BadgeId = 5, SortOrder = 0, Emoji = "✅", Description = "Complete 5 tasks", MetricType = "tasks_completed", TargetValue = 5 },
                new BadgeChallenge { Id = 21, BadgeId = 6, SortOrder = 0, Emoji = "🎯", Description = "Complete 20 tasks", MetricType = "tasks_completed", TargetValue = 20 },
                new BadgeChallenge { Id = 22, BadgeId = 7, SortOrder = 0, Emoji = "🏆", Description = "Complete 50 tasks", MetricType = "tasks_completed", TargetValue = 50 },

                // ── First Journal (Badge 1 = Supportive) ──────────────────────
                new BadgeChallenge { Id = 23, BadgeId = 1, SortOrder = 0, Emoji = "📖", Description = "Write your first journal entry", MetricType = "total_logs", TargetValue = 1 },

                // ══════════════════════════════════════════════════════════════════
                //  AI TEAM BADGE LIBRARY CHALLENGES  (badge_library.csv — Phase 12)
                //  Two free-text journal challenges per badge, exactly as the
                //  "Challenges" column of the AI team's CSV lists them
                //  (semicolon-separated in the source, split into one row each
                //  here). MetricType = "journal_text" because these are
                //  evaluated by the AI team's NLP pipeline against the child's
                //  journal entry text (GeneralNote/ReflectionText/FeelingNote),
                //  NOT by a backend-computed numeric metric like the legacy
                //  streak/tasks_completed/pillar_pct challenges above. The AI
                //  team marks ChildChallengeProgress.IsCompleted directly via
                //  the existing challenge-progress data (no new endpoint
                //  needed — same table, same hashed_child_id pattern).
                // ══════════════════════════════════════════════════════════════════

                // ── Badge 28: Morning Prayer ──────────────────────────────────
                new BadgeChallenge { Id = 24, BadgeId = 28, SortOrder = 0, Emoji = "🕋", Description = "Journal about praying Fajr on time", MetricType = "journal_text", TargetValue = 1 },
                new BadgeChallenge { Id = 25, BadgeId = 28, SortOrder = 1, Emoji = "🤲", Description = "Mention saying your morning Duas", MetricType = "journal_text", TargetValue = 1 },

                // ── Badge 29: Daily Gratitude ─────────────────────────────────
                new BadgeChallenge { Id = 26, BadgeId = 29, SortOrder = 0, Emoji = "🙏", Description = "Write down 3 things you are thankful for today", MetricType = "journal_text", TargetValue = 1 },
                new BadgeChallenge { Id = 27, BadgeId = 29, SortOrder = 1, Emoji = "😊", Description = "Share something that made you smile", MetricType = "journal_text", TargetValue = 1 },

                // ── Badge 30: Math Marathon ───────────────────────────────────
                new BadgeChallenge { Id = 28, BadgeId = 30, SortOrder = 0, Emoji = "🔢", Description = "Write about solving a tough math problem", MetricType = "journal_text", TargetValue = 1 },
                new BadgeChallenge { Id = 29, BadgeId = 30, SortOrder = 1, Emoji = "📐", Description = "Mention practicing your math skills", MetricType = "journal_text", TargetValue = 1 },

                // ── Badge 31: Storyteller ──────────────────────────────────────
                new BadgeChallenge { Id = 30, BadgeId = 31, SortOrder = 0, Emoji = "📝", Description = "Write a short creative story or poem in your journal", MetricType = "journal_text", TargetValue = 1 },
                new BadgeChallenge { Id = 31, BadgeId = 31, SortOrder = 1, Emoji = "📖", Description = "Tell a summary of a book you read today", MetricType = "journal_text", TargetValue = 1 },

                // ── Badge 32: Science Lab ─────────────────────────────────────
                new BadgeChallenge { Id = 32, BadgeId = 32, SortOrder = 0, Emoji = "🔬", Description = "Journal about a cool science experiment or fact you learned", MetricType = "journal_text", TargetValue = 1 },
                new BadgeChallenge { Id = 33, BadgeId = 32, SortOrder = 1, Emoji = "🌌", Description = "Write about exploring nature or space concepts", MetricType = "journal_text", TargetValue = 1 },

                // ── Badge 33: Team Sports ─────────────────────────────────────
                new BadgeChallenge { Id = 34, BadgeId = 33, SortOrder = 0, Emoji = "⚽", Description = "Write about playing a sport or active game with others", MetricType = "journal_text", TargetValue = 1 },
                new BadgeChallenge { Id = 35, BadgeId = 33, SortOrder = 1, Emoji = "📣", Description = "Mention cheering on a friend or teammate today", MetricType = "journal_text", TargetValue = 1 },

                // ── Badge 34: Kindness Mission ────────────────────────────────
                new BadgeChallenge { Id = 36, BadgeId = 34, SortOrder = 0, Emoji = "🤝", Description = "Journal about helping a friend or sibling out", MetricType = "journal_text", TargetValue = 1 },
                new BadgeChallenge { Id = 37, BadgeId = 34, SortOrder = 1, Emoji = "💬", Description = "Write about a nice compliment you gave someone", MetricType = "journal_text", TargetValue = 1 },

                // ── Badge 35: Public Speaking ─────────────────────────────────
                new BadgeChallenge { Id = 38, BadgeId = 35, SortOrder = 0, Emoji = "🎤", Description = "Write about sharing your ideas out loud in front of others", MetricType = "journal_text", TargetValue = 1 },
                new BadgeChallenge { Id = 39, BadgeId = 35, SortOrder = 1, Emoji = "🗣️", Description = "Mention practicing reading out loud", MetricType = "journal_text", TargetValue = 1 },

                // ── Badge 36: Nature Guide ────────────────────────────────────
                new BadgeChallenge { Id = 40, BadgeId = 36, SortOrder = 0, Emoji = "🌳", Description = "Write about spending time outside in the fresh air", MetricType = "journal_text", TargetValue = 1 },
                new BadgeChallenge { Id = 41, BadgeId = 36, SortOrder = 1, Emoji = "🐦", Description = "Journal about observing plants animals or trees today", MetricType = "journal_text", TargetValue = 1 },

                // ── Badge 37: Recycling Hero ──────────────────────────────────
                new BadgeChallenge { Id = 42, BadgeId = 37, SortOrder = 0, Emoji = "♻️", Description = "Write about sorting trash or recycling something today", MetricType = "journal_text", TargetValue = 1 },
                new BadgeChallenge { Id = 43, BadgeId = 37, SortOrder = 1, Emoji = "🧹", Description = "Journal about keeping your room or classroom tidy", MetricType = "journal_text", TargetValue = 1 },

                // ── Badge 38: Problem Solver ──────────────────────────────────
                new BadgeChallenge { Id = 44, BadgeId = 38, SortOrder = 0, Emoji = "🧩", Description = "Write about how you fixed a mistake or problem today", MetricType = "journal_text", TargetValue = 1 },
                new BadgeChallenge { Id = 45, BadgeId = 38, SortOrder = 1, Emoji = "💪", Description = "Journal about trying again when a task felt hard", MetricType = "journal_text", TargetValue = 1 },

                // ── Badge 39: Artistic Garden ─────────────────────────────────
                new BadgeChallenge { Id = 46, BadgeId = 39, SortOrder = 0, Emoji = "🎨", Description = "Journal about a drawing painting or craft you created", MetricType = "journal_text", TargetValue = 1 },
                new BadgeChallenge { Id = 47, BadgeId = 39, SortOrder = 1, Emoji = "🌈", Description = "Write about your favorite colors or designs today", MetricType = "journal_text", TargetValue = 1 },

                // ── Badge 40: Community Helper ────────────────────────────────
                new BadgeChallenge { Id = 48, BadgeId = 40, SortOrder = 0, Emoji = "🏠", Description = "Write about completing your family chores without being asked", MetricType = "journal_text", TargetValue = 1 },
                new BadgeChallenge { Id = 49, BadgeId = 40, SortOrder = 1, Emoji = "🌟", Description = "Journal about doing an act of service for your community", MetricType = "journal_text", TargetValue = 1 }
            );


            // ── 11. LeafyTip — three-channel content config ───────────
            //
            // [A3] PHASE 5: LeafyTip now has three explicit content columns:
            //
            //   Tip        → child-facing practical advice (Purpose = "tip")
            //   Alert      → parent-facing urgent notification (Purpose = "alert")
            //   Motivation → child-facing praise (Purpose = "motivation")
            //
            // The legacy Content column is kept Required for backward
            // compatibility with existing seeded rows and admin list API.
            // New CRUD calls must supply at least one of Tip/Alert/Motivation
            // in addition to Content (see UpsertLeafyTipDto in Phase5Dtos.cs).
            //
            // The composite index (Purpose, Side) enables the sub-second
            // lookup used by TipGenerationService, AlertGenerationService,
            // and MotivationGenerationService when filtering by channel.
            builder.Entity<LeafyTip>(b =>
            {
                b.Property(t => t.Side).HasMaxLength(30).IsRequired();
                b.Property(t => t.Type).HasMaxLength(20).IsRequired();
                b.Property(t => t.Purpose).HasMaxLength(20).IsRequired().HasDefaultValue("tip");
                b.Property(t => t.Content).HasMaxLength(500).IsRequired();

                // Three dedicated content channel columns
                b.Property(t => t.Tip).HasMaxLength(500);        // nullable — populated when Purpose = "tip"
                b.Property(t => t.Alert).HasMaxLength(500);      // nullable — populated when Purpose = "alert"
                b.Property(t => t.Motivation).HasMaxLength(500); // nullable — populated when Purpose = "motivation"

                // Composite index for fast lookup by channel + pillar side
                b.HasIndex(t => new { t.Purpose, t.Side });
            });

            // ── 12. Role Seed ─────────────────────────────────────────
            builder.Entity<IdentityRole<int>>().HasData(
                new IdentityRole<int> { Id = 1, Name = "Parent", NormalizedName = "PARENT" },
                new IdentityRole<int> { Id = 2, Name = "Child", NormalizedName = "CHILD" },
                new IdentityRole<int> { Id = 3, Name = "Admin", NormalizedName = "ADMIN" }
            );

            // ── 13. Hobby Seed ────────────────────────────────────────
            builder.Entity<Hobby>().HasData(
                new Hobby { Id = 1, Name = "رسم", Icon = "🎨", Category = Category.Feelings },
                new Hobby { Id = 2, Name = "رياضة", Icon = "⚽", Category = Category.Moving },
                new Hobby { Id = 3, Name = "قراءة", Icon = "📚", Category = Category.Homework }
            );

            // ── 14. Subject Seed ──────────────────────────────────────
            builder.Entity<Subject>().HasData(
                new Subject { Id = 1, Name = "رياضيات", Icon = "🔢", Category = Category.Homework },
                new Subject { Id = 2, Name = "قرآن", Icon = "🕌", Category = Category.Religion }
            );

            // ── 15. Badge Seed ────────────────────────────────────────
            builder.Entity<Badge>().HasData(
                // ── Streak / Consistency ─────────────────────────────────────────
                new Badge { Id = 1, Name = "First Journal", Description = "Wrote your very first journal entry!", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#F59E0B\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#FDE68A\"/><path d=\"M11 11h4.5v10H11zm1 1v8h2.5v-8z\" fill=\"#92400E\"/><path d=\"M16.5 11H21v10h-4.5zm1 1v8h2.5v-8z\" fill=\"#92400E\"/><path d=\"M11 21h10v1H11z\" fill=\"#92400E\"/></svg>", PersonalitySide = "Right", Category = BadgeCategory.Supportive },
                new Badge { Id = 2, Name = "3-Day Streak", Description = "Logged 3 days in a row — great habit!", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#EF4444\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#FEE2E2\"/><path d=\"M16 8c0 0-4 4-4 8a4 4 0 008 0c0-2-1-3.5-1-3.5s-.5 1.5-1.5 2c0-3-1.5-6.5-1.5-6.5z\" fill=\"#EF4444\"/><path d=\"M15 16c0 0-1.5 1-1.5 2.5a2.5 2.5 0 005 0c0-1-.5-1.5-.5-1.5s-.5 1-1.5 1c0-1-.5-2-.5-2z\" fill=\"#FCA5A5\"/></svg>", PersonalitySide = "Left", Category = BadgeCategory.Consistency3 },
                new Badge { Id = 3, Name = "Flame Hero", Description = "Logged 7 days in a row — you're on fire!", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#DC2626\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#FEE2E2\"/><path d=\"M18 9l-5 8h4l-3 6 7-9h-4z\" fill=\"#F59E0B\" stroke=\"#DC2626\" stroke-width=\".5\"/></svg>", PersonalitySide = "Left", Category = BadgeCategory.Consistency7 },
                new Badge { Id = 4, Name = "Legend Streaker", Description = "Logged 30 days in a row — incredible!", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#991B1B\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#FEE2E2\"/><path d=\"M10 20l2-6 4 3 4-3 2 6H10z\" fill=\"#F59E0B\"/><circle cx=\"10\" cy=\"14\" r=\"1.5\" fill=\"#F59E0B\"/><circle cx=\"16\" cy=\"11\" r=\"1.5\" fill=\"#F59E0B\"/><circle cx=\"22\" cy=\"14\" r=\"1.5\" fill=\"#F59E0B\"/><rect x=\"10\" y=\"20\" width=\"12\" height=\"2\" rx=\"1\" fill=\"#B45309\"/></svg>", PersonalitySide = "Left", Category = BadgeCategory.Consistency30 },

                // ── Task Master ──────────────────────────────────────────────────
                new Badge { Id = 5, Name = "Task Starter", Description = "Completed 5 tasks — a great beginning!", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#0D9488\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#CCFBF1\"/><path d=\"M11 16l3.5 3.5L21 12\" stroke=\"#0D9488\" stroke-width=\"2.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\" fill=\"none\"/></svg>", PersonalitySide = "Right", Category = BadgeCategory.TaskMaster5 },
                new Badge { Id = 6, Name = "Task Achiever", Description = "Completed 20 tasks — super achiever!", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#0F766E\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#CCFBF1\"/><circle cx=\"16\" cy=\"16\" r=\"7\" fill=\"none\" stroke=\"#0F766E\" stroke-width=\"1.5\"/><circle cx=\"16\" cy=\"16\" r=\"4\" fill=\"none\" stroke=\"#0F766E\" stroke-width=\"1.5\"/><circle cx=\"16\" cy=\"16\" r=\"2\" fill=\"#0F766E\"/><path d=\"M22 10l-2 4.5\" stroke=\"#EF4444\" stroke-width=\"1.5\" stroke-linecap=\"round\"/><circle cx=\"22\" cy=\"10\" r=\"1\" fill=\"#EF4444\"/></svg>", PersonalitySide = "Right", Category = BadgeCategory.TaskMaster20 },
                new Badge { Id = 7, Name = "Task Champion", Description = "Completed 50 tasks — absolute champion!", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#115E59\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#CCFBF1\"/><path d=\"M13 10h6v6a3 3 0 01-6 0V10z\" fill=\"#F59E0B\"/><path d=\"M10 11h3v3a1.5 1.5 0 01-3 0V11z\" fill=\"#F59E0B\"/><path d=\"M19 11h3v3a1.5 1.5 0 01-3 0V11z\" fill=\"#F59E0B\"/><rect x=\"15\" y=\"16\" width=\"2\" height=\"3\" fill=\"#B45309\"/><rect x=\"13\" y=\"19\" width=\"6\" height=\"2\" rx=\"1\" fill=\"#B45309\"/></svg>", PersonalitySide = "Right", Category = BadgeCategory.TaskMaster50 },

                // ── Level Milestones ─────────────────────────────────────────────
                new Badge { Id = 8, Name = "Sprout Scout", Description = "Started the journey — Level 1 reached!", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#16A34A\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#DCFCE7\"/><path d=\"M16 22V14\" stroke=\"#16A34A\" stroke-width=\"1.5\" stroke-linecap=\"round\"/><path d=\"M16 17c0-3 3-5 3-5s0 3-3 5z\" fill=\"#16A34A\"/><path d=\"M16 15c0-2-3-4-3-4s0 3 3 4z\" fill=\"#4ADE80\"/></svg>", PersonalitySide = "Right", Category = BadgeCategory.LevelSeedling },
                new Badge { Id = 9, Name = "Blossom Buddy", Description = "Reached Level 5 — kindness is blooming!", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#DB2777\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#FCE7F3\"/><circle cx=\"16\" cy=\"16\" r=\"3\" fill=\"#F59E0B\"/><ellipse cx=\"16\" cy=\"11\" rx=\"2\" ry=\"3\" fill=\"#F9A8D4\"/><ellipse cx=\"16\" cy=\"21\" rx=\"2\" ry=\"3\" fill=\"#F9A8D4\"/><ellipse cx=\"11\" cy=\"16\" rx=\"3\" ry=\"2\" fill=\"#F9A8D4\"/><ellipse cx=\"21\" cy=\"16\" rx=\"3\" ry=\"2\" fill=\"#F9A8D4\"/></svg>", PersonalitySide = "Right", Category = BadgeCategory.LevelExplorer },
                new Badge { Id = 10, Name = "Curiosity Captain", Description = "Reached Level 9 — brain power unlocked!", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#4338CA\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#EEF2FF\"/><path d=\"M13 11c0 0 0-2 3-2s3 2 3 2-1 2-3 3c-2-1-3-3-3-3z\" fill=\"#F59E0B\"/><rect x=\"15\" y=\"14\" width=\"2\" height=\"3\" fill=\"#6366F1\"/><rect x=\"13\" y=\"17\" width=\"6\" height=\"2\" rx=\"1\" fill=\"#4338CA\"/><circle cx=\"20\" cy=\"10\" r=\"1\" fill=\"#F59E0B\"/></svg>", PersonalitySide = "Left", Category = BadgeCategory.LevelAdventurer },
                new Badge { Id = 11, Name = "Resilience Ranger", Description = "Reached Level 13 — true hero strength!", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#1D4ED8\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#EFF6FF\"/><path d=\"M16 10l-5 2v4c0 3 2 5 5 6 3-1 5-3 5-6v-4l-5-2z\" fill=\"#3B82F6\"/><path d=\"M13.5 16l2 2 3-3\" stroke=\"white\" stroke-width=\"1.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\" fill=\"none\"/></svg>", PersonalitySide = "Left", Category = BadgeCategory.LevelChampion },
                new Badge { Id = 12, Name = "Master Guardian", Description = "Reached Level 17 — the ultimate champion!", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#B45309\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#FEF3C7\"/><path d=\"M16 10l1.5 4h4.5l-3.5 2.5 1.5 4.5L16 19l-4 2 1.5-4.5L10 14h4.5z\" fill=\"#F59E0B\" stroke=\"#B45309\" stroke-width=\".5\"/></svg>", PersonalitySide = "Right", Category = BadgeCategory.LevelLegend },
                new Badge { Id = 13, Name = "Grawiaa Legend", Description = "Reached Level 20 — you ARE a legend!", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#78350F\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#FEF3C7\"/><path d=\"M16 9l1.2 3.5h3.8l-3 2.2 1.2 3.5L16 16.2l-3.2 2 1.2-3.5-3-2.2h3.8z\" fill=\"#FBBF24\"/><circle cx=\"16\" cy=\"9\" r=\"1\" fill=\"#F59E0B\"/><circle cx=\"23\" cy=\"14\" r=\"1\" fill=\"#F59E0B\"/><circle cx=\"20\" cy=\"22\" r=\"1\" fill=\"#F59E0B\"/><circle cx=\"12\" cy=\"22\" r=\"1\" fill=\"#F59E0B\"/><circle cx=\"9\" cy=\"14\" r=\"1\" fill=\"#F59E0B\"/></svg>", PersonalitySide = "Right", Category = BadgeCategory.LevelGrawiaaHero },

                // ══════════════════════════════════════════════════════════════════
                //  AI TEAM BADGE LIBRARY  (badge_library.csv — Phase 12 spec)
                //  IDs 28-40: 13 badges with 8-pillar percentage vectors.
                //  IDs 14-27 removed — had no challenges, unreachable by engine.
                // ══════════════════════════════════════════════════════════════════

                new Badge { Id = 28, Name = "Morning Prayer", Description = "Start your day bright with a quiet moment of connection.", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#0F766E\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#CCFBF1\"/><path d=\"M18 11a6 6 0 100 10 5 5 0 010-10z\" fill=\"#0F766E\"/><circle cx=\"21\" cy=\"11\" r=\"1.5\" fill=\"#F59E0B\"/></svg>", PersonalitySide = "Right", Category = BadgeCategory.SpiritualBronze, AcadPct = 0.1m, SocPct = 0.1m, MentPct = 0.7m, PhysPct = 0.1m, SpirPct = 1.0m, EnvPct = 0.1m, CreaPct = 0.1m, ResPct = 0.5m },
                new Badge { Id = 29, Name = "Daily Gratitude", Description = "Celebrate the beautiful little blessings all around you.", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#0D9488\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#CCFBF1\"/><path d=\"M16 20c0 0-5-3.5-5-6.5a2.5 2.5 0 015 0 2.5 2.5 0 015 0c0 3-5 6.5-5 6.5z\" fill=\"#EF4444\"/><path d=\"M16 9v2M21 11l-1.5 1.5M23 16h-2M11 11l1.5 1.5M9 16h2\" stroke=\"#F59E0B\" stroke-width=\"1.5\" stroke-linecap=\"round\"/></svg>", PersonalitySide = "Right", Category = BadgeCategory.SpiritualSilver, AcadPct = 0.3m, SocPct = 0.2m, MentPct = 1.0m, PhysPct = 0.0m, SpirPct = 0.6m, EnvPct = 0.1m, CreaPct = 0.5m, ResPct = 0.9m },
                new Badge { Id = 30, Name = "Math Marathon", Description = "Power up your brain by conquering tricky numbers!", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#2563EB\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#EFF6FF\"/><text x=\"16\" y=\"21\" text-anchor=\"middle\" font-size=\"14\" font-weight=\"bold\" fill=\"#1D4ED8\" font-family=\"serif\">&#x3A3;</text></svg>", PersonalitySide = "Left", Category = BadgeCategory.AcademicBronze, AcadPct = 1.0m, SocPct = 0.2m, MentPct = 0.8m, PhysPct = 0.1m, SpirPct = 0.1m, EnvPct = 0.1m, CreaPct = 0.4m, ResPct = 0.6m },
                new Badge { Id = 31, Name = "Storyteller", Description = "Let your wonderful imagination paint beautiful pictures.", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#7C3AED\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#EDE9FE\"/><rect x=\"11\" y=\"12\" width=\"4\" height=\"8\" rx=\"1\" fill=\"#7C3AED\"/><rect x=\"17\" y=\"12\" width=\"4\" height=\"8\" rx=\"1\" fill=\"#8B5CF6\"/><path d=\"M11 12h10v1H11z\" fill=\"#4C1D95\"/><circle cx=\"21\" cy=\"10\" r=\"1.5\" fill=\"#F59E0B\"/></svg>", PersonalitySide = "Left", Category = BadgeCategory.CreativeExplorer, AcadPct = 0.7m, SocPct = 0.5m, MentPct = 0.6m, PhysPct = 0.0m, SpirPct = 0.2m, EnvPct = 0.1m, CreaPct = 1.0m, ResPct = 0.4m },
                new Badge { Id = 32, Name = "Science Lab", Description = "Explore how the world works like a real scientist.", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#0369A1\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#E0F2FE\"/><circle cx=\"16\" cy=\"16\" r=\"2.5\" fill=\"#0369A1\"/><ellipse cx=\"16\" cy=\"16\" rx=\"7\" ry=\"3.5\" fill=\"none\" stroke=\"#0369A1\" stroke-width=\"1.2\"/><ellipse cx=\"16\" cy=\"16\" rx=\"7\" ry=\"3.5\" fill=\"none\" stroke=\"#0369A1\" stroke-width=\"1.2\" transform=\"rotate(60 16 16)\"/><ellipse cx=\"16\" cy=\"16\" rx=\"7\" ry=\"3.5\" fill=\"none\" stroke=\"#0369A1\" stroke-width=\"1.2\" transform=\"rotate(120 16 16)\"/></svg>", PersonalitySide = "Left", Category = BadgeCategory.AcademicSilver, AcadPct = 0.9m, SocPct = 0.3m, MentPct = 0.7m, PhysPct = 0.2m, SpirPct = 0.1m, EnvPct = 0.6m, CreaPct = 0.8m, ResPct = 0.4m },
                new Badge { Id = 33, Name = "Team Sports", Description = "Move your body and shine brightly alongside your teammates.", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#15803D\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#DCFCE7\"/><circle cx=\"16\" cy=\"16\" r=\"6\" fill=\"white\" stroke=\"#15803D\" stroke-width=\"1\"/><polygon points=\"16,11 17.5,14.5 21,14.5 18.5,16.5 19.5,20 16,17.5 12.5,20 13.5,16.5 11,14.5 14.5,14.5\" fill=\"#15803D\" opacity=\".4\"/><circle cx=\"16\" cy=\"16\" r=\"2\" fill=\"#15803D\"/></svg>", PersonalitySide = "Right", Category = BadgeCategory.SportBronze, AcadPct = 0.1m, SocPct = 1.0m, MentPct = 0.4m, PhysPct = 1.0m, SpirPct = 0.1m, EnvPct = 0.2m, CreaPct = 0.1m, ResPct = 0.8m },
                new Badge { Id = 34, Name = "Kindness Mission", Description = "Spread warmth and joy to the people around you.", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#D97706\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#FEF3C7\"/><path d=\"M16 21c0 0-6-4-6-7.5a3 3 0 016 0 3 3 0 016 0c0 3.5-6 7.5-6 7.5z\" fill=\"#EF4444\"/><circle cx=\"16\" cy=\"12.5\" r=\"2\" fill=\"white\" opacity=\".8\"/></svg>", PersonalitySide = "Right", Category = BadgeCategory.SocialBronze, AcadPct = 0.2m, SocPct = 1.0m, MentPct = 0.7m, PhysPct = 0.1m, SpirPct = 0.8m, EnvPct = 0.2m, CreaPct = 0.3m, ResPct = 0.5m },
                new Badge { Id = 35, Name = "Public Speaking", Description = "Be brave and share your voice clearly with the world.", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#4338CA\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#EEF2FF\"/><rect x=\"14\" y=\"9\" width=\"4\" height=\"7\" rx=\"2\" fill=\"#4338CA\"/><path d=\"M12 15a4 4 0 008 0\" fill=\"none\" stroke=\"#4338CA\" stroke-width=\"1.5\"/><line x1=\"16\" y1=\"19\" x2=\"16\" y2=\"22\" stroke=\"#4338CA\" stroke-width=\"1.5\"/><line x1=\"13\" y1=\"22\" x2=\"19\" y2=\"22\" stroke=\"#4338CA\" stroke-width=\"1.5\" stroke-linecap=\"round\"/></svg>", PersonalitySide = "Left", Category = BadgeCategory.SocialSilver, AcadPct = 0.6m, SocPct = 0.9m, MentPct = 0.8m, PhysPct = 0.2m, SpirPct = 0.2m, EnvPct = 0.1m, CreaPct = 0.4m, ResPct = 0.9m },
                new Badge { Id = 36, Name = "Nature Guide", Description = "Discover and protect the beautiful outdoors around you.", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#166534\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#DCFCE7\"/><path d=\"M16 22c0 0-6-4-6-8a6 6 0 0112 0c0 4-6 8-6 8z\" fill=\"#16A34A\"/><path d=\"M16 22V14\" stroke=\"#DCFCE7\" stroke-width=\"1\" stroke-linecap=\"round\"/><path d=\"M16 18l3-3M16 16l-2-2\" stroke=\"#DCFCE7\" stroke-width=\"1\" stroke-linecap=\"round\"/></svg>", PersonalitySide = "Right", Category = BadgeCategory.HomeResBronze, AcadPct = 0.4m, SocPct = 0.3m, MentPct = 0.5m, PhysPct = 0.6m, SpirPct = 0.4m, EnvPct = 1.0m, CreaPct = 0.2m, ResPct = 0.4m },
                new Badge { Id = 37, Name = "Recycling Hero", Description = "Take care of the earth by keeping your surroundings clean.", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#0F766E\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#CCFBF1\"/><path d=\"M16 10l2.5 4H19L16 19l-3-5h.5L16 10z\" fill=\"#0F766E\"/><path d=\"M22 17.5l-3.5-1.5 1 2.5-2 1.5 5 .5-1.5-3z\" fill=\"#0F766E\"/><path d=\"M10 17.5l3.5-1.5-1 2.5 2 1.5-5 .5 1.5-3z\" fill=\"#0F766E\"/></svg>", PersonalitySide = "Right", Category = BadgeCategory.HomeResSilver, AcadPct = 0.3m, SocPct = 0.5m, MentPct = 0.4m, PhysPct = 0.3m, SpirPct = 0.2m, EnvPct = 1.0m, CreaPct = 0.2m, ResPct = 0.5m },
                new Badge { Id = 38, Name = "Problem Solver", Description = "Stay strong and find creative answers when things get tough.", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#6D28D9\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#EDE9FE\"/><path d=\"M12 12h4c0-1 .5-2 1.5-2s1.5 1 1.5 2h1v4c1 0 2 .5 2 1.5s-1 1.5-2 1.5v1h-4c0 1-.5 2-1.5 2s-1.5-1-1.5-2h-1v-4c-1 0-2-.5-2-1.5s1-1.5 2-1.5V12z\" fill=\"#7C3AED\"/></svg>", PersonalitySide = "Left", Category = BadgeCategory.MentalBronze, AcadPct = 0.8m, SocPct = 0.4m, MentPct = 0.9m, PhysPct = 0.1m, SpirPct = 0.1m, EnvPct = 0.1m, CreaPct = 0.6m, ResPct = 1.0m },
                new Badge { Id = 39, Name = "Artistic Garden", Description = "Let your inner creativity bloom with beautiful colors.", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#A21CAF\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#FAE8FF\"/><circle cx=\"16\" cy=\"16\" r=\"5\" fill=\"white\" stroke=\"#A21CAF\" stroke-width=\"1\"/><circle cx=\"13\" cy=\"13.5\" r=\"1.5\" fill=\"#EF4444\"/><circle cx=\"19\" cy=\"13.5\" r=\"1.5\" fill=\"#3B82F6\"/><circle cx=\"21\" cy=\"17\" r=\"1.5\" fill=\"#16A34A\"/><circle cx=\"11\" cy=\"17\" r=\"1.5\" fill=\"#F59E0B\"/><circle cx=\"16\" cy=\"21\" r=\"1.5\" fill=\"#7C3AED\"/></svg>", PersonalitySide = "Left", Category = BadgeCategory.HobbiesBronze, AcadPct = 0.2m, SocPct = 0.2m, MentPct = 0.6m, PhysPct = 0.4m, SpirPct = 0.3m, EnvPct = 0.8m, CreaPct = 0.9m, ResPct = 0.3m },
                new Badge { Id = 40, Name = "Community Helper", Description = "Be a shining light in your neighborhood and household.", IconUrl = "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#C2410C\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#FFF7ED\"/><path d=\"M16 10l7 6h-2v6H11v-6H9z\" fill=\"#C2410C\"/><path d=\"M14 22v-4h4v4\" fill=\"#FFF7ED\"/><path d=\"M15 16c0 0-2-1.5-2-2.5a1.5 1.5 0 013 0 1.5 1.5 0 013 0c0 1-2 2.5-2 2.5z\" fill=\"#EF4444\"/></svg>", PersonalitySide = "Right", Category = BadgeCategory.NutritionBronze, AcadPct = 0.1m, SocPct = 0.9m, MentPct = 0.6m, PhysPct = 0.5m, SpirPct = 0.7m, EnvPct = 0.6m, CreaPct = 0.2m, ResPct = 0.7m }
            );

            // ── 16. LeafyTip Seed — 8 pillars × 3 purposes ───────────
            //
            // Each record populates BOTH the legacy Content field AND the
            // appropriate dedicated channel field (Tip/Alert/Motivation)
            // so that existing consumers of Content continue to work while
            // new code can use the typed channel fields directly.
            builder.Entity<LeafyTip>().HasData(

                // ── SPIRITUAL (Pillar 1: Connection with Allah) ─────────
                new LeafyTip
                {
                    Id = 101,
                    Side = "spiritual",
                    Type = "encouragement",
                    Purpose = "tip",
                    Tip = "Try praying Fajr on time tomorrow — even 5 minutes of Quran at dawn will uplift your spirit! 🕌",
                    Content = "Try praying Fajr on time tomorrow — even 5 minutes of Quran at dawn will uplift your spirit! 🕌"
                },
                new LeafyTip
                {
                    Id = 102,
                    Side = "spiritual",
                    Type = "warning",
                    Purpose = "alert",
                    Alert = "⚠️ Your child's spiritual connection score has dropped below 20% this week. Consider scheduling family Quran time.",
                    Content = "⚠️ Your child's spiritual connection score has dropped below 20% this week. Consider scheduling family Quran time."
                },
                new LeafyTip
                {
                    Id = 103,
                    Side = "spiritual",
                    Type = "achievement",
                    Purpose = "motivation",
                    Motivation = "🌟 MashaAllah! You completed all your prayers AND made Athkar today — you are a true spiritual hero!",
                    Content = "🌟 MashaAllah! You completed all your prayers AND made Athkar today — you are a true spiritual hero!"
                },

                // ── SOCIAL (Pillar 2: Social Interaction) ───────────────
                new LeafyTip
                {
                    Id = 201,
                    Side = "social",
                    Type = "encouragement",
                    Purpose = "tip",
                    Tip = "Smile at a friend tomorrow and say something kind — small acts of kindness make you a social superstar! 🤝",
                    Content = "Smile at a friend tomorrow and say something kind — small acts of kindness make you a social superstar! 🤝"
                },
                new LeafyTip
                {
                    Id = 202,
                    Side = "social",
                    Type = "warning",
                    Purpose = "alert",
                    Alert = "⚠️ Your child seems to be spending very little time with others this week. Check in about their friendships.",
                    Content = "⚠️ Your child seems to be spending very little time with others this week. Check in about their friendships."
                },
                new LeafyTip
                {
                    Id = 203,
                    Side = "social",
                    Type = "achievement",
                    Purpose = "motivation",
                    Motivation = "🎉 Wow! You showed up for your friends and family so well this week — your Kindness trait is soaring!",
                    Content = "🎉 Wow! You showed up for your friends and family so well this week — your Kindness trait is soaring!"
                },

                // ── ACADEMIC (Pillar 3: Homework & Study) ───────────────
                new LeafyTip
                {
                    Id = 301,
                    Side = "academic",
                    Type = "encouragement",
                    Purpose = "tip",
                    Tip = "Try the Pomodoro method: 25 minutes of focused study, then a 5-minute break. Your Curiosity trait will thank you! 📚",
                    Content = "Try the Pomodoro method: 25 minutes of focused study, then a 5-minute break. Your Curiosity trait will thank you! 📚"
                },
                new LeafyTip
                {
                    Id = 302,
                    Side = "academic",
                    Type = "warning",
                    Purpose = "alert",
                    Alert = "⚠️ Homework & Study scores are critically low this week. Your child may need extra support or motivation.",
                    Content = "⚠️ Homework & Study scores are critically low this week. Your child may need extra support or motivation."
                },
                new LeafyTip
                {
                    Id = 303,
                    Side = "academic",
                    Type = "achievement",
                    Purpose = "motivation",
                    Motivation = "📖 You crushed your homework this week! Your Curiosity trait has grown strong — keep exploring!",
                    Content = "📖 You crushed your homework this week! Your Curiosity trait has grown strong — keep exploring!"
                },

                // ── ENVIRONMENT (Pillar 4: Family Chores) ───────────────
                new LeafyTip
                {
                    Id = 401,
                    Side = "environment",
                    Type = "encouragement",
                    Purpose = "tip",
                    Tip = "Help set the table tonight or fold some laundry — small acts of helping build your Kindness superpower! 🏠",
                    Content = "Help set the table tonight or fold some laundry — small acts of helping build your Kindness superpower! 🏠"
                },
                new LeafyTip
                {
                    Id = 402,
                    Side = "environment",
                    Type = "warning",
                    Purpose = "alert",
                    Alert = "⚠️ Your child's home responsibility score is very low. Gentle encouragement around household participation may help.",
                    Content = "⚠️ Your child's home responsibility score is very low. Gentle encouragement around household participation may help."
                },
                new LeafyTip
                {
                    Id = 403,
                    Side = "environment",
                    Type = "achievement",
                    Purpose = "motivation",
                    Motivation = "🏆 You helped your family so much this week! Your Kindness and Resilience traits are both glowing!",
                    Content = "🏆 You helped your family so much this week! Your Kindness and Resilience traits are both glowing!"
                },

                // ── PHYSICAL (Pillar 5: Nutrition & Food) ───────────────
                new LeafyTip
                {
                    Id = 501,
                    Side = "physical",
                    Type = "encouragement",
                    Purpose = "tip",
                    Tip = "Swap one snack for a fruit today — your body and your Vitality trait will both feel the boost! 🍎",
                    Content = "Swap one snack for a fruit today — your body and your Vitality trait will both feel the boost! 🍎"
                },
                new LeafyTip
                {
                    Id = 502,
                    Side = "physical",
                    Type = "warning",
                    Purpose = "alert",
                    Alert = "⚠️ Nutrition & Food scores are declining. Your child may be eating irregularly — check meals and hydration.",
                    Content = "⚠️ Nutrition & Food scores are declining. Your child may be eating irregularly — check meals and hydration."
                },
                new LeafyTip
                {
                    Id = 503,
                    Side = "physical",
                    Type = "achievement",
                    Purpose = "motivation",
                    Motivation = "🥗 Amazing nutrition choices this week! Your Vitality trait is powering up — you're a food champion!",
                    Content = "🥗 Amazing nutrition choices this week! Your Vitality trait is powering up — you're a food champion!"
                },

                // ── SPORT (Pillar 6: Sport & Movement) ──────────────────
                new LeafyTip
                {
                    Id = 601,
                    Side = "sport",
                    Type = "encouragement",
                    Purpose = "tip",
                    Tip = "Take a 15-minute walk or play your favourite sport today — moving your body charges up your Vitality! ⚽",
                    Content = "Take a 15-minute walk or play your favourite sport today — moving your body charges up your Vitality! ⚽"
                },
                new LeafyTip
                {
                    Id = 602,
                    Side = "sport",
                    Type = "warning",
                    Purpose = "alert",
                    Alert = "⚠️ Physical activity has been very low this week. Encourage outdoor play or a short walk after school.",
                    Content = "⚠️ Physical activity has been very low this week. Encourage outdoor play or a short walk after school."
                },
                new LeafyTip
                {
                    Id = 603,
                    Side = "sport",
                    Type = "achievement",
                    Purpose = "motivation",
                    Motivation = "🏃 You moved your body every day this week — incredible! Your Vitality and Resilience traits are at full power!",
                    Content = "🏃 You moved your body every day this week — incredible! Your Vitality and Resilience traits are at full power!"
                },

                // ── CREATIVITY (Pillar 7: Hobbies) ──────────────────────
                new LeafyTip
                {
                    Id = 701,
                    Side = "creativity",
                    Type = "encouragement",
                    Purpose = "tip",
                    Tip = "Spend 20 minutes on a hobby today — drawing, building, or crafting. Your Curiosity trait grows every time you create! 🎨",
                    Content = "Spend 20 minutes on a hobby today — drawing, building, or crafting. Your Curiosity trait grows every time you create! 🎨"
                },
                new LeafyTip
                {
                    Id = 702,
                    Side = "creativity",
                    Type = "warning",
                    Purpose = "alert",
                    Alert = "⚠️ Creative engagement is very low this week. Your child may benefit from screen-free creative activities.",
                    Content = "⚠️ Creative engagement is very low this week. Your child may benefit from screen-free creative activities."
                },
                new LeafyTip
                {
                    Id = 703,
                    Side = "creativity",
                    Type = "achievement",
                    Purpose = "motivation",
                    Motivation = "🎨 You explored so many creative hobbies this week! Your Curiosity trait is bursting with colour!",
                    Content = "🎨 You explored so many creative hobbies this week! Your Curiosity trait is bursting with colour!"
                },

                // ── MENTAL (Pillar 8: Mental Health / Mood) ──────────────
                new LeafyTip
                {
                    Id = 801,
                    Side = "mental",
                    Type = "encouragement",
                    Purpose = "tip",
                    Tip = "It's okay to feel sad or tired sometimes. Write down one thing you're grateful for today — it helps your Resilience grow! 💗",
                    Content = "It's okay to feel sad or tired sometimes. Write down one thing you're grateful for today — it helps your Resilience grow! 💗"
                },
                new LeafyTip
                {
                    Id = 802,
                    Side = "mental",
                    Type = "warning",
                    Purpose = "alert",
                    Alert = "⚠️ Your child's mood & mental health signals are concerning this week. Multiple negative emotions detected — a check-in conversation is recommended.",
                    Content = "⚠️ Your child's mood & mental health signals are concerning this week. Multiple negative emotions detected — a check-in conversation is recommended."
                },
                new LeafyTip
                {
                    Id = 803,
                    Side = "mental",
                    Type = "achievement",
                    Purpose = "motivation",
                    Motivation = "💪 You handled your emotions so well this week — even the hard days! Your Resilience trait is incredibly strong!",
                    Content = "💪 You handled your emotions so well this week — even the hard days! Your Resilience trait is incredibly strong!"
                }
            );
        }
    }
}
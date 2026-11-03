using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GrawiaaApp.API.Migrations
{
    /// <inheritdoc />
    public partial class init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    LastActiveAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUsers_AspNetUsers_ParentId",
                        column: x => x.ParentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Badges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IconUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PersonalitySide = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    AcadPct = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    SocPct = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    MentPct = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    PhysPct = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    SpirPct = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    EnvPct = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CreaPct = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    ResPct = table.Column<decimal>(type: "decimal(5,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Badges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Characters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Avatar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Personality = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Hobbies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hobbies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeafyTips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Side = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "tip"),
                    Tip = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Alert = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Motivation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Content = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeafyTips", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Levels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LevelNumber = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequiredBadges = table.Column<int>(type: "int", nullable: false),
                    MinTrustScore = table.Column<double>(type: "float", nullable: false),
                    MaxAnomalyScore = table.Column<int>(type: "int", nullable: false),
                    Emoji = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Quote = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CardBgColor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CardTitleColor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BarColor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExpReward = table.Column<int>(type: "int", nullable: false),
                    LevelUpBadgeId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Levels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogFormItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogFormItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PersonalityClusters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClusterKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DominantTraits = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Characteristics = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecommendedFocus = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MemberCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastComputedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalityClusters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Category = table.Column<int>(type: "int", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdminProfiles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_AdminProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChildTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Points = table.Column<int>(type: "int", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    IsCompletedToday = table.Column<bool>(type: "bit", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChildId = table.Column<int>(type: "int", nullable: false),
                    HashedChildId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    IsParentAssigned = table.Column<bool>(type: "bit", nullable: false),
                    IsFixedTask = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChildTasks_AspNetUsers_ChildId",
                        column: x => x.ChildId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DailyLogEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChildId = table.Column<int>(type: "int", nullable: false),
                    LogDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LogDuration = table.Column<int>(type: "int", nullable: false),
                    StudyingPct = table.Column<double>(type: "float", nullable: false),
                    MoodEmoji = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SpiritualPct = table.Column<double>(type: "float", nullable: false),
                    MovingPct = table.Column<double>(type: "float", nullable: false),
                    FoodPct = table.Column<double>(type: "float", nullable: false),
                    ChorsPct = table.Column<double>(type: "float", nullable: false),
                    HobbiesPct = table.Column<double>(type: "float", nullable: false),
                    SocialPct = table.Column<double>(type: "float", nullable: false),
                    DailySpiritualConnection_Pct = table.Column<double>(type: "float", nullable: false),
                    DailySocialInteraction_Pct = table.Column<double>(type: "float", nullable: false),
                    DailyAcademicProgress_Pct = table.Column<double>(type: "float", nullable: false),
                    DailyHomeResponsibility_Pct = table.Column<double>(type: "float", nullable: false),
                    DailyPhysicalHealth_Pct = table.Column<double>(type: "float", nullable: false),
                    DailyPhysicalActivity_Pct = table.Column<double>(type: "float", nullable: false),
                    DailyCreativityEngagement_Pct = table.Column<double>(type: "float", nullable: false),
                    DailyMentalWellbeing_Pct = table.Column<double>(type: "float", nullable: false),
                    PillarsCalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GeneralNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FeelingNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MessageToParent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SpiritualActivities = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompletedSubjects = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LearningNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhysicalActivity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActivityNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NutritionItems = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WaterGlasses = table.Column<int>(type: "int", nullable: false),
                    FoodNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HelpedFamily = table.Column<bool>(type: "bit", nullable: false),
                    CompletedChores = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FunActivities = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SocialTimeWith = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SocialInteraction = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChallengeHandling = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SocialQualityScore = table.Column<double>(type: "float", nullable: false),
                    MoodLevel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MoodReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReflectionText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SleepHours = table.Column<int>(type: "int", nullable: false),
                    SleepNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WasIllToday = table.Column<bool>(type: "bit", nullable: false),
                    IllnessType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IllnessNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IllnessAvoidanceTip = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DetectedEmotion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SentimentScore = table.Column<double>(type: "float", nullable: true),
                    AnomalyScore = table.Column<int>(type: "int", nullable: false),
                    NlpAnomalyScore = table.Column<int>(type: "int", nullable: false),
                    AnomalyFlag = table.Column<bool>(type: "bit", nullable: false),
                    IsHonest = table.Column<bool>(type: "bit", nullable: false),
                    Alert = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmotionSafetyAlertFlag = table.Column<bool>(type: "bit", nullable: false),
                    EmotionSafetyAlertText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScoreVariance = table.Column<double>(type: "float", nullable: false),
                    NlpSpiritualConnection_Pct = table.Column<double>(type: "float", nullable: false),
                    NlpSocialInteraction_Pct = table.Column<double>(type: "float", nullable: false),
                    NlpAcademicProgress_Pct = table.Column<double>(type: "float", nullable: false),
                    NlpHomeResponsibility_Pct = table.Column<double>(type: "float", nullable: false),
                    NlpPhysicalHealth_Pct = table.Column<double>(type: "float", nullable: false),
                    NlpPhysicalActivity_Pct = table.Column<double>(type: "float", nullable: false),
                    NlpCreativityEngagement_Pct = table.Column<double>(type: "float", nullable: false),
                    NlpMentalWellbeing_Pct = table.Column<double>(type: "float", nullable: false),
                    NlpPillarsCalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeltaAcademic = table.Column<double>(type: "float", nullable: false),
                    DeltaEmotional = table.Column<double>(type: "float", nullable: false),
                    DeltaPhysical = table.Column<double>(type: "float", nullable: false),
                    DeltaReligious = table.Column<double>(type: "float", nullable: false),
                    DeltaSocial = table.Column<double>(type: "float", nullable: false),
                    DeltaResilience = table.Column<double>(type: "float", nullable: false),
                    ExpAwarded = table.Column<int>(type: "int", nullable: false),
                    TriggeredLevelUp = table.Column<bool>(type: "bit", nullable: false),
                    LevelAfter = table.Column<int>(type: "int", nullable: false),
                    HashedChildId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsJournalSaved = table.Column<bool>(type: "bit", nullable: false),
                    IsFinalized = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyLogEntries_AspNetUsers_ChildId",
                        column: x => x.ChildId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    IsUrgent = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceiverId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_AspNetUsers_ReceiverId",
                        column: x => x.ReceiverId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParentProfiles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_ParentProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rewards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Cost = table.Column<int>(type: "int", nullable: false),
                    IsRedeemed = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChildId = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    ParentApproved = table.Column<bool>(type: "bit", nullable: false),
                    BudgetLimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rewards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rewards_AspNetUsers_ChildId",
                        column: x => x.ChildId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrustScoreHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScoreAtTimestamp = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChildId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrustScoreHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrustScoreHistories_AspNetUsers_ChildId",
                        column: x => x.ChildId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BadgeChallenges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BadgeId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Emoji = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MetricType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MetricSubType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TargetValue = table.Column<double>(type: "float", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BadgeChallenges_Badges_BadgeId",
                        column: x => x.BadgeId,
                        principalTable: "Badges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChildBadges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChildId = table.Column<int>(type: "int", nullable: false),
                    BadgeId = table.Column<int>(type: "int", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildBadges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChildBadges_AspNetUsers_ChildId",
                        column: x => x.ChildId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChildBadges_Badges_BadgeId",
                        column: x => x.BadgeId,
                        principalTable: "Badges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChildHobbies",
                columns: table => new
                {
                    ChildId = table.Column<int>(type: "int", nullable: false),
                    HobbyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildHobbies", x => new { x.ChildId, x.HobbyId });
                    table.ForeignKey(
                        name: "FK_ChildHobbies_AspNetUsers_ChildId",
                        column: x => x.ChildId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChildHobbies_Hobbies_HobbyId",
                        column: x => x.HobbyId,
                        principalTable: "Hobbies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChildId = table.Column<int>(type: "int", nullable: false),
                    HashedChildId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LevelId = table.Column<int>(type: "int", nullable: false),
                    LevelNumber = table.Column<int>(type: "int", nullable: false),
                    CurrentExp = table.Column<int>(type: "int", nullable: false),
                    TotalExp = table.Column<int>(type: "int", nullable: false),
                    LastLevelUpAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLevels_AspNetUsers_ChildId",
                        column: x => x.ChildId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserLevels_Levels_LevelId",
                        column: x => x.LevelId,
                        principalTable: "Levels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParentFeedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DailyLogEntryId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentFeedbacks_DailyLogEntries_DailyLogEntryId",
                        column: x => x.DailyLogEntryId,
                        principalTable: "DailyLogEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChildProfiles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    IsStandalone = table.Column<bool>(type: "bit", nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    Age = table.Column<int>(type: "int", nullable: false),
                    CharacterName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CharacterType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastKnownIp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeviceFingerprint = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PasswordResetPending = table.Column<bool>(type: "bit", nullable: true),
                    ResetToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Level = table.Column<int>(type: "int", nullable: false),
                    CurrentExp = table.Column<int>(type: "int", nullable: false),
                    TrustScore = table.Column<int>(type: "int", nullable: false),
                    TrustScoreSetAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DailyManagementJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PastIllnessSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IllnessAvoidanceTip = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PendingParentEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PendingLinkToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    SpiritualConnection_Pct = table.Column<double>(type: "float", nullable: false),
                    SocialInteraction_Pct = table.Column<double>(type: "float", nullable: false),
                    AcademicProgress_Pct = table.Column<double>(type: "float", nullable: false),
                    HomeResponsibility_Pct = table.Column<double>(type: "float", nullable: false),
                    PhysicalHealth_Pct = table.Column<double>(type: "float", nullable: false),
                    PhysicalActivity_Pct = table.Column<double>(type: "float", nullable: false),
                    CreativityEngagement_Pct = table.Column<double>(type: "float", nullable: false),
                    MentalWellbeing_Pct = table.Column<double>(type: "float", nullable: false),
                    ResiliencePct = table.Column<double>(type: "float", nullable: false),
                    MeanScore = table.Column<double>(type: "float", nullable: false),
                    LastAggregatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AggregationWeekStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HasActiveEmotionSafetyAlert = table.Column<bool>(type: "bit", nullable: false),
                    LastEmotionSafetyAlertAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Tip = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Alert = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Motivation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KindnessTrait = table.Column<double>(type: "float", nullable: false),
                    VitalityTrait = table.Column<double>(type: "float", nullable: false),
                    CuriosityTrait = table.Column<double>(type: "float", nullable: false),
                    ResilienceTrait = table.Column<double>(type: "float", nullable: false),
                    SpiritualTrait = table.Column<double>(type: "float", nullable: false),
                    LastTraitSynthesisAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecommendedBadgeId = table.Column<int>(type: "int", nullable: true),
                    RecommendedBadgeSetAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecommendedLeafyTipId = table.Column<int>(type: "int", nullable: true),
                    RecommendedLeafyTipSetAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeafyTipText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LeafyMessageText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LeafyAlertText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LeafyTextSetAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPersonalityClusterId = table.Column<int>(type: "int", nullable: true),
                    PersonalityClusterAssignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NlpSpiritualConnection_Pct = table.Column<double>(type: "float", nullable: false),
                    NlpSocialInteraction_Pct = table.Column<double>(type: "float", nullable: false),
                    NlpAcademicProgress_Pct = table.Column<double>(type: "float", nullable: false),
                    NlpHomeResponsibility_Pct = table.Column<double>(type: "float", nullable: false),
                    NlpPhysicalHealth_Pct = table.Column<double>(type: "float", nullable: false),
                    NlpPhysicalActivity_Pct = table.Column<double>(type: "float", nullable: false),
                    NlpCreativityEngagement_Pct = table.Column<double>(type: "float", nullable: false),
                    NlpMentalWellbeing_Pct = table.Column<double>(type: "float", nullable: false),
                    FinalSpiritualConnection_Pct = table.Column<double>(type: "float", nullable: false),
                    FinalSocialInteraction_Pct = table.Column<double>(type: "float", nullable: false),
                    FinalAcademicProgress_Pct = table.Column<double>(type: "float", nullable: false),
                    FinalHomeResponsibility_Pct = table.Column<double>(type: "float", nullable: false),
                    FinalPhysicalHealth_Pct = table.Column<double>(type: "float", nullable: false),
                    FinalPhysicalActivity_Pct = table.Column<double>(type: "float", nullable: false),
                    FinalCreativityEngagement_Pct = table.Column<double>(type: "float", nullable: false),
                    FinalMentalWellbeing_Pct = table.Column<double>(type: "float", nullable: false),
                    RecommendedBadgeSynced = table.Column<bool>(type: "bit", nullable: false),
                    LeafySynced = table.Column<bool>(type: "bit", nullable: false),
                    FinalMeanScore = table.Column<double>(type: "float", nullable: false),
                    TotalTasksCount = table.Column<int>(type: "int", nullable: false),
                    CompletedTasksCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_ChildProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChildProfiles_Badges_RecommendedBadgeId",
                        column: x => x.RecommendedBadgeId,
                        principalTable: "Badges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChildProfiles_LeafyTips_RecommendedLeafyTipId",
                        column: x => x.RecommendedLeafyTipId,
                        principalTable: "LeafyTips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChildProfiles_ParentProfiles_ParentId",
                        column: x => x.ParentId,
                        principalTable: "ParentProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChildProfiles_PersonalityClusters_LastPersonalityClusterId",
                        column: x => x.LastPersonalityClusterId,
                        principalTable: "PersonalityClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ChildChallengeProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChildId = table.Column<int>(type: "int", nullable: false),
                    HashedChildId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ChallengeId = table.Column<int>(type: "int", nullable: false),
                    CurrentValue = table.Column<double>(type: "float", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildChallengeProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChildChallengeProgresses_AspNetUsers_ChildId",
                        column: x => x.ChildId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChildChallengeProgresses_BadgeChallenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "BadgeChallenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdminChildrenPersonalityClusters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChildId = table.Column<int>(type: "int", nullable: false),
                    HashedChildId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PersonalityClusterId = table.Column<int>(type: "int", nullable: false),
                    SimilarityScore = table.Column<double>(type: "float", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    ClusteringRunId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminChildrenPersonalityClusters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminChildrenPersonalityClusters_ChildProfiles_ChildId",
                        column: x => x.ChildId,
                        principalTable: "ChildProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdminChildrenPersonalityClusters_PersonalityClusters_PersonalityClusterId",
                        column: x => x.PersonalityClusterId,
                        principalTable: "PersonalityClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiBadgeSuggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChildId = table.Column<int>(type: "int", nullable: false),
                    Pillar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BadgeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BadgeIcon = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Urgency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentPct = table.Column<double>(type: "float", nullable: false),
                    TargetPct = table.Column<double>(type: "float", nullable: false),
                    IsShown = table.Column<bool>(type: "bit", nullable: false),
                    IsDismissed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiBadgeSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiBadgeSuggestions_ChildProfiles_ChildId",
                        column: x => x.ChildId,
                        principalTable: "ChildProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { 1, null, "Parent", "PARENT" },
                    { 2, null, "Child", "CHILD" },
                    { 3, null, "Admin", "ADMIN" }
                });

            migrationBuilder.InsertData(
                table: "Badges",
                columns: new[] { "Id", "AcadPct", "Category", "CreaPct", "Description", "EnvPct", "IconUrl", "MentPct", "Name", "PersonalitySide", "PhysPct", "ResPct", "SocPct", "SpirPct", "UnlockedAt" },
                values: new object[,]
                {
                    { 1, 0m, 0, 0m, "Wrote your very first journal entry!", 0m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#F59E0B\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#FDE68A\"/><path d=\"M11 11h4.5v10H11zm1 1v8h2.5v-8z\" fill=\"#92400E\"/><path d=\"M16.5 11H21v10h-4.5zm1 1v8h2.5v-8z\" fill=\"#92400E\"/><path d=\"M11 21h10v1H11z\" fill=\"#92400E\"/></svg>", 0m, "First Journal", "Right", 0m, 0m, 0m, 0m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6323) },
                    { 2, 0m, 1, 0m, "Logged 3 days in a row — great habit!", 0m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#EF4444\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#FEE2E2\"/><path d=\"M16 8c0 0-4 4-4 8a4 4 0 008 0c0-2-1-3.5-1-3.5s-.5 1.5-1.5 2c0-3-1.5-6.5-1.5-6.5z\" fill=\"#EF4444\"/><path d=\"M15 16c0 0-1.5 1-1.5 2.5a2.5 2.5 0 005 0c0-1-.5-1.5-.5-1.5s-.5 1-1.5 1c0-1-.5-2-.5-2z\" fill=\"#FCA5A5\"/></svg>", 0m, "3-Day Streak", "Left", 0m, 0m, 0m, 0m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6333) },
                    { 3, 0m, 2, 0m, "Logged 7 days in a row — you're on fire!", 0m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#DC2626\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#FEE2E2\"/><path d=\"M18 9l-5 8h4l-3 6 7-9h-4z\" fill=\"#F59E0B\" stroke=\"#DC2626\" stroke-width=\".5\"/></svg>", 0m, "Flame Hero", "Left", 0m, 0m, 0m, 0m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6335) },
                    { 4, 0m, 3, 0m, "Logged 30 days in a row — incredible!", 0m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#991B1B\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#FEE2E2\"/><path d=\"M10 20l2-6 4 3 4-3 2 6H10z\" fill=\"#F59E0B\"/><circle cx=\"10\" cy=\"14\" r=\"1.5\" fill=\"#F59E0B\"/><circle cx=\"16\" cy=\"11\" r=\"1.5\" fill=\"#F59E0B\"/><circle cx=\"22\" cy=\"14\" r=\"1.5\" fill=\"#F59E0B\"/><rect x=\"10\" y=\"20\" width=\"12\" height=\"2\" rx=\"1\" fill=\"#B45309\"/></svg>", 0m, "Legend Streaker", "Left", 0m, 0m, 0m, 0m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6337) },
                    { 5, 0m, 4, 0m, "Completed 5 tasks — a great beginning!", 0m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#0D9488\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#CCFBF1\"/><path d=\"M11 16l3.5 3.5L21 12\" stroke=\"#0D9488\" stroke-width=\"2.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\" fill=\"none\"/></svg>", 0m, "Task Starter", "Right", 0m, 0m, 0m, 0m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6339) },
                    { 6, 0m, 5, 0m, "Completed 20 tasks — super achiever!", 0m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#0F766E\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#CCFBF1\"/><circle cx=\"16\" cy=\"16\" r=\"7\" fill=\"none\" stroke=\"#0F766E\" stroke-width=\"1.5\"/><circle cx=\"16\" cy=\"16\" r=\"4\" fill=\"none\" stroke=\"#0F766E\" stroke-width=\"1.5\"/><circle cx=\"16\" cy=\"16\" r=\"2\" fill=\"#0F766E\"/><path d=\"M22 10l-2 4.5\" stroke=\"#EF4444\" stroke-width=\"1.5\" stroke-linecap=\"round\"/><circle cx=\"22\" cy=\"10\" r=\"1\" fill=\"#EF4444\"/></svg>", 0m, "Task Achiever", "Right", 0m, 0m, 0m, 0m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6341) },
                    { 7, 0m, 6, 0m, "Completed 50 tasks — absolute champion!", 0m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#115E59\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#CCFBF1\"/><path d=\"M13 10h6v6a3 3 0 01-6 0V10z\" fill=\"#F59E0B\"/><path d=\"M10 11h3v3a1.5 1.5 0 01-3 0V11z\" fill=\"#F59E0B\"/><path d=\"M19 11h3v3a1.5 1.5 0 01-3 0V11z\" fill=\"#F59E0B\"/><rect x=\"15\" y=\"16\" width=\"2\" height=\"3\" fill=\"#B45309\"/><rect x=\"13\" y=\"19\" width=\"6\" height=\"2\" rx=\"1\" fill=\"#B45309\"/></svg>", 0m, "Task Champion", "Right", 0m, 0m, 0m, 0m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6342) },
                    { 8, 0m, 7, 0m, "Started the journey — Level 1 reached!", 0m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#16A34A\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#DCFCE7\"/><path d=\"M16 22V14\" stroke=\"#16A34A\" stroke-width=\"1.5\" stroke-linecap=\"round\"/><path d=\"M16 17c0-3 3-5 3-5s0 3-3 5z\" fill=\"#16A34A\"/><path d=\"M16 15c0-2-3-4-3-4s0 3 3 4z\" fill=\"#4ADE80\"/></svg>", 0m, "Sprout Scout", "Right", 0m, 0m, 0m, 0m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6344) },
                    { 9, 0m, 8, 0m, "Reached Level 5 — kindness is blooming!", 0m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#DB2777\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#FCE7F3\"/><circle cx=\"16\" cy=\"16\" r=\"3\" fill=\"#F59E0B\"/><ellipse cx=\"16\" cy=\"11\" rx=\"2\" ry=\"3\" fill=\"#F9A8D4\"/><ellipse cx=\"16\" cy=\"21\" rx=\"2\" ry=\"3\" fill=\"#F9A8D4\"/><ellipse cx=\"11\" cy=\"16\" rx=\"3\" ry=\"2\" fill=\"#F9A8D4\"/><ellipse cx=\"21\" cy=\"16\" rx=\"3\" ry=\"2\" fill=\"#F9A8D4\"/></svg>", 0m, "Blossom Buddy", "Right", 0m, 0m, 0m, 0m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6346) },
                    { 10, 0m, 9, 0m, "Reached Level 9 — brain power unlocked!", 0m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#4338CA\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#EEF2FF\"/><path d=\"M13 11c0 0 0-2 3-2s3 2 3 2-1 2-3 3c-2-1-3-3-3-3z\" fill=\"#F59E0B\"/><rect x=\"15\" y=\"14\" width=\"2\" height=\"3\" fill=\"#6366F1\"/><rect x=\"13\" y=\"17\" width=\"6\" height=\"2\" rx=\"1\" fill=\"#4338CA\"/><circle cx=\"20\" cy=\"10\" r=\"1\" fill=\"#F59E0B\"/></svg>", 0m, "Curiosity Captain", "Left", 0m, 0m, 0m, 0m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6348) },
                    { 11, 0m, 10, 0m, "Reached Level 13 — true hero strength!", 0m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#1D4ED8\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#EFF6FF\"/><path d=\"M16 10l-5 2v4c0 3 2 5 5 6 3-1 5-3 5-6v-4l-5-2z\" fill=\"#3B82F6\"/><path d=\"M13.5 16l2 2 3-3\" stroke=\"white\" stroke-width=\"1.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\" fill=\"none\"/></svg>", 0m, "Resilience Ranger", "Left", 0m, 0m, 0m, 0m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6350) },
                    { 12, 0m, 11, 0m, "Reached Level 17 — the ultimate champion!", 0m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#B45309\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#FEF3C7\"/><path d=\"M16 10l1.5 4h4.5l-3.5 2.5 1.5 4.5L16 19l-4 2 1.5-4.5L10 14h4.5z\" fill=\"#F59E0B\" stroke=\"#B45309\" stroke-width=\".5\"/></svg>", 0m, "Master Guardian", "Right", 0m, 0m, 0m, 0m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6351) },
                    { 13, 0m, 12, 0m, "Reached Level 20 — you ARE a legend!", 0m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#78350F\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#FEF3C7\"/><path d=\"M16 9l1.2 3.5h3.8l-3 2.2 1.2 3.5L16 16.2l-3.2 2 1.2-3.5-3-2.2h3.8z\" fill=\"#FBBF24\"/><circle cx=\"16\" cy=\"9\" r=\"1\" fill=\"#F59E0B\"/><circle cx=\"23\" cy=\"14\" r=\"1\" fill=\"#F59E0B\"/><circle cx=\"20\" cy=\"22\" r=\"1\" fill=\"#F59E0B\"/><circle cx=\"12\" cy=\"22\" r=\"1\" fill=\"#F59E0B\"/><circle cx=\"9\" cy=\"14\" r=\"1\" fill=\"#F59E0B\"/></svg>", 0m, "Grawiaa Legend", "Right", 0m, 0m, 0m, 0m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6353) },
                    { 28, 0.1m, 15, 0.1m, "Start your day bright with a quiet moment of connection.", 0.1m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#0F766E\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#CCFBF1\"/><path d=\"M18 11a6 6 0 100 10 5 5 0 010-10z\" fill=\"#0F766E\"/><circle cx=\"21\" cy=\"11\" r=\"1.5\" fill=\"#F59E0B\"/></svg>", 0.7m, "Morning Prayer", "Right", 0.1m, 0.5m, 0.1m, 1.0m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6355) },
                    { 29, 0.3m, 16, 0.5m, "Celebrate the beautiful little blessings all around you.", 0.1m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#0D9488\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#CCFBF1\"/><path d=\"M16 20c0 0-5-3.5-5-6.5a2.5 2.5 0 015 0 2.5 2.5 0 015 0c0 3-5 6.5-5 6.5z\" fill=\"#EF4444\"/><path d=\"M16 9v2M21 11l-1.5 1.5M23 16h-2M11 11l1.5 1.5M9 16h2\" stroke=\"#F59E0B\" stroke-width=\"1.5\" stroke-linecap=\"round\"/></svg>", 1.0m, "Daily Gratitude", "Right", 0.0m, 0.9m, 0.2m, 0.6m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6363) },
                    { 30, 1.0m, 21, 0.4m, "Power up your brain by conquering tricky numbers!", 0.1m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#2563EB\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#EFF6FF\"/><text x=\"16\" y=\"21\" text-anchor=\"middle\" font-size=\"14\" font-weight=\"bold\" fill=\"#1D4ED8\" font-family=\"serif\">&#x3A3;</text></svg>", 0.8m, "Math Marathon", "Left", 0.1m, 0.6m, 0.2m, 0.1m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6368) },
                    { 31, 0.7m, 39, 1.0m, "Let your wonderful imagination paint beautiful pictures.", 0.1m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#7C3AED\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#EDE9FE\"/><rect x=\"11\" y=\"12\" width=\"4\" height=\"8\" rx=\"1\" fill=\"#7C3AED\"/><rect x=\"17\" y=\"12\" width=\"4\" height=\"8\" rx=\"1\" fill=\"#8B5CF6\"/><path d=\"M11 12h10v1H11z\" fill=\"#4C1D95\"/><circle cx=\"21\" cy=\"10\" r=\"1.5\" fill=\"#F59E0B\"/></svg>", 0.6m, "Storyteller", "Left", 0.0m, 0.4m, 0.5m, 0.2m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6372) },
                    { 32, 0.9m, 22, 0.8m, "Explore how the world works like a real scientist.", 0.6m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#0369A1\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#E0F2FE\"/><circle cx=\"16\" cy=\"16\" r=\"2.5\" fill=\"#0369A1\"/><ellipse cx=\"16\" cy=\"16\" rx=\"7\" ry=\"3.5\" fill=\"none\" stroke=\"#0369A1\" stroke-width=\"1.2\"/><ellipse cx=\"16\" cy=\"16\" rx=\"7\" ry=\"3.5\" fill=\"none\" stroke=\"#0369A1\" stroke-width=\"1.2\" transform=\"rotate(60 16 16)\"/><ellipse cx=\"16\" cy=\"16\" rx=\"7\" ry=\"3.5\" fill=\"none\" stroke=\"#0369A1\" stroke-width=\"1.2\" transform=\"rotate(120 16 16)\"/></svg>", 0.7m, "Science Lab", "Left", 0.2m, 0.4m, 0.3m, 0.1m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6382) },
                    { 33, 0.1m, 30, 0.1m, "Move your body and shine brightly alongside your teammates.", 0.2m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#15803D\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#DCFCE7\"/><circle cx=\"16\" cy=\"16\" r=\"6\" fill=\"white\" stroke=\"#15803D\" stroke-width=\"1\"/><polygon points=\"16,11 17.5,14.5 21,14.5 18.5,16.5 19.5,20 16,17.5 12.5,20 13.5,16.5 11,14.5 14.5,14.5\" fill=\"#15803D\" opacity=\".4\"/><circle cx=\"16\" cy=\"16\" r=\"2\" fill=\"#15803D\"/></svg>", 0.4m, "Team Sports", "Right", 1.0m, 0.8m, 1.0m, 0.1m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6387) },
                    { 34, 0.2m, 18, 0.3m, "Spread warmth and joy to the people around you.", 0.2m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#D97706\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#FEF3C7\"/><path d=\"M16 21c0 0-6-4-6-7.5a3 3 0 016 0 3 3 0 016 0c0 3.5-6 7.5-6 7.5z\" fill=\"#EF4444\"/><circle cx=\"16\" cy=\"12.5\" r=\"2\" fill=\"white\" opacity=\".8\"/></svg>", 0.7m, "Kindness Mission", "Right", 0.1m, 0.5m, 1.0m, 0.8m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6391) },
                    { 35, 0.6m, 19, 0.4m, "Be brave and share your voice clearly with the world.", 0.1m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#4338CA\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#EEF2FF\"/><rect x=\"14\" y=\"9\" width=\"4\" height=\"7\" rx=\"2\" fill=\"#4338CA\"/><path d=\"M12 15a4 4 0 008 0\" fill=\"none\" stroke=\"#4338CA\" stroke-width=\"1.5\"/><line x1=\"16\" y1=\"19\" x2=\"16\" y2=\"22\" stroke=\"#4338CA\" stroke-width=\"1.5\"/><line x1=\"13\" y1=\"22\" x2=\"19\" y2=\"22\" stroke=\"#4338CA\" stroke-width=\"1.5\" stroke-linecap=\"round\"/></svg>", 0.8m, "Public Speaking", "Left", 0.2m, 0.9m, 0.9m, 0.2m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6395) },
                    { 36, 0.4m, 24, 0.2m, "Discover and protect the beautiful outdoors around you.", 1.0m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#166534\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#DCFCE7\"/><path d=\"M16 22c0 0-6-4-6-8a6 6 0 0112 0c0 4-6 8-6 8z\" fill=\"#16A34A\"/><path d=\"M16 22V14\" stroke=\"#DCFCE7\" stroke-width=\"1\" stroke-linecap=\"round\"/><path d=\"M16 18l3-3M16 16l-2-2\" stroke=\"#DCFCE7\" stroke-width=\"1\" stroke-linecap=\"round\"/></svg>", 0.5m, "Nature Guide", "Right", 0.6m, 0.4m, 0.3m, 0.4m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6399) },
                    { 37, 0.3m, 25, 0.2m, "Take care of the earth by keeping your surroundings clean.", 1.0m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#0F766E\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#CCFBF1\"/><path d=\"M16 10l2.5 4H19L16 19l-3-5h.5L16 10z\" fill=\"#0F766E\"/><path d=\"M22 17.5l-3.5-1.5 1 2.5-2 1.5 5 .5-1.5-3z\" fill=\"#0F766E\"/><path d=\"M10 17.5l3.5-1.5-1 2.5 2 1.5-5 .5 1.5-3z\" fill=\"#0F766E\"/></svg>", 0.4m, "Recycling Hero", "Right", 0.3m, 0.5m, 0.5m, 0.2m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6402) },
                    { 38, 0.8m, 36, 0.6m, "Stay strong and find creative answers when things get tough.", 0.1m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#6D28D9\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#EDE9FE\"/><path d=\"M12 12h4c0-1 .5-2 1.5-2s1.5 1 1.5 2h1v4c1 0 2 .5 2 1.5s-1 1.5-2 1.5v1h-4c0 1-.5 2-1.5 2s-1.5-1-1.5-2h-1v-4c-1 0-2-.5-2-1.5s1-1.5 2-1.5V12z\" fill=\"#7C3AED\"/></svg>", 0.9m, "Problem Solver", "Left", 0.1m, 1.0m, 0.4m, 0.1m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6408) },
                    { 39, 0.2m, 33, 0.9m, "Let your inner creativity bloom with beautiful colors.", 0.8m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#A21CAF\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#FAE8FF\"/><circle cx=\"16\" cy=\"16\" r=\"5\" fill=\"white\" stroke=\"#A21CAF\" stroke-width=\"1\"/><circle cx=\"13\" cy=\"13.5\" r=\"1.5\" fill=\"#EF4444\"/><circle cx=\"19\" cy=\"13.5\" r=\"1.5\" fill=\"#3B82F6\"/><circle cx=\"21\" cy=\"17\" r=\"1.5\" fill=\"#16A34A\"/><circle cx=\"11\" cy=\"17\" r=\"1.5\" fill=\"#F59E0B\"/><circle cx=\"16\" cy=\"21\" r=\"1.5\" fill=\"#7C3AED\"/></svg>", 0.6m, "Artistic Garden", "Left", 0.4m, 0.3m, 0.2m, 0.3m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6411) },
                    { 40, 0.1m, 27, 0.2m, "Be a shining light in your neighborhood and household.", 0.6m, "<svg viewBox=\"0 0 32 32\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"16\" cy=\"16\" r=\"14\" fill=\"#C2410C\"/><circle cx=\"16\" cy=\"16\" r=\"11\" fill=\"#FFF7ED\"/><path d=\"M16 10l7 6h-2v6H11v-6H9z\" fill=\"#C2410C\"/><path d=\"M14 22v-4h4v4\" fill=\"#FFF7ED\"/><path d=\"M15 16c0 0-2-1.5-2-2.5a1.5 1.5 0 013 0 1.5 1.5 0 013 0c0 1-2 2.5-2 2.5z\" fill=\"#EF4444\"/></svg>", 0.6m, "Community Helper", "Right", 0.5m, 0.7m, 0.9m, 0.7m, new DateTime(2026, 6, 24, 14, 22, 41, 918, DateTimeKind.Utc).AddTicks(6415) }
                });

            migrationBuilder.InsertData(
                table: "Hobbies",
                columns: new[] { "Id", "Category", "Icon", "Name" },
                values: new object[,]
                {
                    { 1, 6, "🎨", "رسم" },
                    { 2, 5, "⚽", "رياضة" },
                    { 3, 2, "📚", "قراءة" }
                });

            migrationBuilder.InsertData(
                table: "LeafyTips",
                columns: new[] { "Id", "Alert", "Content", "Motivation", "Purpose", "Side", "Tip", "Type" },
                values: new object[,]
                {
                    { 101, null, "Try praying Fajr on time tomorrow — even 5 minutes of Quran at dawn will uplift your spirit! 🕌", null, "tip", "spiritual", "Try praying Fajr on time tomorrow — even 5 minutes of Quran at dawn will uplift your spirit! 🕌", "encouragement" },
                    { 102, "⚠️ Your child's spiritual connection score has dropped below 20% this week. Consider scheduling family Quran time.", "⚠️ Your child's spiritual connection score has dropped below 20% this week. Consider scheduling family Quran time.", null, "alert", "spiritual", null, "warning" },
                    { 103, null, "🌟 MashaAllah! You completed all your prayers AND made Athkar today — you are a true spiritual hero!", "🌟 MashaAllah! You completed all your prayers AND made Athkar today — you are a true spiritual hero!", "motivation", "spiritual", null, "achievement" },
                    { 201, null, "Smile at a friend tomorrow and say something kind — small acts of kindness make you a social superstar! 🤝", null, "tip", "social", "Smile at a friend tomorrow and say something kind — small acts of kindness make you a social superstar! 🤝", "encouragement" },
                    { 202, "⚠️ Your child seems to be spending very little time with others this week. Check in about their friendships.", "⚠️ Your child seems to be spending very little time with others this week. Check in about their friendships.", null, "alert", "social", null, "warning" },
                    { 203, null, "🎉 Wow! You showed up for your friends and family so well this week — your Kindness trait is soaring!", "🎉 Wow! You showed up for your friends and family so well this week — your Kindness trait is soaring!", "motivation", "social", null, "achievement" },
                    { 301, null, "Try the Pomodoro method: 25 minutes of focused study, then a 5-minute break. Your Curiosity trait will thank you! 📚", null, "tip", "academic", "Try the Pomodoro method: 25 minutes of focused study, then a 5-minute break. Your Curiosity trait will thank you! 📚", "encouragement" },
                    { 302, "⚠️ Homework & Study scores are critically low this week. Your child may need extra support or motivation.", "⚠️ Homework & Study scores are critically low this week. Your child may need extra support or motivation.", null, "alert", "academic", null, "warning" },
                    { 303, null, "📖 You crushed your homework this week! Your Curiosity trait has grown strong — keep exploring!", "📖 You crushed your homework this week! Your Curiosity trait has grown strong — keep exploring!", "motivation", "academic", null, "achievement" },
                    { 401, null, "Help set the table tonight or fold some laundry — small acts of helping build your Kindness superpower! 🏠", null, "tip", "environment", "Help set the table tonight or fold some laundry — small acts of helping build your Kindness superpower! 🏠", "encouragement" },
                    { 402, "⚠️ Your child's home responsibility score is very low. Gentle encouragement around household participation may help.", "⚠️ Your child's home responsibility score is very low. Gentle encouragement around household participation may help.", null, "alert", "environment", null, "warning" },
                    { 403, null, "🏆 You helped your family so much this week! Your Kindness and Resilience traits are both glowing!", "🏆 You helped your family so much this week! Your Kindness and Resilience traits are both glowing!", "motivation", "environment", null, "achievement" },
                    { 501, null, "Swap one snack for a fruit today — your body and your Vitality trait will both feel the boost! 🍎", null, "tip", "physical", "Swap one snack for a fruit today — your body and your Vitality trait will both feel the boost! 🍎", "encouragement" },
                    { 502, "⚠️ Nutrition & Food scores are declining. Your child may be eating irregularly — check meals and hydration.", "⚠️ Nutrition & Food scores are declining. Your child may be eating irregularly — check meals and hydration.", null, "alert", "physical", null, "warning" },
                    { 503, null, "🥗 Amazing nutrition choices this week! Your Vitality trait is powering up — you're a food champion!", "🥗 Amazing nutrition choices this week! Your Vitality trait is powering up — you're a food champion!", "motivation", "physical", null, "achievement" },
                    { 601, null, "Take a 15-minute walk or play your favourite sport today — moving your body charges up your Vitality! ⚽", null, "tip", "sport", "Take a 15-minute walk or play your favourite sport today — moving your body charges up your Vitality! ⚽", "encouragement" },
                    { 602, "⚠️ Physical activity has been very low this week. Encourage outdoor play or a short walk after school.", "⚠️ Physical activity has been very low this week. Encourage outdoor play or a short walk after school.", null, "alert", "sport", null, "warning" },
                    { 603, null, "🏃 You moved your body every day this week — incredible! Your Vitality and Resilience traits are at full power!", "🏃 You moved your body every day this week — incredible! Your Vitality and Resilience traits are at full power!", "motivation", "sport", null, "achievement" },
                    { 701, null, "Spend 20 minutes on a hobby today — drawing, building, or crafting. Your Curiosity trait grows every time you create! 🎨", null, "tip", "creativity", "Spend 20 minutes on a hobby today — drawing, building, or crafting. Your Curiosity trait grows every time you create! 🎨", "encouragement" },
                    { 702, "⚠️ Creative engagement is very low this week. Your child may benefit from screen-free creative activities.", "⚠️ Creative engagement is very low this week. Your child may benefit from screen-free creative activities.", null, "alert", "creativity", null, "warning" },
                    { 703, null, "🎨 You explored so many creative hobbies this week! Your Curiosity trait is bursting with colour!", "🎨 You explored so many creative hobbies this week! Your Curiosity trait is bursting with colour!", "motivation", "creativity", null, "achievement" },
                    { 801, null, "It's okay to feel sad or tired sometimes. Write down one thing you're grateful for today — it helps your Resilience grow! 💗", null, "tip", "mental", "It's okay to feel sad or tired sometimes. Write down one thing you're grateful for today — it helps your Resilience grow! 💗", "encouragement" },
                    { 802, "⚠️ Your child's mood & mental health signals are concerning this week. Multiple negative emotions detected — a check-in conversation is recommended.", "⚠️ Your child's mood & mental health signals are concerning this week. Multiple negative emotions detected — a check-in conversation is recommended.", null, "alert", "mental", null, "warning" },
                    { 803, null, "💪 You handled your emotions so well this week — even the hard days! Your Resilience trait is incredibly strong!", "💪 You handled your emotions so well this week — even the hard days! Your Resilience trait is incredibly strong!", "motivation", "mental", null, "achievement" }
                });

            migrationBuilder.InsertData(
                table: "Levels",
                columns: new[] { "Id", "BarColor", "CardBgColor", "CardTitleColor", "Emoji", "ExpReward", "LevelNumber", "LevelUpBadgeId", "MaxAnomalyScore", "MinTrustScore", "Name", "Quote", "RequiredBadges", "Title" },
                values: new object[,]
                {
                    { 1, "linear-gradient(90deg,#a8e063,#56ab2f)", "#fffbe6", "#e8a800", "🌱", 0, 1, null, 100, 0.0, "The Sprout", "Every journey starts with a single step!", 0, "Sprout" },
                    { 2, "linear-gradient(90deg,#4DB6AC,#26a69a)", "#e4f6ff", "#4a8e9a", "🌿", 50, 2, 9, 35, 0.40000000000000002, "The Seedling", "You were so resilient this week!", 3, "Seedling" },
                    { 3, "linear-gradient(90deg,#FFD97D,#ffa040)", "#ffe4ec", "#e6536f", "🌸", 100, 3, 10, 30, 0.59999999999999998, "The Sapling", "Your kindness made the garden grow!", 6, "Sapling" },
                    { 4, "linear-gradient(90deg,#FFD97D,#ff9800)", "#fffef0", "#d0900a", "🌳", 150, 4, 11, 25, 0.75, "The Branch", "You shine brighter every day!", 9, "Branch" },
                    { 5, "linear-gradient(90deg,#9575CD,#7c4dff)", "#f5f0ff", "#7c4dff", "🌲", 200, 5, 12, 20, 0.90000000000000002, "The Mighty Tree", "A true champion never stops growing!", 12, "Mighty Tree" }
                });

            migrationBuilder.InsertData(
                table: "Subjects",
                columns: new[] { "Id", "Category", "Icon", "Name" },
                values: new object[,]
                {
                    { 1, 2, "🔢", "رياضيات" },
                    { 2, 0, "🕌", "قرآن" }
                });

            migrationBuilder.InsertData(
                table: "BadgeChallenges",
                columns: new[] { "Id", "BadgeId", "Description", "Emoji", "MetricSubType", "MetricType", "SortOrder", "TargetValue" },
                values: new object[,]
                {
                    { 1, 8, "Complete your first journal entry", "📖", null, "total_logs", 0, 1.0 },
                    { 2, 8, "Set your hero name", "🦸", null, "has_hero_name", 1, 1.0 },
                    { 3, 8, "Link to a parent", "👨‍👩‍👧", null, "has_parent_link", 2, 1.0 },
                    { 4, 9, "Log 5 days in a row", "🔥", null, "streak", 0, 5.0 },
                    { 5, 9, "Pray all 5 prayers in one day", "🕌", null, "prayer_day", 1, 1.0 },
                    { 6, 9, "Try a new hobby", "🎨", null, "has_hobby", 2, 1.0 },
                    { 7, 10, "Help family 3 times", "🏠", null, "helped_family", 0, 3.0 },
                    { 8, 10, "Share a kind word", "💬", null, "kind_word", 1, 1.0 },
                    { 9, 10, "Read Quran for 7 days", "📖", null, "quran_days", 2, 7.0 },
                    { 10, 11, "Exercise 5 days", "⚽", null, "movement_days", 0, 5.0 },
                    { 11, 11, "Complete all subjects (1+ day)", "📚", null, "subjects_day", 1, 1.0 },
                    { 12, 11, "Drink 6 glasses of water (1 day)", "💧", null, "water_six", 2, 1.0 },
                    { 13, 12, "30-day streak", "🔥", null, "streak", 0, 30.0 },
                    { 14, 12, "Earn all movement badges", "🏅", null, "movement_badges", 1, 1.0 },
                    { 15, 12, "Master 3 hobbies", "🎯", null, "hobby_count", 2, 3.0 },
                    { 16, 13, "Complete all Champion challenges", "🌟", null, "total_logs", 0, 30.0 },
                    { 17, 2, "Log 3 days in a row", "🔥", null, "streak", 0, 3.0 },
                    { 18, 3, "Log 7 days in a row", "⚡", null, "streak", 0, 7.0 },
                    { 19, 4, "Log 30 days in a row", "🏅", null, "streak", 0, 30.0 },
                    { 20, 5, "Complete 5 tasks", "✅", null, "tasks_completed", 0, 5.0 },
                    { 21, 6, "Complete 20 tasks", "🎯", null, "tasks_completed", 0, 20.0 },
                    { 22, 7, "Complete 50 tasks", "🏆", null, "tasks_completed", 0, 50.0 },
                    { 23, 1, "Write your first journal entry", "📖", null, "total_logs", 0, 1.0 },
                    { 24, 28, "Journal about praying Fajr on time", "🕋", null, "journal_text", 0, 1.0 },
                    { 25, 28, "Mention saying your morning Duas", "🤲", null, "journal_text", 1, 1.0 },
                    { 26, 29, "Write down 3 things you are thankful for today", "🙏", null, "journal_text", 0, 1.0 },
                    { 27, 29, "Share something that made you smile", "😊", null, "journal_text", 1, 1.0 },
                    { 28, 30, "Write about solving a tough math problem", "🔢", null, "journal_text", 0, 1.0 },
                    { 29, 30, "Mention practicing your math skills", "📐", null, "journal_text", 1, 1.0 },
                    { 30, 31, "Write a short creative story or poem in your journal", "📝", null, "journal_text", 0, 1.0 },
                    { 31, 31, "Tell a summary of a book you read today", "📖", null, "journal_text", 1, 1.0 },
                    { 32, 32, "Journal about a cool science experiment or fact you learned", "🔬", null, "journal_text", 0, 1.0 },
                    { 33, 32, "Write about exploring nature or space concepts", "🌌", null, "journal_text", 1, 1.0 },
                    { 34, 33, "Write about playing a sport or active game with others", "⚽", null, "journal_text", 0, 1.0 },
                    { 35, 33, "Mention cheering on a friend or teammate today", "📣", null, "journal_text", 1, 1.0 },
                    { 36, 34, "Journal about helping a friend or sibling out", "🤝", null, "journal_text", 0, 1.0 },
                    { 37, 34, "Write about a nice compliment you gave someone", "💬", null, "journal_text", 1, 1.0 },
                    { 38, 35, "Write about sharing your ideas out loud in front of others", "🎤", null, "journal_text", 0, 1.0 },
                    { 39, 35, "Mention practicing reading out loud", "🗣️", null, "journal_text", 1, 1.0 },
                    { 40, 36, "Write about spending time outside in the fresh air", "🌳", null, "journal_text", 0, 1.0 },
                    { 41, 36, "Journal about observing plants animals or trees today", "🐦", null, "journal_text", 1, 1.0 },
                    { 42, 37, "Write about sorting trash or recycling something today", "♻️", null, "journal_text", 0, 1.0 },
                    { 43, 37, "Journal about keeping your room or classroom tidy", "🧹", null, "journal_text", 1, 1.0 },
                    { 44, 38, "Write about how you fixed a mistake or problem today", "🧩", null, "journal_text", 0, 1.0 },
                    { 45, 38, "Journal about trying again when a task felt hard", "💪", null, "journal_text", 1, 1.0 },
                    { 46, 39, "Journal about a drawing painting or craft you created", "🎨", null, "journal_text", 0, 1.0 },
                    { 47, 39, "Write about your favorite colors or designs today", "🌈", null, "journal_text", 1, 1.0 },
                    { 48, 40, "Write about completing your family chores without being asked", "🏠", null, "journal_text", 0, 1.0 },
                    { 49, 40, "Journal about doing an act of service for your community", "🌟", null, "journal_text", 1, 1.0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminChildrenPersonalityClusters_ChildId_IsCurrent",
                table: "AdminChildrenPersonalityClusters",
                columns: new[] { "ChildId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminChildrenPersonalityClusters_ClusteringRunId",
                table: "AdminChildrenPersonalityClusters",
                column: "ClusteringRunId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminChildrenPersonalityClusters_HashedChildId",
                table: "AdminChildrenPersonalityClusters",
                column: "HashedChildId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminChildrenPersonalityClusters_PersonalityClusterId",
                table: "AdminChildrenPersonalityClusters",
                column: "PersonalityClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_AiBadgeSuggestions_ChildId_IsDismissed_ExpiresAt",
                table: "AiBadgeSuggestions",
                columns: new[] { "ChildId", "IsDismissed", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ParentId",
                table: "AspNetUsers",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeChallenges_BadgeId",
                table: "BadgeChallenges",
                column: "BadgeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildBadges_BadgeId",
                table: "ChildBadges",
                column: "BadgeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildBadges_ChildId",
                table: "ChildBadges",
                column: "ChildId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildChallengeProgresses_ChallengeId",
                table: "ChildChallengeProgresses",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildChallengeProgresses_ChildId_ChallengeId",
                table: "ChildChallengeProgresses",
                columns: new[] { "ChildId", "ChallengeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChildChallengeProgresses_HashedChildId",
                table: "ChildChallengeProgresses",
                column: "HashedChildId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildHobbies_HobbyId",
                table: "ChildHobbies",
                column: "HobbyId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildProfiles_LastPersonalityClusterId",
                table: "ChildProfiles",
                column: "LastPersonalityClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildProfiles_ParentId",
                table: "ChildProfiles",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildProfiles_RecommendedBadgeId",
                table: "ChildProfiles",
                column: "RecommendedBadgeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildProfiles_RecommendedLeafyTipId",
                table: "ChildProfiles",
                column: "RecommendedLeafyTipId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildTasks_ChildId",
                table: "ChildTasks",
                column: "ChildId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyLogEntries_ChildId_LogDate",
                table: "DailyLogEntries",
                columns: new[] { "ChildId", "LogDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyLogEntries_HashedChildId",
                table: "DailyLogEntries",
                column: "HashedChildId");

            migrationBuilder.CreateIndex(
                name: "IX_LeafyTips_Purpose_Side",
                table: "LeafyTips",
                columns: new[] { "Purpose", "Side" });

            migrationBuilder.CreateIndex(
                name: "IX_Levels_LevelNumber",
                table: "Levels",
                column: "LevelNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ReceiverId",
                table: "Notifications",
                column: "ReceiverId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentFeedbacks_DailyLogEntryId",
                table: "ParentFeedbacks",
                column: "DailyLogEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalityClusters_ClusterKey",
                table: "PersonalityClusters",
                column: "ClusterKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_ChildId",
                table: "Rewards",
                column: "ChildId");

            migrationBuilder.CreateIndex(
                name: "IX_TrustScoreHistories_ChildId",
                table: "TrustScoreHistories",
                column: "ChildId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLevels_ChildId",
                table: "UserLevels",
                column: "ChildId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLevels_HashedChildId",
                table: "UserLevels",
                column: "HashedChildId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLevels_LevelId",
                table: "UserLevels",
                column: "LevelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminChildrenPersonalityClusters");

            migrationBuilder.DropTable(
                name: "AdminProfiles");

            migrationBuilder.DropTable(
                name: "AiBadgeSuggestions");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Characters");

            migrationBuilder.DropTable(
                name: "ChildBadges");

            migrationBuilder.DropTable(
                name: "ChildChallengeProgresses");

            migrationBuilder.DropTable(
                name: "ChildHobbies");

            migrationBuilder.DropTable(
                name: "ChildTasks");

            migrationBuilder.DropTable(
                name: "LogFormItems");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "ParentFeedbacks");

            migrationBuilder.DropTable(
                name: "Rewards");

            migrationBuilder.DropTable(
                name: "Subjects");

            migrationBuilder.DropTable(
                name: "TrustScoreHistories");

            migrationBuilder.DropTable(
                name: "UserLevels");

            migrationBuilder.DropTable(
                name: "ChildProfiles");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "BadgeChallenges");

            migrationBuilder.DropTable(
                name: "Hobbies");

            migrationBuilder.DropTable(
                name: "DailyLogEntries");

            migrationBuilder.DropTable(
                name: "Levels");

            migrationBuilder.DropTable(
                name: "LeafyTips");

            migrationBuilder.DropTable(
                name: "ParentProfiles");

            migrationBuilder.DropTable(
                name: "PersonalityClusters");

            migrationBuilder.DropTable(
                name: "Badges");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}

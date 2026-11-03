using GrawiaaApp.API.Data;
using GrawiaaApp.API.Interfaces;
using GrawiaaApp.API.Logic;
using GrawiaaApp.API.Models;
using GrawiaaApp.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// ================= Controllers =================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// ================= Database =================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()));

// ================= Identity =================
// ⚠️ AddIdentity MUST come before AddAuthentication.
// AddIdentity overrides DefaultAuthenticateScheme to Cookie — placing
// AddAuthentication(JWT) afterwards correctly overrides it back to JWT.
builder.Services.AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredUniqueChars = 0;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ================= JWT Authentication =================
// AddAuthentication after AddIdentity so JWT overrides the Cookie scheme.
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role,
        ClockSkew = TimeSpan.Zero          // no grace period on expiry
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"🔴 JWT FAIL: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine($"🟢 JWT OK: {context.Principal?.Identity?.Name}");
            return Task.CompletedTask;
        }
    };
});

// ================= Prevent Cookie middleware from hijacking API 401/403 =================
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

// ================= Caching =================
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheService, CacheService>();

// ================= Application Services =================
builder.Services.AddScoped<IGamingService, GamingService>();
// PHASE 10: BadgeLogic (threshold-based) REMOVED — replaced by NewBadgeEngineService below.
// ParentNotificationLogic is KEPT — it is a DIFFERENT IScoreObserver, unrelated to
// badges/levels (it sends parent alerts when TrustScore drops below 70).
builder.Services.AddScoped<IScoreObserver, ParentNotificationLogic>();

// ── PHASE 10: New Badge + Level System (sole authority for badges/levels) ──
builder.Services.AddScoped<INewBadgeEngineService, NewBadgeEngineService>();
builder.Services.AddScoped<ILevelRulesProvider, LevelRulesProvider>();
builder.Services.AddScoped<INewLevelEngineService, NewLevelEngineService>();
builder.Services.AddScoped<INewProgressionService, NewProgressionService>();

// ── PHASE 10 REFACTOR: Journal abstraction + privacy adapter ──────────────
// JournalService wraps DailyLogEntries behind a journal_entries-shaped
// abstraction (no DB rename). ChildIdentityService provides an optional,
// non-persisted GetSafeChildKey(childId) for logging/external payloads.
builder.Services.AddScoped<IJournalService, JournalService>();
builder.Services.AddScoped<IChildIdentityService, ChildIdentityService>();

// ── PHASE 10 FINAL HARDENING: System Integrity Validator ──────────────────
builder.Services.AddScoped<ISystemIntegrityValidator, SystemIntegrityValidator>();

// Leafy AI — 8 Pillars Calculation Engine (stateless static class; no DI registration needed).
// Registered as a scoped service wrapper for controllers that need to invoke it via DI.
builder.Services.AddScoped<IPillarCalculationService, PillarCalculationService>();

// ── PHASE 11: Single-source Progress / Pillars / Traits pipeline ──────────
// Replaces WeeklyAggregationTrigger, WeeklyTraitSynthesisService (background +
// static ComputeTraits helper), and the inline 90-day FromWeeklyEntries calls
// previously in SaveJournal/QuickCheckIn. See PillarPipelineService.cs.
builder.Services.AddScoped<IPillarPipelineService, PillarPipelineService>();
builder.Services.AddScoped<ITrustScoreEngine, TrustScoreEngine>();
builder.Services.AddScoped<IPersonalityClusterFallbackService, PersonalityClusterFallbackService>();

// Background services
builder.Services.AddHostedService<DailyLogClosureService>();

// ── PHASE 12: AI Integration scheduled jobs ─────────────────────────
// Orchestration/validation only — none of these recompute AI outputs.
// See each service's header comment for exactly what it does and does not do.
builder.Services.AddHostedService<DailyAiOrchestrationService>();              // Daily — Anomaly, NLP, Leafy
builder.Services.AddHostedService<WeeklyBadgeRecommenderOrchestrationService>(); // Weekly — Badge Recommender
builder.Services.AddHostedService<PeriodicClusteringOrchestrationService>();    // Periodic — Personality Clustering
// ════════════════════════════════════════════════════════════════
//  AI TEAM — Database Polling Sync Services (Phase 15)
// ════════════════════════════════════════════════════════════════
builder.Services.AddHostedService<BadgeRecommendationSyncService>();
builder.Services.AddHostedService<PersonalityClusterSyncService>();
builder.Services.AddHostedService<LeafyRagSyncService>();
// ── PHASE 13 BUG FIX: NLP → Final Hybrid Pillar Sync ───────────────────
// When the AI team writes NlpXxx_Pct DIRECTLY to the database (bypassing
// the API), PillarPipelineService is never triggered automatically, leaving
// FinalXxx_Pct stale. NlpPillarSyncService detects stale profiles
// (NlpPillarsCalculatedAt > LastAggregatedAt) and re-runs
// RecalculateProfileAndTraitsAsync to refresh FinalXxx_Pct + FinalMeanScore.
// Runs every 60 min by default ("AiTeam:NlpSyncIntervalMinutes" to override).
builder.Services.AddHostedService<NlpPillarSyncService>();                      // Hourly — NLP→Final pillar sync

// ── PHASE 14 BUG FIX: Challenge Progress → Badge/Level Bridge ─────────────
// When the AI team's NLP pipeline writes "journal_text" challenge
// completions DIRECTLY to child_challenge_progress (bypassing the API),
// NewBadgeEngineService/NewLevelEngineService are never triggered
// automatically, so AI-gated badges (journal_text-only challenges, e.g.
// Morning Prayer, Storyteller) can sit 100% complete in
// ChildChallengeProgresses and still never unlock for the child.
// ChallengeProgressBridgeService closes that gap — it existed in the
// codebase but was never registered as a hosted service, so it never
// actually ran. Registering it here is the fix.
// Runs every 5 min by default ("AiTeam:ChallengeBridgeIntervalMinutes" /
// "AiTeam:ChallengeBridgeIntervalSeconds" to override).
builder.Services.AddHostedService<ChallengeProgressBridgeService>();            // Fast — Challenge→Badge/Level bridge

// ── Phase 4: Tip / Alert / Motivation Services ─────────────────────────
builder.Services.AddScoped<ITipGenerationService, TipGenerationService>();
builder.Services.AddScoped<IAlertGenerationService, AlertGenerationService>();
builder.Services.AddScoped<IMotivationGenerationService, MotivationGenerationService>();

builder.Services.AddScoped<IEncryptionService, AesEncryptionService>();
builder.Services.AddScoped<IDefaultTaskSeederService, DefaultTaskSeederService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<IEmailSender, EmailSender>();

// ================= CORS =================
// FIX: Read allowed origins from configuration so the policy works for
// both file:// (VS Code Live Server / browser open) and http://127.0.0.1:5500.
// AllowAnyOrigin() is fine here because we are NOT using AllowCredentials().
// JWT tokens are sent in the Authorization header, not as cookies,
// so credentials-mode is irrelevant for this API.
var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>()
    ?? new[] {
        "http://127.0.0.1:5500",
        "http://localhost:5500",
        "http://127.0.0.1:3000",
        "http://localhost:3000",
        "http://127.0.0.1:5153",
        "http://localhost:5153"
    };

builder.Services.AddCors(options =>
{
    // ── Production policy: explicit origin whitelist ──────────────
    options.AddPolicy("GrowiaOrigins", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              // FIX P2: إضافة X-Device-Fingerprint لـ AllowedHeaders في production policy
              .WithHeaders("Authorization", "Content-Type", "X-Device-Fingerprint", "Accept"));

    // ── Development / local file:// fallback ─────────────────────
    // Used ONLY during development (file:// has no origin header so
    // WithOrigins would reject it). Switch to "GrowiaOrigins" in prod.
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// ================= Swagger =================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Grawiaa API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        Description = "Paste your JWT token here (without 'Bearer' prefix)."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ================= Build =================
var app = builder.Build();

// ================= Middleware Pipeline =================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseRouting();

// Use "AllowAll" during development (supports file:// and any localhost port).
// Switch to "GrowiaOrigins" when deploying to a real server.
app.UseCors(app.Environment.IsDevelopment() ? "AllowAll" : "GrowiaOrigins");

// ⚠️ Order matters: Authentication → Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ================= Seed Roles & Default Admin =================
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    foreach (var role in new[] { "Admin", "Parent", "Child" })
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole<int>(role));
    }

    var adminUser = await userManager.FindByNameAsync("admin");
    if (adminUser == null)
    {
        var admin = new ApplicationUser
        {
            UserName = "admin",
            Email = "admin@growia.com",
            EmailConfirmed = true,
            FullName = "Growia Admin",
            UserType = "Admin",
            CreatedAt = DateTime.UtcNow
        };
        var result = await userManager.CreateAsync(admin, "Admin@123");
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
    }
}

// ── PHASE 10 FINAL HARDENING: Run integrity check at startup (non-blocking) ──
_ = Task.Run(async () =>
{
    await Task.Delay(TimeSpan.FromSeconds(5)); // wait for EF pool to warm up
    try
    {
        using var scope = app.Services.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<ISystemIntegrityValidator>();
        var report = await validator.ValidateAsync();
        var startupLogger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("StartupIntegrity");
        if (!report.IsHealthy)
            startupLogger.LogError("[Integrity] Startup check FAILED — {Count} violation(s). Review logs.", report.ViolationCount);
        else
            startupLogger.LogInformation("[Integrity] Startup check PASSED — {Count} rules verified.", report.TotalChecks);
    }
    catch (Exception ex)
    {
        // Never crash the app on validator failure — log and continue
        var fb = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("StartupIntegrity");
        fb.LogError(ex, "[Integrity] Startup validator threw an exception.");
    }
});

app.Run();
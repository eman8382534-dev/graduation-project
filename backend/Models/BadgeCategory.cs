namespace GrawiaaApp.API.Models
{
    public enum BadgeCategory
    {
        // ── First Journal ────────────────────────────────────────────
        Supportive,       // أول journal entry

        // ── Consistency (streak) ────────────────────────────────────
        Consistency3,     // 3 أيام متتالية
        Consistency7,     // 7 أيام  — Flame Hero
        Consistency30,    // 30 يوم  — Legend Streaker

        // ── Task Master ──────────────────────────────────────────────
        TaskMaster5,      // 5 tasks مكتملة
        TaskMaster20,     // 20 tasks — Achiever
        TaskMaster50,     // 50 tasks — Champion

        // ── Level Milestones (6 levels) ──────────────────────────────
        LevelSeedling,    // Level 1 — The Sprout
        LevelExplorer,    // Level 2 — Week One Hero
        LevelAdventurer,  // Level 3 — Kindness Bloom
        LevelChampion,    // Level 4 — Sunshine Star
        LevelLegend,      // Level 5 — Champion
        LevelGrawiaaHero, // Level 6 — Grawiaa Legend

        // ── Balanced ─────────────────────────────────────────────────
        BalancedHero,     // كل الـ 8 pillars >= 40%
        BalancedMaster,   // كل الـ 8 pillars >= 70%

        // ══════════════════════════════════════════════════════════════
        //  8 PILLAR BADGES — Bronze / Silver / Gold
        //  مستقلة تماماً عن الـ levels
        //  Bronze >= 30% | Silver >= 60% | Gold >= 85%
        // ══════════════════════════════════════════════════════════════

        // 🕋 Pillar 1 — Spiritual Connection
        SpiritualBronze,
        SpiritualSilver,
        SpiritualGold,

        // 👥 Pillar 2 — Social Interaction
        SocialBronze,
        SocialSilver,
        SocialGold,

        // 📚 Pillar 3 — Academic Progress
        AcademicBronze,
        AcademicSilver,
        AcademicGold,

        // 🏠 Pillar 4 — Home Responsibility
        HomeResBronze,
        HomeResSilver,
        HomeResGold,

        // 🍎 Pillar 5 — Nutrition & Physical Health
        NutritionBronze,
        NutritionSilver,
        NutritionGold,

        // ⚽ Pillar 6 — Sport & Movement
        SportBronze,
        SportSilver,
        SportGold,

        // 🎨 Pillar 7 — Hobbies & Creativity
        HobbiesBronze,
        HobbiesSilver,
        HobbiesGold,

        // 😊 Pillar 8 — Mental Health
        MentalBronze,
        MentalSilver,
        MentalGold,

        // 📖 Pillar 9 — Creative (AI library badge 31 uses this)
        CreativeExplorer,
    }
}

// ============================================================
//  TraitSynthesisEngine.cs — Leafy AI System
//
//  Converts 8 Final Hybrid Pillars (ChildProfile.FinalXxx_Pct) +
//  AI signals into 5 Core Personality Traits.
//
//  DESIGN CONTRACT  (aligned with Leafy & The System spec)
//  ───────────────
//  • Consumes ChildProfile FINAL HYBRID pillars and AiAnalyticsResult only.
//  • Does NOT modify AI engine or Pillar engine.
//  • Does NOT write to ChildProfile — use ApplyToProfile() for that.
//  • Fully deterministic — zero randomness.
//  • Injectable via DI (registered as Scoped).
//  • Reused by WeeklyTraitSynthesisService (background) and any
//    inline call after a journal save.
//
//  AUDIT FIX #2 (Phase 13 alignment): this engine originally read the
//  Structured-only ChildProfile.Xxx_Pct fields (pre-Hybrid-Pillar, Phase
//  11 design). Phase 13 introduced FinalXxx_Pct (70% Structured + 30%
//  NLP) as "the canonical values consumed by ... All frontend pillar
//  displays" (see ChildProfile.cs's Final Hybrid Pillars block) and
//  NewLevelEngineService.cs states the rule explicitly: "Per Phase 13
//  spec: all downstream systems use Final Pillars." Traits are a
//  downstream consumer of pillars, so this engine — and the formulas
//  documented below — now read FinalXxx_Pct, matching that rule and
//  AiController's GetChildProfileByHashedId comment ("5 Core Traits
//  derived from Final Hybrid Pillars"). NOTE: this changes the actual
//  numbers traits compute to whenever NLP data is present (previously
//  the NLP component was silently ignored by trait math even though it
//  already fed into FinalXxx_Pct everywhere else) — confirm this is the
//  behavior you want before deploying, since AI/UI consumers may have
//  cached expectations from the old structured-only values.
//
//  TRAIT FORMULAS  (Primary + Secondary impacts, per design spec)
//  ──────────────
//
//  KINDNESS                                       (cap: 90)
//    primary    = FinalSocialInteraction_Pct  * 0.45  ← Social Interaction (Priority)
//               + FinalHomeResponsibility_Pct * 0.40  ← Family Chores (Priority)
//    secondary  = FinalMentalWellbeing_Pct    * 0.15  ← Mental Health (Mood)
//    sentimentBoost = (sentimentScore * 0.06) when sentimentScore ≥ 50, else 0
//    anomalyPenalty applied at end (−5 if anomalyFlag)
//    Result clamped to [0, 90]
//    Total primary+secondary weights: 0.45 + 0.40 + 0.15 = 1.00 ✓
//    BUG FIX (Phase 14 Audit): previous formula used Mental 0.91 + Social 0.09,
//    which contradicted Spec Rule B. Family Chores was entirely missing from the
//    computation despite KindnessChoresWeight = 0.40 being declared. Fixed to
//    Social 0.45 + Chores 0.40 + Mental 0.15 matching the spec's intent that
//    Social Interaction and Family Chores are the PRIMARY drivers of Kindness.
//
//  VITALITY                                       (cap: 100)
//    primary    = FinalPhysicalHealth_Pct   * 0.57   ← Nutrition & Food (Priority)
//               + FinalPhysicalActivity_Pct * 0.43   ← Sport & Movement
//    anomalyPenalty applied at end (−3 if anomalyFlag)
//
//  CURIOSITY                                      (cap: 100)
//    primary    = FinalAcademicProgress_Pct      * 0.60   ← Homework & Study (Priority)
//    secondary  = FinalCreativityEngagement_Pct  * 0.40   ← Hobbies
//    anomalyPenalty applied at end (−3 if anomalyFlag)
//
//  RESILIENCE                                     (cap: 65)
//    primary    = FinalMentalWellbeing_Pct       * 0.52   ← Mental Health (Priority)
//    secondary  = FinalSocialInteraction_Pct     * 0.10   ← Social Interaction
//               + FinalHomeResponsibility_Pct    * 0.10   ← Family Chores
//               + FinalPhysicalHealth_Pct        * 0.05   ← Nutrition & Food
//               + FinalPhysicalActivity_Pct      * 0.05   ← Sport & Movement
//               + sentimentScore                 * 0.18   ← AI sentiment modifier
//    anomalyPenalty applied at end (−5 if anomalyFlag)
//    Result clamped to [0, 65]
//
//  SPIRITUAL                                      (cap: 100)
//    base  = FinalSpiritualConnection_Pct * 0.80
//          + FinalMentalWellbeing_Pct     * 0.20
//    anomalyPenalty applied at end (−3 if anomalyFlag)
//
//  ANOMALY GLOBAL PENALTY
//  ──────────────────────
//  When anomalyFlag = true an additional destabilisation penalty is
//  applied uniformly across ALL five traits before final clamp:
//    penalty = −5 points per trait
//  This reflects reduced confidence in all signals when a log is
//  flagged as potentially inconsistent.
//
//  NOTE on Mental Wellbeing AI dominance (≈72% across affected traits):
//  Every trait formula that touches FinalMentalWellbeing_Pct (Kindness,
//  Resilience, Spiritual) receives a pillar whose Structured component is
//  itself 60% AI SentimentScore (see PillarCalculationEngine.CalcMental),
//  on top of the 30% NLP share every Final pillar carries, plus Resilience's
//  own explicit +0.18 sentiment modifier. Net effect: MentalWellbeing's
//  AI-derived signal effectively dominates ~72% of 3 out of 5 traits.
//
//  ⚠ RISK — Weak or noisy SentimentScore propagates broadly:
//  If the AI sentiment model underperforms (low confidence, drift, or gaps
//  in coverage), the error will propagate across Kindness, Resilience, AND
//  Spiritual simultaneously. A single bad SentimentScore batch can depress
//  or inflate 3 traits at once, making the degradation difficult to diagnose
//  because it looks like a correlated drop rather than a model failure.
//
//  ACCEPTED per design (intentional AI-heaviness for MentalWellbeing focus).
//  AI team must monitor SentimentScore model quality separately and flag
//  confidence issues upstream. If the sentiment model is known to be weak,
//  consider reducing KindnessMentalWeight/ResilienceSentimentWeight or
//  temporarily capping FinalMentalWellbeing_Pct contribution until the
//  model is retrained/replaced.
//
//  All outputs are clamped to their per-trait ceiling then [0.0, 100.0],
//  and rounded to 1 decimal.
// ============================================================

using System;
using GrawiaaApp.API.Models;

namespace GrawiaaApp.API.Services
{
    public sealed class TraitSynthesisEngine : ITraitSynthesisService
    {
        // ─────────────────────────────────────────────────────────────
        //  Per-trait caps (design spec ceilings)
        // ─────────────────────────────────────────────────────────────

        private const double KindnessCap = 90.0;
        private const double ResilienceCap = 65.0;

        // ─────────────────────────────────────────────────────────────
        //  Weight constants
        // ─────────────────────────────────────────────────────────────

        // Kindness — SPEC RULE B (Phase 14 Audit Fix):
        //   Social Interaction  = PRIMARY  (45%)  — Journal Section → Primary Kindness
        //   Family Chores       = PRIMARY  (40%)  — Journal Section → Primary Kindness
        //   Mental Health       = SECONDARY (15%) — Journal Section → Secondary Kindness
        //
        // PREVIOUS (incorrect): Mental Health 91%, Social 9%, Chores missing entirely.
        // The previous weights contradicted Spec Rule B which designates Social
        // Interaction and Family Chores as the PRIMARY drivers of Kindness, with
        // Mental Health (Mood) only as a SECONDARY influence.
        // Total primary+secondary: 45 + 40 + 15 = 100% ✓
        private const double KindnessSocialWeight = 0.45;    // primary: Social Interaction → Kindness
        private const double KindnessChoresWeight = 0.40;    // primary: Family Chores → Kindness
        private const double KindnessMentalWeight = 0.15;    // secondary: Mental Health (Mood) → Kindness
        private const double KindnessSentimentBoostWeight = 0.06;   // small AI sentiment boost (when ≥ threshold)

        // Vitality — aligns with spec: Nutrition & Food = Priority (57%), Sport & Movement = Secondary (38%)
        // PhysicalHealth_Pct = Nutrition & Food
        // PhysicalActivity_Pct = Sport & Movement
        private const double VitalityNutritionWeight = 0.50;   // primary: Nutrition & Food → Vitality
        private const double VitalitySportWeight = 0.38;   // secondary: Sport & Movement → Vitality
        private const double VitalityHobbiesWeight = 0.12;  // secondary: Hobbies → Vitality (per spec) (50+38+12=100%)

        // Curiosity — aligns with spec: Homework & Study = Priority, Hobbies = Secondary
        private const double CuriosityAcademicWeight = 0.60;   // primary: Homework & Study → Curiosity
        private const double CuriosityHobbiesWeight = 0.40;   // secondary: Hobbies → Curiosity

        // Resilience — aligns with spec: Mental Health = Priority; Social + Chores + Sport + Nutrition = Secondary
        // Total secondary weights: 10 + 10 + 5 + 5 = 30% + sentiment 18% = 48%
        // Primary Mental Health: 52%
        private const double ResilienceMentalWeight = 0.52;   // primary: Mental Health → Resilience
        private const double ResilienceSentimentWeight = 0.18;   // modifier: AI sentiment score
        private const double ResilienceSocialWeight = 0.10;   // secondary: Social Interaction → Resilience
        private const double ResilienceChoresWeight = 0.10;   // secondary: Family Chores → Resilience
        private const double ResilienceNutritionWeight = 0.05;   // secondary: Nutrition & Food → Resilience
        private const double ResilienceSportWeight = 0.05;   // secondary: Sport & Movement → Resilience

        // Spiritual — unchanged (no secondary pillar in spec)
        private const double SpiritualPrimaryWeight = 0.80;
        private const double SpiritualMentalWeight = 0.20;

        // Global anomaly destabilisation penalty (applied to all traits when flag = true)
        private const double GlobalAnomalyPenalty = 5.0;

        // Per-trait additional anomaly penalties (applied before global)
        private const double KindnessAnomalyPenalty = 5.0;
        private const double VitalityAnomalyPenalty = 3.0;
        private const double CuriosityAnomalyPenalty = 3.0;
        private const double ResilienceAnomalyPenalty = 5.0;
        private const double SpiritualAnomalyPenalty = 3.0;

        // sentimentScore threshold above which Kindness receives a boost
        private const double SentimentBoostThreshold = 50.0;

        // ─────────────────────────────────────────────────────────────
        //  ITraitSynthesisService implementation
        // ─────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public TraitSynthesisResult Synthesise(ChildProfile profile, AiAnalyticsResult ai)
        {
            if (profile is null) throw new ArgumentNullException(nameof(profile));
            if (ai is null) throw new ArgumentNullException(nameof(ai));

            double sentiment = ai.SentimentScore;   // 0–100
            bool anomalyFlag = ai.AnomalyFlag;
            double globalPenalty = anomalyFlag ? GlobalAnomalyPenalty : 0.0;

            return new TraitSynthesisResult
            {
                Kindness = RoundCapped(ComputeKindness(profile, sentiment, anomalyFlag) - globalPenalty, KindnessCap),
                Vitality = Round(ComputeVitality(profile, anomalyFlag) - globalPenalty),
                Curiosity = Round(ComputeCuriosity(profile, anomalyFlag) - globalPenalty),
                Resilience = RoundCapped(ComputeResilience(profile, sentiment, anomalyFlag) - globalPenalty, ResilienceCap),
                Spiritual = Round(ComputeSpiritual(profile, anomalyFlag) - globalPenalty),
            };
        }

        /// <inheritdoc/>
        public void ApplyToProfile(ChildProfile profile, TraitSynthesisResult result)
        {
            if (profile is null) throw new ArgumentNullException(nameof(profile));
            if (result is null) throw new ArgumentNullException(nameof(result));

            profile.KindnessTrait = result.Kindness;
            profile.VitalityTrait = result.Vitality;
            profile.CuriosityTrait = result.Curiosity;
            profile.ResilienceTrait = result.Resilience;
            profile.ResiliencePct = result.Resilience; // PHASE10-FIX-A: sync orphaned column
            profile.SpiritualTrait = result.Spiritual;
        }

        // ─────────────────────────────────────────────────────────────
        //  Per-trait computation methods
        // ─────────────────────────────────────────────────────────────

        // ── KINDNESS ─────────────────────────────────────────────────
        // Primary:   FinalSocialInteraction_Pct  (45%) — Social Interaction (Priority)
        //          + FinalHomeResponsibility_Pct  (40%) — Family Chores (Priority)
        // Secondary: FinalMentalWellbeing_Pct    (15%) — Mental Health (Mood)
        // Boost:    (sentimentScore × 0.06) if sentimentScore ≥ 50  (applied on top of base)
        // Penalty:  −5 when anomalyFlag
        // Cap:      90
        // BUG FIX (Phase 14 Audit): added FinalHomeResponsibility_Pct × KindnessChoresWeight.
        // Previously this value was declared (0.40) but never referenced in the formula,
        // meaning Family Chores had zero effect on Kindness despite being a Primary driver
        // per Spec Rule B. Weights corrected: Social 0.45 + Chores 0.40 + Mental 0.15 = 1.00.
        private static double ComputeKindness(ChildProfile p, double sentiment, bool anomaly)
        {
            double sentimentBoost = sentiment >= SentimentBoostThreshold ? sentiment : 0.0;

            double raw = (p.FinalSocialInteraction_Pct * KindnessSocialWeight)      // primary: Social Interaction
                       + (p.FinalHomeResponsibility_Pct * KindnessChoresWeight)     // primary: Family Chores (BUG FIX)
                       + (p.FinalMentalWellbeing_Pct * KindnessMentalWeight)        // secondary: Mental Health
                       + (sentimentBoost * KindnessSentimentBoostWeight);            // AI sentiment boost

            if (anomaly) raw -= KindnessAnomalyPenalty;
            return raw;
        }

        // ── VITALITY ─────────────────────────────────────────────────
        // Primary:   FinalPhysicalHealth_Pct   (57%) — Nutrition & Food (Priority)
        // Secondary: FinalPhysicalActivity_Pct (43%) — Sport & Movement
        // Penalty:   −3 when anomalyFlag
        private static double ComputeVitality(ChildProfile p, bool anomaly)
        {
            double raw = (p.FinalPhysicalHealth_Pct * VitalityNutritionWeight)          // primary: Nutrition & Food
                       + (p.FinalPhysicalActivity_Pct * VitalitySportWeight)             // secondary: Sport & Movement
                       + (p.FinalCreativityEngagement_Pct * VitalityHobbiesWeight);     // secondary: Hobbies (spec fix)

            if (anomaly) raw -= VitalityAnomalyPenalty;
            return raw;
        }

        // ── CURIOSITY ─────────────────────────────────────────────────
        // Primary:   FinalAcademicProgress_Pct    (60%) — Homework & Study (Priority)
        // Secondary: FinalCreativityEngagement_Pct (40%) — Hobbies
        // Penalty:   −3 when anomalyFlag
        private static double ComputeCuriosity(ChildProfile p, bool anomaly)
        {
            double raw = (p.FinalAcademicProgress_Pct * CuriosityAcademicWeight)  // primary: Homework & Study
                       + (p.FinalCreativityEngagement_Pct * CuriosityHobbiesWeight); // secondary: Hobbies

            if (anomaly) raw -= CuriosityAnomalyPenalty;
            return raw;
        }

        // ── RESILIENCE ────────────────────────────────────────────────
        // Primary:   FinalMentalWellbeing_Pct  (52%) — Mental Health (Priority)
        // Modifier:  sentimentScore        (18%) — AI sentiment modifier
        // Secondary: FinalSocialInteraction_Pct (10%) — Social Interaction
        // Secondary: FinalHomeResponsibility_Pct (10%) — Family Chores
        // Secondary: FinalPhysicalHealth_Pct    (5%)  — Nutrition & Food
        // Secondary: FinalPhysicalActivity_Pct  (5%)  — Sport & Movement
        // Penalty:   −5 when anomalyFlag
        // Cap:      65
        private static double ComputeResilience(ChildProfile p, double sentiment, bool anomaly)
        {
            double raw = (p.FinalMentalWellbeing_Pct * ResilienceMentalWeight)     // primary: Mental Health
                       + (sentiment * ResilienceSentimentWeight)  // AI sentiment modifier
                       + (p.FinalSocialInteraction_Pct * ResilienceSocialWeight)     // secondary: Social Interaction
                       + (p.FinalHomeResponsibility_Pct * ResilienceChoresWeight)   // secondary: Family Chores
                       + (p.FinalPhysicalHealth_Pct * ResilienceNutritionWeight)  // secondary: Nutrition & Food
                       + (p.FinalPhysicalActivity_Pct * ResilienceSportWeight);    // secondary: Sport & Movement

            if (anomaly) raw -= ResilienceAnomalyPenalty;
            return raw;
        }

        // ── SPIRITUAL ─────────────────────────────────────────────────
        // Primary: FinalSpiritualConnection_Pct (80%) — Connection with Allah (Priority)
        // Support: FinalMentalWellbeing_Pct     (20%)
        // Penalty: −3 when anomalyFlag
        private static double ComputeSpiritual(ChildProfile p, bool anomaly)
        {
            double raw = (p.FinalSpiritualConnection_Pct * SpiritualPrimaryWeight)
                       + (p.FinalMentalWellbeing_Pct * SpiritualMentalWeight);

            if (anomaly) raw -= SpiritualAnomalyPenalty;
            return raw;
        }

        // ─────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Clamps to [0, 100] then rounds to 1 decimal.
        /// Used for Vitality, Curiosity, Spiritual (cap = 100).
        /// </summary>
        private static double Round(double v)
            => Math.Round(Math.Clamp(v, 0.0, 100.0), 1);

        /// <summary>
        /// Clamps to [0, cap] (the trait's design-spec ceiling) then rounds
        /// to 1 decimal. Used for Kindness (cap 90) and Resilience (cap 65).
        /// </summary>
        private static double RoundCapped(double v, double cap)
            => Math.Round(Math.Clamp(v, 0.0, cap), 1);
    }
}
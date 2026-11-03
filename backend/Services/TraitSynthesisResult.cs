// ============================================================
//  TraitSynthesisResult.cs — Leafy AI System
//
//  Immutable output model produced by TraitSynthesisEngine.
//  All five trait values are clamped to 0–100.
//  All calculations are deterministic.
// ============================================================

namespace GrawiaaApp.API.Services
{
    /// <summary>
    /// The 5 Core Personality Traits computed by <see cref="TraitSynthesisEngine"/>
    /// from 8 pillar values + AI analytics signals.
    /// Aligned with Leafy &amp; The System design spec.
    /// </summary>
    public sealed class TraitSynthesisResult
    {
        /// <summary>
        /// Kindness trait (0–90).
        /// Primary:   FinalMentalWellbeing_Pct    (91%) — Mental Health (Priority)
        /// Secondary: FinalSocialInteraction_Pct (9%)  — Social Interaction
        /// Boost:     sentimentScore if ≥ 50
        /// </summary>
        public double Kindness { get; init; }

        /// <summary>
        /// Vitality trait (0–100).
        /// Primary:   FinalPhysicalHealth_Pct   (57%) — Nutrition &amp; Food (Priority)
        /// Secondary: FinalPhysicalActivity_Pct (43%) — Sport &amp; Movement
        /// </summary>
        public double Vitality { get; init; }

        /// <summary>
        /// Curiosity trait (0–100).
        /// Primary:   FinalAcademicProgress_Pct    (60%) — Homework &amp; Study (Priority)
        /// Secondary: FinalCreativityEngagement_Pct (40%) — Hobbies
        /// </summary>
        public double Curiosity { get; init; }

        /// <summary>
        /// Resilience trait (0–65).
        /// Primary:   FinalMentalWellbeing_Pct  (52%) — Mental Health (Priority)
        /// Modifier:  sentimentScore       (18%) — AI sentiment
        /// Secondary: FinalSocialInteraction_Pct (10%) — Social Interaction
        /// Secondary: FinalHomeResponsibility_Pct (10%) — Family Chores
        /// Secondary: FinalPhysicalHealth_Pct    (5%) — Nutrition &amp; Food
        /// Secondary: FinalPhysicalActivity_Pct  (5%) — Sport &amp; Movement
        /// </summary>
        public double Resilience { get; init; }

        /// <summary>
        /// Spiritual trait (0–100).
        /// Primary:   FinalSpiritualConnection_Pct (80%) — Connection with Allah (Priority)
        /// Support:   FinalMentalWellbeing_Pct     (20%)
        /// </summary>
        public double Spiritual { get; init; }
    }
}
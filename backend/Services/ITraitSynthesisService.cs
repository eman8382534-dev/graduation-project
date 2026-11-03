// ============================================================
//  ITraitSynthesisService.cs — Leafy AI System
//
//  DI contract for the Trait Synthesis Engine.
//
//  Consumes:
//    • ChildProfile   — 8 Final Hybrid pillars (FinalXxx_Pct, Phase 13:
//                        70% Structured + 30% NLP). See audit fix #2 in
//                        TraitSynthesisEngine.cs for why this reads the
//                        Final pillars and not the Structured-only ones.
//    • AiAnalyticsResult — sentimentScore, detectedEmotion, anomalyFlag
//
//  Produces:
//    • TraitSynthesisResult — 5 core personality traits (0–100 each)
//
//  RULES:
//  ──────
//  • Must NOT modify AI engine or Pillar engine.
//  • Only consumes their outputs.
//  • Fully deterministic — no random values.
//  • Injectable via DI (Scoped).
// ============================================================

using GrawiaaApp.API.Models;

namespace GrawiaaApp.API.Services
{
    public interface ITraitSynthesisService
    {
        /// <summary>
        /// Computes 5 Core Personality Traits from the 8 aggregated pillars
        /// on a ChildProfile, enriched by the AI analytics signals.
        /// </summary>
        /// <param name="profile">
        ///   ChildProfile with already-computed 8-pillar values.
        ///   This method does NOT modify the pillar fields.
        /// </param>
        /// <param name="ai">
        ///   AI analytics result from AiAnalyticsEngine.
        ///   Used for sentimentScore, detectedEmotion, and anomalyFlag.
        /// </param>
        TraitSynthesisResult Synthesise(ChildProfile profile, AiAnalyticsResult ai);

        /// <summary>
        /// Applies a TraitSynthesisResult back onto a ChildProfile in-place.
        /// Call this after Synthesise() to persist the 5 trait values.
        /// </summary>
        void ApplyToProfile(ChildProfile profile, TraitSynthesisResult result);
    }
}
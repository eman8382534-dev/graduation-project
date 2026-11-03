// ============================================================
//  AiAnalyticsResult.cs — Leafy AI System
//
//  Internal result model used by the backend's legacy AI engine.
//  NOTE: As of Phase 9, AI fields on DailyLogEntry are written
//  exclusively by the external AI team directly to the database.
//  This class is retained for WeeklyTraitSynthesisService which
//  reads already-stored AI values and aggregates them into
//  ChildProfile weekly fields.
// ============================================================

namespace GrawiaaApp.API.Services
{
    public sealed class AiAnalyticsResult
    {
        // ── Sentiment ─────────────────────────────────────────────────

        /// <summary>
        /// Overall sentiment score. Range: 0 (very negative) – 100 (very positive).
        /// </summary>
        public int SentimentScore { get; init; }

        // ── Emotion ───────────────────────────────────────────────────

        /// <summary>
        /// Primary emotion detected.
        /// </summary>
        public string DetectedEmotion { get; init; } = "Good";

        // ── Anomaly Signals ───────────────────────────────────────────

        /// <summary>
        /// Consistency / honesty anomaly score (0–100).
        /// Renamed from IsHonestAnomalyScore to AnomalyScore.
        /// </summary>
        public int AnomalyScore { get; init; }

        /// <summary>
        /// NLP-level anomaly score (0–100).
        /// </summary>
        public int NlpAnomalyScore { get; init; }

        /// <summary>
        /// True when anomaly is detected.
        /// </summary>
        public bool AnomalyFlag { get; init; }
    }
}

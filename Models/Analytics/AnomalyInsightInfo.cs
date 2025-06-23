namespace Log_Parser_App.Models.Analytics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Represents anomaly detection insights and recommendations
    /// </summary>
    public class AnomalyInsightInfo
    {
        /// <summary>
        /// Summary text describing detected anomalies
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Total number of anomalies detected
        /// </summary>
        public int AnomalyCount { get; set; }

        /// <summary>
        /// Collection of top anomalies with highest severity
        /// </summary>
        public AnomalyDetail[] TopAnomalies { get; set; } = Array.Empty<AnomalyDetail>();

        /// <summary>
        /// Recommended actions based on detected anomalies
        /// </summary>
        public string RecommendedActions { get; set; } = string.Empty;

        /// <summary>
        /// Overall severity level of detected anomalies
        /// </summary>
        public AnomalySeverity OverallSeverity { get; set; } = AnomalySeverity.Low;

        /// <summary>
        /// Timestamp when the analysis was performed
        /// </summary>
        public DateTime AnalysisTimestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Indicates whether anomalies were detected
        /// </summary>
        public bool HasAnomalies => AnomalyCount > 0;

        /// <summary>
        /// Indicates whether recommendations are available
        /// </summary>
        public bool HasRecommendations => !string.IsNullOrEmpty(RecommendedActions);

        /// <summary>
        /// Summary text for display, with fallback for no anomalies
        /// </summary>
        public string DisplaySummary => string.IsNullOrEmpty(Summary) 
            ? "No significant anomalies detected" 
            : Summary;

        /// <summary>
        /// Formatted anomaly count for display
        /// </summary>
        public string AnomalyCountFormatted => AnomalyCount switch
        {
            0 => "No anomalies",
            1 => "1 anomaly",
            _ => $"{AnomalyCount} anomalies"
        };

        /// <summary>
        /// Color code based on overall severity
        /// </summary>
        public string SeverityColor => OverallSeverity switch
        {
            AnomalySeverity.Critical => "#F15B5B", // Red
            AnomalySeverity.High => "#F9A825",     // Orange
            AnomalySeverity.Medium => "#FDD835",   // Yellow
            AnomalySeverity.Low => "#64F5A6",      // Green
            _ => "#888888"                         // Gray
        };

        /// <summary>
        /// Icon representation for the overall severity
        /// </summary>
        public string SeverityIcon => OverallSeverity switch
        {
            AnomalySeverity.Critical => "🚨",
            AnomalySeverity.High => "⚠️",
            AnomalySeverity.Medium => "⚡",
            AnomalySeverity.Low => "ℹ️",
            _ => "🔍"
        };

        /// <summary>
        /// Formatted time since analysis
        /// </summary>
        public string TimeSinceAnalysis
        {
            get
            {
                var timeDiff = DateTime.UtcNow - AnalysisTimestamp;
                if (timeDiff.TotalMinutes < 1)
                    return "Just now";
                if (timeDiff.TotalMinutes < 60)
                    return $"{(int)timeDiff.TotalMinutes}m ago";
                if (timeDiff.TotalHours < 24)
                    return $"{(int)timeDiff.TotalHours}h ago";
                return AnalysisTimestamp.ToString("MM/dd HH:mm");
            }
        }

        /// <summary>
        /// Top 3 anomalies for compact display
        /// </summary>
        public AnomalyDetail[] TopThreeAnomalies => TopAnomalies.Take(3).ToArray();

        /// <summary>
        /// Indicates if this insight requires immediate attention
        /// </summary>
        public bool RequiresAttention => OverallSeverity >= AnomalySeverity.High || AnomalyCount > 5;

        /// <summary>
        /// Brief insight for compact display
        /// </summary>
        public string BriefInsight
        {
            get
            {
                if (!HasAnomalies)
                    return "System operating normally";

                var highSeverityCount = TopAnomalies.Count(a => a.Severity >= AnomalySeverity.High);
                if (highSeverityCount > 0)
                    return $"{highSeverityCount} high-priority anomalies detected";

                return $"{AnomalyCount} minor anomalies identified";
            }
        }

        /// <summary>
        /// Display text for UI showing anomaly count and severity
        /// </summary>
        public string DisplayText => $"{SeverityIcon} {AnomalyCountFormatted} ({OverallSeverity})";
    }

    /// <summary>
    /// Represents a specific anomaly with detailed information
    /// </summary>
    public class AnomalyDetail
    {
        /// <summary>
        /// Type of anomaly detected
        /// </summary>
        public AnomalyType Type { get; set; }

        /// <summary>
        /// Timestamp when the anomaly was detected
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Measured value that triggered the anomaly detection
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Threshold value that was exceeded
        /// </summary>
        public double Threshold { get; set; }

        /// <summary>
        /// Severity level of this specific anomaly
        /// </summary>
        public AnomalySeverity Severity { get; set; }

        /// <summary>
        /// Descriptive text explaining the anomaly
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Additional context or metadata about the anomaly
        /// </summary>
        public string Context { get; set; } = string.Empty;

        /// <summary>
        /// Deviation percentage from normal values
        /// </summary>
        public double DeviationPercentage => Threshold > 0 ? ((Value - Threshold) / Threshold) * 100 : 0;

        /// <summary>
        /// Formatted description for display
        /// </summary>
        public string FormattedDescription => string.IsNullOrEmpty(Description) 
            ? $"{Type} anomaly detected" 
            : Description;

        /// <summary>
        /// Short description for compact display
        /// </summary>
        public string ShortDescription
        {
            get
            {
                const int maxLength = 50;
                var desc = FormattedDescription;
                return desc.Length <= maxLength ? desc : desc.Substring(0, maxLength) + "...";
            }
        }

        /// <summary>
        /// Icon representation for the anomaly type
        /// </summary>
        public string TypeIcon => Type switch
        {
            AnomalyType.ErrorSpike => "📈",
            AnomalyType.ProcessUIDClustering => "🔗",
            AnomalyType.UserBehaviorAnomaly => "👤",
            AnomalyType.PerformanceDegradation => "⏰",
            AnomalyType.PatternDeviation => "🔄",
            _ => "❓"
        };
    }

    /// <summary>
    /// Types of anomalies that can be detected
    /// </summary>
    public enum AnomalyType
    {
        /// <summary>
        /// Sudden spike in error rate
        /// </summary>
        ErrorSpike,

        /// <summary>
        /// Multiple errors from same ProcessUID
        /// </summary>
        ProcessUIDClustering,

        /// <summary>
        /// Unusual user behavior patterns
        /// </summary>
        UserBehaviorAnomaly,

        /// <summary>
        /// Performance degradation indicators
        /// </summary>
        PerformanceDegradation,

        /// <summary>
        /// Deviation from normal patterns
        /// </summary>
        PatternDeviation
    }

    /// <summary>
    /// Severity levels for anomalies
    /// </summary>
    public enum AnomalySeverity
    {
        /// <summary>
        /// Low impact anomaly
        /// </summary>
        Low,

        /// <summary>
        /// Medium impact anomaly
        /// </summary>
        Medium,

        /// <summary>
        /// High impact anomaly requiring attention
        /// </summary>
        High,

        /// <summary>
        /// Critical anomaly requiring immediate action
        /// </summary>
        Critical
    }
} 
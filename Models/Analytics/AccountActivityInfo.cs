namespace Log_Parser_App.Models.Analytics
{
    using System;

    /// <summary>
    /// Represents account activity analysis for authentication monitoring
    /// </summary>
    public class AccountActivityInfo
    {
        /// <summary>
        /// Username of the account
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Total authentication attempts for this user
        /// </summary>
        public int TotalAttempts { get; set; }

        /// <summary>
        /// Number of failed authentication attempts
        /// </summary>
        public int FailedAttempts { get; set; }

        /// <summary>
        /// Failure rate as a percentage (0.0 to 1.0)
        /// </summary>
        public double FailureRate { get; set; }

        /// <summary>
        /// Last authentication activity timestamp
        /// </summary>
        public DateTime LastActivity { get; set; }

        /// <summary>
        /// Risk level assessment for this account
        /// </summary>
        public RiskLevel RiskLevel { get; set; }

        /// <summary>
        /// Number of successful authentication attempts
        /// </summary>
        public int SuccessfulAttempts => TotalAttempts - FailedAttempts;

        /// <summary>
        /// Success rate as a percentage (0.0 to 1.0)
        /// </summary>
        public double SuccessRate => TotalAttempts > 0 ? 1.0 - FailureRate : 0.0;

        /// <summary>
        /// Formatted failure description for UI display
        /// </summary>
        public string FailureDescription => $"{FailedAttempts} failures ({FailureRate:P0})";

        /// <summary>
        /// Background color based on risk level
        /// </summary>
        public string RiskBackgroundColor => RiskLevel switch
        {
            RiskLevel.High => "#4A2626",    // Dark red
            RiskLevel.Medium => "#4A3326",  // Dark orange
            RiskLevel.Low => "#3A3320",     // Dark yellow
            _ => "#2A2A2A"                  // Default gray
        };

        /// <summary>
        /// Risk level indicator color
        /// </summary>
        public string RiskColor => RiskLevel switch
        {
            RiskLevel.High => "#F15B5B",    // Red
            RiskLevel.Medium => "#F9A825",  // Orange
            RiskLevel.Low => "#FDD835",     // Yellow
            _ => "#888888"                  // Gray
        };

        /// <summary>
        /// Risk level display text
        /// </summary>
        public string RiskLevelText => RiskLevel switch
        {
            RiskLevel.High => "HIGH",
            RiskLevel.Medium => "MED",
            RiskLevel.Low => "LOW",
            _ => "UNK"
        };

        /// <summary>
        /// Time since last activity in human-readable format
        /// </summary>
        public string TimeSinceLastActivity
        {
            get
            {
                var timeDiff = DateTime.UtcNow - LastActivity;
                if (timeDiff.TotalMinutes < 1)
                    return "Now";
                if (timeDiff.TotalHours < 1)
                    return $"{(int)timeDiff.TotalMinutes}m ago";
                if (timeDiff.TotalDays < 1)
                    return $"{(int)timeDiff.TotalHours}h ago";
                return $"{(int)timeDiff.TotalDays}d ago";
            }
        }

        /// <summary>
        /// Detailed activity summary for tooltips or expanded view
        /// </summary>
        public string ActivitySummary => 
            $"{UserName}: {SuccessfulAttempts} successful, {FailedAttempts} failed " +
            $"({FailureRate:P1} failure rate). Last activity: {TimeSinceLastActivity}";

        /// <summary>
        /// Formatted user display name
        /// </summary>
        public string UserDisplayName => string.IsNullOrEmpty(UserName) ? "Unknown User" : UserName;

        /// <summary>
        /// Indicates if this account requires immediate attention
        /// </summary>
        public bool RequiresAttention => RiskLevel == RiskLevel.High || 
                                        (FailureRate > 0.5 && TotalAttempts > 5);

        /// <summary>
        /// Display text for UI showing user and risk level
        /// </summary>
        public string DisplayText => $"{UserDisplayName} ({RiskLevelText}): {FailureDescription}";

        /// <summary>
        /// UI color alias for XAML binding
        /// </summary>
        public string UIColor => RiskColor;
    }

    /// <summary>
    /// Risk level classification for account activity
    /// </summary>
    public enum RiskLevel
    {
        /// <summary>
        /// Low risk - normal authentication patterns
        /// </summary>
        Low,

        /// <summary>
        /// Medium risk - some authentication failures but within acceptable range
        /// </summary>
        Medium,

        /// <summary>
        /// High risk - high failure rate or suspicious patterns
        /// </summary>
        High
    }
} 
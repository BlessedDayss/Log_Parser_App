namespace Log_Parser_App.Models.Analytics
{
    using System;

    /// <summary>
    /// Represents the status and activity information of a RabbitMQ consumer
    /// </summary>
    public class ConsumerStatusInfo
    {
        /// <summary>
        /// Name or identifier of the consumer
        /// </summary>
        public string ConsumerName { get; set; } = string.Empty;

        /// <summary>
        /// Current status of the consumer
        /// </summary>
        public ConsumerStatus Status { get; set; }

        /// <summary>
        /// Total number of messages processed by this consumer
        /// </summary>
        public int MessageCount { get; set; }

        /// <summary>
        /// Error rate as a percentage (0.0 to 1.0)
        /// </summary>
        public double ErrorRate { get; set; }

        /// <summary>
        /// Last activity timestamp of this consumer
        /// </summary>
        public DateTime LastActivity { get; set; }

        /// <summary>
        /// Background color for UI display based on status
        /// </summary>
        public string StatusColor => Status switch
        {
            ConsumerStatus.Active => "#64F5A6",    // Green
            ConsumerStatus.Error => "#F15B5B",     // Red
            ConsumerStatus.Inactive => "#888888",  // Gray
            _ => "#DDDDDD"                          // Default
        };

        /// <summary>
        /// Icon representation for the consumer status
        /// </summary>
        public string StatusIcon => Status switch
        {
            ConsumerStatus.Active => "⚡",
            ConsumerStatus.Error => "⚠️",
            ConsumerStatus.Inactive => "⏸️",
            _ => "❓"
        };

        /// <summary>
        /// Formatted error rate as percentage string
        /// </summary>
        public string ErrorRateFormatted => $"{ErrorRate:P1}";

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
        /// Display text for UI showing consumer name and status
        /// </summary>
        public string DisplayText => $"{StatusIcon} {ConsumerName} ({Status})";

        /// <summary>
        /// UI color alias for XAML binding
        /// </summary>
        public string UIColor => StatusColor;
    }

    /// <summary>
    /// Enumeration of possible consumer statuses
    /// </summary>
    public enum ConsumerStatus
    {
        /// <summary>
        /// Consumer is actively processing messages without significant errors
        /// </summary>
        Active,

        /// <summary>
        /// Consumer has high error rate or recent critical errors
        /// </summary>
        Error,

        /// <summary>
        /// Consumer has not processed messages recently
        /// </summary>
        Inactive
    }
} 
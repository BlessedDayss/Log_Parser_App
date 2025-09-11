namespace Log_Parser_App.Models.Analytics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Represents a time bucket of warnings for timeline visualization
    /// </summary>
    public class WarningTimelineInfo
    {
        /// <summary>
        /// Timestamp representing the start of this time bucket
        /// </summary>
        public DateTime TimeStamp { get; set; }

        /// <summary>
        /// Total number of warnings in this time bucket
        /// </summary>
        public int WarningCount { get; set; }

        /// <summary>
        /// Collection of top warning messages in this time bucket
        /// </summary>
        public WarningItem[] TopWarnings { get; set; } = Array.Empty<WarningItem>();

        /// <summary>
        /// Relative height for timeline bar visualization (0.0 to 1.0)
        /// </summary>
        public double RelativeHeight { get; set; }

        /// <summary>
        /// Absolute height in pixels for timeline bar
        /// </summary>
        public int BarHeight => Math.Max(2, (int)(RelativeHeight * 50)); // 2px minimum, 50px maximum

        /// <summary>
        /// Tooltip text for this time bucket
        /// </summary>
        public string TooltipText
        {
            get
            {
                var timeFormatted = TimeStamp.ToString("MMM dd HH:mm");
                var warningsList = TopWarnings.Any() 
                    ? string.Join(", ", TopWarnings.Select(w => $"{w.Message} ({w.Count})"))
                    : "Various warnings";
                
                return $"{timeFormatted}: {WarningCount} warnings\n{warningsList}";
            }
        }

        /// <summary>
        /// Formatted time label for timeline axis
        /// </summary>
        public string TimeLabel
        {
            get
            {
                var now = DateTime.UtcNow;
                var timeDiff = now - TimeStamp;

                if (timeDiff.TotalDays > 1)
                    return TimeStamp.ToString("MM/dd");
                if (timeDiff.TotalHours > 1)
                    return TimeStamp.ToString("HH:mm");
                return TimeStamp.ToString("HH:mm");
            }
        }

        /// <summary>
        /// Summary of warnings for this time bucket
        /// </summary>
        public string WarningSummary
        {
            get
            {
                if (WarningCount == 0)
                    return "No warnings";
                
                if (TopWarnings.Length == 1)
                    return $"{WarningCount} warnings: {TopWarnings[0].Message}";
                
                if (TopWarnings.Length > 1)
                    return $"{WarningCount} warnings: {TopWarnings[0].Message} and {TopWarnings.Length - 1} more";
                
                return $"{WarningCount} warnings";
            }
        }

        /// <summary>
        /// Indicates if this time bucket has significant warning activity
        /// </summary>
        public bool HasSignificantActivity => WarningCount > 0;

        /// <summary>
        /// Color for the timeline bar based on warning intensity
        /// </summary>
        public string BarColor
        {
            get
            {
                if (WarningCount == 0)
                    return "#444444"; // Dark gray for no warnings
                
                if (RelativeHeight > 0.8)
                    return "#F15B5B"; // Red for high warning activity
                
                if (RelativeHeight > 0.5)
                    return "#F9A825"; // Orange for medium warning activity
                
                return "#FDD835"; // Yellow for low warning activity
            }
        }
    }

    /// <summary>
    /// Represents a specific warning message with its occurrence count
    /// </summary>
    public class WarningItem
    {
        /// <summary>
        /// Warning message text
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Number of times this warning occurred in the time bucket
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Percentage of total warnings this item represents
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// Shortened message for display in compact widgets
        /// </summary>
        public string MessageShort
        {
            get
            {
                if (string.IsNullOrEmpty(Message))
                    return "Unknown warning";

                const int maxLength = 40;
                return Message.Length <= maxLength 
                    ? Message 
                    : Message.Substring(0, maxLength) + "...";
            }
        }

        /// <summary>
        /// Formatted count and percentage for display
        /// </summary>
        public string CountFormatted => Count == 1 ? "1 time" : $"{Count} times";

        /// <summary>
        /// Formatted percentage for display
        /// </summary>
        public string PercentageFormatted => $"{Percentage:P0}";
    }
} 
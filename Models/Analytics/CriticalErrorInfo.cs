namespace Log_Parser_App.Models.Analytics
{
    using System;
    using System.Linq;

    /// <summary>
    /// Represents critical error information for dashboard display
    /// </summary>
    public class CriticalErrorInfo
    {
        /// <summary>
        /// Timestamp when the error occurred
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Username associated with the error
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Primary error message
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Full stack trace of the error
        /// </summary>
        public string StackTrace { get; set; } = string.Empty;

        /// <summary>
        /// Process UID associated with the error
        /// </summary>
        public string ProcessUID { get; set; } = string.Empty;

        /// <summary>
        /// Node or consumer that generated the error
        /// </summary>
        public string Node { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if this error is part of a group of identical errors
        /// </summary>
        public bool IsGroupedError { get; set; } = false;

        /// <summary>
        /// Relevance score for intelligent error prioritization (optional)
        /// </summary>
        public double RelevanceScore { get; set; } = 1.0;

        /// <summary>
        /// Shortened error message for display in compact widgets
        /// </summary>
        public string ErrorMessageShort
        {
            get
            {
                if (string.IsNullOrEmpty(ErrorMessage))
                    return "Unknown error";

                const int maxLength = 60;
                return ErrorMessage.Length <= maxLength 
                    ? ErrorMessage 
                    : ErrorMessage.Substring(0, maxLength) + "...";
            }
        }

        /// <summary>
        /// Time ago formatted string for UI display
        /// </summary>
        public string TimeAgo
        {
            get
            {
                var timeDiff = DateTime.UtcNow - Timestamp;
                if (timeDiff.TotalMinutes < 1)
                    return "Now";
                if (timeDiff.TotalMinutes < 60)
                    return $"{(int)timeDiff.TotalMinutes}m ago";
                if (timeDiff.TotalHours < 24)
                    return $"{(int)timeDiff.TotalHours}h ago";
                if (timeDiff.TotalDays < 7)
                    return $"{(int)timeDiff.TotalDays}d ago";
                return Timestamp.ToString("MM/dd");
            }
        }

        /// <summary>
        /// Formatted timestamp for detailed display
        /// </summary>
        public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

        /// <summary>
        /// Indicates whether this error has a stack trace available
        /// </summary>
        public bool HasStackTrace => !string.IsNullOrEmpty(StackTrace);

        /// <summary>
        /// Shortened stack trace for preview display
        /// </summary>
        public string StackTracePreview
        {
            get
            {
                if (string.IsNullOrEmpty(StackTrace))
                    return "No stack trace available";

                // Take first few lines of stack trace
                var lines = StackTrace.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length <= 3)
                    return StackTrace;

                return string.Join("\n", lines.Take(3).ToArray()) + "\n... (click to view full trace)";
            }
        }

        /// <summary>
        /// User display name or fallback if username is empty
        /// </summary>
        public string UserDisplayName => string.IsNullOrEmpty(UserName) ? "Unknown User" : UserName;

        /// <summary>
        /// Node display name or fallback if node is empty
        /// </summary>
        public string NodeDisplayName => string.IsNullOrEmpty(Node) ? "Unknown Node" : Node;

        /// <summary>
        /// Display text for UI showing time and short error message
        /// </summary>
        public string DisplayText => IsGroupedError ? 
            $"{TimeAgo}: ALL ERRORS THE SAME, SEE ERROR MESSAGE" : 
            $"{TimeAgo}: {ErrorMessageShort}";
    }
} 
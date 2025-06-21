using System;
using System.Collections.Generic;

namespace Log_Parser_App.Models
{
    /// <summary>
    /// Result of error analysis containing detected keywords, stack traces, and navigation info
    /// </summary>
    public class ErrorAnalysisResult
    {
        /// <summary>
        /// Detected error keywords with their positions and types
        /// </summary>
        public IEnumerable<ErrorKeywordMatch> Keywords { get; set; } = new List<ErrorKeywordMatch>();

        /// <summary>
        /// Parsed stack trace information
        /// </summary>
        public IEnumerable<StackTraceInfo> StackTraces { get; set; } = new List<StackTraceInfo>();

        /// <summary>
        /// Activity heatmap data for time-based visualization
        /// </summary>
        public ActivityHeatmapData? HeatmapData { get; set; }

        /// <summary>
        /// Navigation information for error traversal
        /// </summary>
        public ErrorNavigationInfo? Navigation { get; set; }

        /// <summary>
        /// Total number of errors detected
        /// </summary>
        public int TotalErrorCount { get; set; }

        /// <summary>
        /// Analysis timestamp
        /// </summary>
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Information about detected error keywords in log entries
    /// </summary>
    public class ErrorKeywordMatch
    {
        /// <summary>
        /// The matched keyword (Error, Exception, etc.)
        /// </summary>
        public string Keyword { get; set; } = string.Empty;

        /// <summary>
        /// Type of error for styling purposes
        /// </summary>
        public ErrorType ErrorType { get; set; }

        /// <summary>
        /// Position in the message where keyword was found
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// Length of the matched keyword
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Background color for highlighting
        /// </summary>
        public string BackgroundColor { get; set; } = string.Empty;

        /// <summary>
        /// The log entry containing this keyword
        /// </summary>
        public LogEntry LogEntry { get; set; } = null!;

        /// <summary>
        /// Index of this error in the overall error sequence
        /// </summary>
        public int ErrorIndex { get; set; }
    }

    /// <summary>
    /// Parsed stack trace information with structured data
    /// </summary>
    public class StackTraceInfo
    {
        /// <summary>
        /// The log entry containing the stack trace
        /// </summary>
        public LogEntry LogEntry { get; set; } = null!;

        /// <summary>
        /// Individual stack frames
        /// </summary>
        public IEnumerable<StackFrame> Frames { get; set; } = new List<StackFrame>();

        /// <summary>
        /// Exception type if detected
        /// </summary>
        public string? ExceptionType { get; set; }

        /// <summary>
        /// Exception message if detected
        /// </summary>
        public string? ExceptionMessage { get; set; }

        /// <summary>
        /// Whether the stack trace was successfully parsed
        /// </summary>
        public bool IsParsed { get; set; }
    }

    /// <summary>
    /// Individual stack frame within a stack trace
    /// </summary>
    public class StackFrame
    {
        /// <summary>
        /// Method name
        /// </summary>
        public string? Method { get; set; }

        /// <summary>
        /// Class or namespace
        /// </summary>
        public string? Class { get; set; }

        /// <summary>
        /// Source file name
        /// </summary>
        public string? FileName { get; set; }

        /// <summary>
        /// Line number in source file
        /// </summary>
        public int? LineNumber { get; set; }

        /// <summary>
        /// Raw stack frame text
        /// </summary>
        public string RawText { get; set; } = string.Empty;
    }

    /// <summary>
    /// Navigation information for error traversal
    /// </summary>
    public class ErrorNavigationInfo
    {
        /// <summary>
        /// Total number of errors available for navigation
        /// </summary>
        public int TotalErrors { get; set; }

        /// <summary>
        /// Currently selected error index (0-based)
        /// </summary>
        public int CurrentIndex { get; set; }

        /// <summary>
        /// Whether there is a previous error available
        /// </summary>
        public bool HasPrevious => CurrentIndex > 0;

        /// <summary>
        /// Whether there is a next error available
        /// </summary>
        public bool HasNext => CurrentIndex < TotalErrors - 1;

        /// <summary>
        /// Display text for current position (e.g., "Error 3 of 15")
        /// </summary>
        public string PositionText => TotalErrors > 0 ? $"Error {CurrentIndex + 1} of {TotalErrors}" : "No errors";

        /// <summary>
        /// Index of errors for quick access
        /// </summary>
        public IEnumerable<int> ErrorIndices { get; set; } = new List<int>();
    }

    /// <summary>
    /// Types of errors for categorization and styling
    /// </summary>
    public enum ErrorType
    {
        /// <summary>
        /// General error keyword
        /// </summary>
        Error,

        /// <summary>
        /// Exception-related keyword
        /// </summary>
        Exception,

        /// <summary>
        /// Database operation errors
        /// </summary>
        DatabaseError,

        /// <summary>
        /// Validation or invalid state errors
        /// </summary>
        ValidationError,

        /// <summary>
        /// Unknown or unclassified error
        /// </summary>
        Unknown
    }
} 
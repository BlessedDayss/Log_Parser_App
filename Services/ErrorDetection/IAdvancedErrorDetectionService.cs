using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Services.ErrorDetection
{
    /// <summary>
    /// Advanced error detection service for keyword-based analysis and error highlighting
    /// Implements pipeline pattern for extensible error processing
    /// </summary>
    public interface IAdvancedErrorDetectionService
    {
        /// <summary>
        /// Analyzes log entries for errors using keyword detection and stack trace parsing
        /// </summary>
        /// <param name="entries">Log entries to analyze</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Comprehensive error analysis result</returns>
        Task<ErrorAnalysisResult> AnalyzeErrorsAsync(
            IEnumerable<LogEntry> entries, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Highlights error keywords in log entries for UI display
        /// </summary>
        /// <param name="entries">Log entries to process</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Log entries with error highlighting information</returns>
        Task<IEnumerable<LogEntry>> HighlightErrorKeywordsAsync(
            IEnumerable<LogEntry> entries, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets navigation information for error traversal
        /// </summary>
        /// <param name="entries">Log entries containing errors</param>
        /// <param name="currentIndex">Current error index (0-based)</param>
        /// <returns>Navigation information for UI controls</returns>
        ErrorNavigationInfo GetErrorNavigation(IEnumerable<LogEntry> entries, int currentIndex);

        /// <summary>
        /// Generates activity heatmap data from log entries
        /// </summary>
        /// <param name="entries">Log entries to analyze</param>
        /// <param name="intervalMinutes">Time interval for grouping (default: 60 minutes)</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Activity heatmap data for visualization</returns>
        Task<ActivityHeatmapData> GenerateActivityHeatmapAsync(
            IEnumerable<LogEntry> entries,
            int intervalMinutes = 60,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Parses stack traces from log entries
        /// </summary>
        /// <param name="entries">Log entries potentially containing stack traces</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Parsed stack trace information</returns>
        Task<IEnumerable<StackTraceInfo>> ParseStackTracesAsync(
            IEnumerable<LogEntry> entries,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Filters log entries based on heatmap time selection
        /// </summary>
        /// <param name="entries">All log entries</param>
        /// <param name="selectedDataPoint">Selected heatmap data point</param>
        /// <returns>Filtered log entries for the selected time slot</returns>
        IEnumerable<LogEntry> FilterByHeatmapSelection(
            IEnumerable<LogEntry> entries, 
            HeatmapDataPoint selectedDataPoint);

        /// <summary>
        /// Gets error keywords that this service can detect
        /// </summary>
        /// <returns>List of detectable error keywords</returns>
        IReadOnlyList<string> GetDetectableKeywords();

        /// <summary>
        /// Gets error type classification for a keyword
        /// </summary>
        /// <param name="keyword">Error keyword to classify</param>
        /// <returns>Error type classification</returns>
        ErrorType ClassifyErrorKeyword(string keyword);

        /// <summary>
        /// Gets background color for error type highlighting
        /// </summary>
        /// <param name="errorType">Type of error</param>
        /// <returns>Hex color string for background highlighting</returns>
        string GetErrorHighlightColor(ErrorType errorType);

        /// <summary>
        /// Event fired when error analysis is completed
        /// </summary>
        event EventHandler<ErrorAnalysisCompletedEventArgs>? ErrorAnalysisCompleted;

        /// <summary>
        /// Event fired when error navigation changes
        /// </summary>
        event EventHandler<ErrorNavigationChangedEventArgs>? ErrorNavigationChanged;
    }

    /// <summary>
    /// Event arguments for error analysis completion
    /// </summary>
    public class ErrorAnalysisCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// Result of the error analysis
        /// </summary>
        public ErrorAnalysisResult Result { get; }

        /// <summary>
        /// Number of log entries analyzed
        /// </summary>
        public int EntriesAnalyzed { get; }

        /// <summary>
        /// Time taken for analysis
        /// </summary>
        public TimeSpan AnalysisTime { get; }

        /// <summary>
        /// Whether analysis was completed successfully
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Error message if analysis failed
        /// </summary>
        public string? ErrorMessage { get; }

        public ErrorAnalysisCompletedEventArgs(
            ErrorAnalysisResult result, 
            int entriesAnalyzed, 
            TimeSpan analysisTime, 
            bool isSuccess = true, 
            string? errorMessage = null)
        {
            Result = result;
            EntriesAnalyzed = entriesAnalyzed;
            AnalysisTime = analysisTime;
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// Event arguments for error navigation changes
    /// </summary>
    public class ErrorNavigationChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Updated navigation information
        /// </summary>
        public ErrorNavigationInfo Navigation { get; }

        /// <summary>
        /// Previously selected error index
        /// </summary>
        public int PreviousIndex { get; }

        /// <summary>
        /// Currently selected error index
        /// </summary>
        public int CurrentIndex { get; }

        /// <summary>
        /// The selected log entry
        /// </summary>
        public LogEntry? SelectedEntry { get; }

        public ErrorNavigationChangedEventArgs(
            ErrorNavigationInfo navigation, 
            int previousIndex, 
            int currentIndex, 
            LogEntry? selectedEntry = null)
        {
            Navigation = navigation;
            PreviousIndex = previousIndex;
            CurrentIndex = currentIndex;
            SelectedEntry = selectedEntry;
        }
    }

    /// <summary>
    /// Configuration for advanced error detection
    /// </summary>
    public class AdvancedErrorDetectionConfig
    {
        /// <summary>
        /// Keywords to detect for error classification
        /// </summary>
        public Dictionary<string, ErrorType> ErrorKeywords { get; set; } = new()
        {
            { "Error", ErrorType.Error },
            { "Exception", ErrorType.Exception },
            { "DbOperationException", ErrorType.DatabaseError },
            { "PostgresException", ErrorType.DatabaseError },
            { "Invalid", ErrorType.ValidationError },
            { "RootAlreadyExists", ErrorType.ValidationError }
        };

        /// <summary>
        /// Color mapping for error types
        /// </summary>
        public Dictionary<ErrorType, string> ErrorColors { get; set; } = new()
        {
            { ErrorType.Error, "#FFEBEE" },           // Light red
            { ErrorType.Exception, "#FFF3E0" },       // Light orange
            { ErrorType.DatabaseError, "#F3E5F5" },   // Light purple
            { ErrorType.ValidationError, "#FFFDE7" }, // Light yellow
            { ErrorType.Unknown, "#F5F5F5" }          // Light gray
        };

        /// <summary>
        /// Whether to enable case-sensitive keyword matching
        /// </summary>
        public bool CaseSensitive { get; set; } = false;

        /// <summary>
        /// Whether to enable stack trace parsing
        /// </summary>
        public bool EnableStackTraceParsing { get; set; } = true;

        /// <summary>
        /// Whether to enable activity heatmap generation
        /// </summary>
        public bool EnableActivityHeatmap { get; set; } = true;

        /// <summary>
        /// Maximum number of entries to process in a single batch
        /// </summary>
        public int MaxBatchSize { get; set; } = 10000;

        /// <summary>
        /// Timeout for async operations in milliseconds
        /// </summary>
        public int TimeoutMs { get; set; } = 30000;
    }
} 
using System;
using System.Collections.Generic;
using System.Linq;

namespace Log_Parser_App.Models
{
    /// <summary>
    /// Metadata extracted from log entries
    /// </summary>
    public class LogMetadata
    {
        /// <summary>
        /// Type of log format
        /// </summary>
        public LogFormatType LogType { get; set; }

        /// <summary>
        /// Total number of log entries
        /// </summary>
        public int TotalEntries { get; set; }

        /// <summary>
        /// Timestamp of the first log entry
        /// </summary>
        public DateTime FirstLogTime { get; set; }

        /// <summary>
        /// Timestamp of the last log entry
        /// </summary>
        public DateTime LastLogTime { get; set; }

        /// <summary>
        /// Distribution of log levels (level -> count)
        /// </summary>
        public Dictionary<string, int> LogLevels { get; set; } = new();

        /// <summary>
        /// Distribution of log sources (source -> count)
        /// </summary>
        public Dictionary<string, int> Sources { get; set; } = new();

        /// <summary>
        /// Distribution of error types (error type -> count)
        /// </summary>
        public Dictionary<string, int> ErrorTypes { get; set; } = new();

        /// <summary>
        /// Whether any entries contain stack traces
        /// </summary>
        public bool HasStackTraces { get; set; }

        /// <summary>
        /// Whether any entries contain correlation IDs
        /// </summary>
        public bool HasCorrelationIds { get; set; }

        /// <summary>
        /// Average length of log messages
        /// </summary>
        public double AverageMessageLength { get; set; }

        /// <summary>
        /// Longest log message found
        /// </summary>
        public string LongestMessage { get; set; } = string.Empty;

        /// <summary>
        /// Number of unique error messages
        /// </summary>
        public int UniqueErrorCount { get; set; }

        /// <summary>
        /// File size in bytes (if applicable)
        /// </summary>
        public long? FileSizeBytes { get; set; }

        /// <summary>
        /// Encoding detected/used for the file
        /// </summary>
        public string? FileEncoding { get; set; }

        /// <summary>
        /// Number of lines in the original file
        /// </summary>
        public int? TotalLines { get; set; }

        /// <summary>
        /// Number of lines that were successfully parsed
        /// </summary>
        public int? ParsedLines { get; set; }

        /// <summary>
        /// Number of lines that failed to parse
        /// </summary>
        public int? FailedLines { get; set; }

        /// <summary>
        /// Parsing success rate (0.0 to 1.0)
        /// </summary>
        public double? ParsingSuccessRate { get; set; }

        /// <summary>
        /// Custom properties specific to the log type
        /// </summary>
        public Dictionary<string, object> CustomProperties { get; set; } = new();

        /// <summary>
        /// Performance metrics for processing
        /// </summary>
        public Dictionary<string, double> PerformanceMetrics { get; set; } = new();

        /// <summary>
        /// Quality assessment scores
        /// </summary>
        public Dictionary<string, double> QualityScores { get; set; } = new();

        /// <summary>
        /// Time span covered by the log entries
        /// </summary>
        public TimeSpan TimeSpanCovered => LastLogTime - FirstLogTime;

        /// <summary>
        /// Average entries per hour
        /// </summary>
        public double EntriesPerHour => TimeSpanCovered.TotalHours > 0 ? TotalEntries / TimeSpanCovered.TotalHours : 0;

        /// <summary>
        /// Most frequent log level
        /// </summary>
        public string? MostFrequentLevel => LogLevels.Count > 0 ? 
            LogLevels.OrderByDescending(kvp => kvp.Value).First().Key : null;

        /// <summary>
        /// Most active source
        /// </summary>
        public string? MostActiveSource => Sources.Count > 0 ? 
            Sources.OrderByDescending(kvp => kvp.Value).First().Key : null;
    }
} 
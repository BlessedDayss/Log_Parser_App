using System.Collections.Generic;

namespace Log_Parser_App.Models
{
    /// <summary>
    /// Configuration options for log processing based on log type
    /// </summary>
    public class LogProcessingOptions
    {
        /// <summary>
        /// Type of log format this configuration applies to
        /// </summary>
        public LogFormatType LogType { get; set; }

        /// <summary>
        /// List of supported fields for this log type
        /// </summary>
        public List<string> SupportedFields { get; set; } = new();

        /// <summary>
        /// Default filter options for this log type
        /// </summary>
        public List<string> DefaultFilters { get; set; } = new();

        /// <summary>
        /// Whether this log type requires special parsing logic
        /// </summary>
        public bool RequiresSpecialParsing { get; set; }

        /// <summary>
        /// Whether this log type supports real-time monitoring
        /// </summary>
        public bool SupportsRealTimeMonitoring { get; set; }

        /// <summary>
        /// Maximum recommended file size for processing (in bytes)
        /// </summary>
        public long MaxFileSize { get; set; }

        /// <summary>
        /// Recommended batch size for processing entries
        /// </summary>
        public int RecommendedBatchSize { get; set; }

        /// <summary>
        /// Whether this log type supports structured logging
        /// </summary>
        public bool SupportsStructuredLogging { get; set; }

        /// <summary>
        /// Default encoding for reading log files
        /// </summary>
        public string DefaultEncoding { get; set; } = "UTF-8";

        /// <summary>
        /// Custom properties specific to this log type
        /// </summary>
        public Dictionary<string, object> CustomProperties { get; set; } = new();
    }
} 
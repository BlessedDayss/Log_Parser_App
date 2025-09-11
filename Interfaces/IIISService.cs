using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Interfaces
{
    /// <summary>
    /// Business logic service interface for IIS log operations
    /// Following Service pattern for clean architecture
    /// Single Responsibility: Business operations for IIS logs
    /// </summary>
    public interface IIISService
    {
        /// <summary>
        /// Load and process IIS logs from files or directory
        /// </summary>
        /// <param name="paths">File paths or directory path</param>
        /// <param name="isDirectory">True if path is directory</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Processed IIS entries with metadata</returns>
        Task<IISProcessingResult> ProcessIISLogsAsync(IEnumerable<string> paths, bool isDirectory = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create optimized view model for IIS data
        /// </summary>
        /// <param name="entries">IIS log entries</param>
        /// <param name="filePaths">Source file paths</param>
        /// <returns>IIS view model</returns>
        IISViewModel CreateViewModel(IEnumerable<IisLogEntry> entries, IEnumerable<string> filePaths);

        /// <summary>
        /// Apply complex filtering with multiple criteria
        /// </summary>
        /// <param name="entries">Source entries</param>
        /// <param name="criteria">Complex filter criteria</param>
        /// <returns>Filtered results</returns>
        IEnumerable<IisLogEntry> ApplyAdvancedFiltering(IEnumerable<IisLogEntry> entries, IISAdvancedFilterCriteria criteria);

        /// <summary>
        /// Get real-time analytics for IIS logs
        /// </summary>
        /// <param name="entries">IIS log entries</param>
        /// <returns>Analytics data</returns>
        IISAnalytics GetAnalytics(IEnumerable<IisLogEntry> entries);

        /// <summary>
        /// Export IIS data in various formats
        /// </summary>
        /// <param name="entries">IIS log entries</param>
        /// <param name="format">Export format</param>
        /// <param name="filePath">Output file path</param>
        /// <returns>Success status</returns>
        Task<bool> ExportDataAsync(IEnumerable<IisLogEntry> entries, IISExportFormat format, string filePath);

        /// <summary>
        /// Validate IIS log files and get detailed validation report
        /// </summary>
        /// <param name="filePaths">File paths to validate</param>
        /// <returns>Detailed validation report</returns>
        Task<IISValidationReport> ValidateFilesAsync(IEnumerable<string> filePaths);
    }

    /// <summary>
    /// IIS processing result with metadata
    /// </summary>
    public class IISProcessingResult
    {
        public IEnumerable<IisLogEntry> Entries { get; set; } = Enumerable.Empty<IisLogEntry>();
        public IISLogStatistics Statistics { get; set; } = new();
        public IISProcessingMetadata Metadata { get; set; } = new();
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    /// <summary>
    /// Processing metadata for IIS operations
    /// </summary>
    public class IISProcessingMetadata
    {
        public int FilesProcessed { get; set; }
        public int FilesSkipped { get; set; }
        public int EntriesProcessed { get; set; }
        public int EntriesFailed { get; set; }
        public double SuccessRate { get; set; }
        public List<string> ProcessedFiles { get; set; } = new();
        public List<string> SkippedFiles { get; set; } = new();
        public Dictionary<string, string> FileErrors { get; set; } = new();
    }

    /// <summary>
    /// View model for IIS logs with UI optimization
    /// </summary>
    public class IISViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public IEnumerable<IisLogEntry> AllEntries { get; set; } = Enumerable.Empty<IisLogEntry>();
        public IEnumerable<IisLogEntry> FilteredEntries { get; set; } = Enumerable.Empty<IisLogEntry>();
        public IISLogStatistics Statistics { get; set; } = new();
        public bool HasData => AllEntries.Any();
    }

    /// <summary>
    /// Advanced filtering criteria for complex scenarios
    /// </summary>
    public class IISAdvancedFilterCriteria
    {
        public List<IISFilterGroup> FilterGroups { get; set; } = new();
        public IISFilterOperation GlobalOperation { get; set; } = IISFilterOperation.And;
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public List<int> StatusCodes { get; set; } = new();
        public List<string> HttpMethods { get; set; } = new();
        public List<string> IPAddresses { get; set; } = new();
        public int? MinResponseTime { get; set; }
        public int? MaxResponseTime { get; set; }
    }

    /// <summary>
    /// Filter group for complex filtering
    /// </summary>
    public class IISFilterGroup
    {
        public List<IISFilterCriterion> Criteria { get; set; } = new();
        public IISFilterOperation Operation { get; set; } = IISFilterOperation.And;
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Real-time analytics for IIS logs
    /// </summary>
    public class IISAnalytics
    {
        public Dictionary<string, int> TopErrorPages { get; set; } = new();
        public Dictionary<string, int> TopIPAddresses { get; set; } = new();
        public Dictionary<string, int> TopUserAgents { get; set; } = new();
        public Dictionary<int, double> ResponseTimeByHour { get; set; } = new();
        public Dictionary<int, int> RequestsByHour { get; set; } = new();
        public List<IISPerformanceAlert> PerformanceAlerts { get; set; } = new();
        public List<IISSecurityAlert> SecurityAlerts { get; set; } = new();
    }

    /// <summary>
    /// Performance alert for IIS monitoring
    /// </summary>
    public class IISPerformanceAlert
    {
        public string AlertType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
        public string Severity { get; set; } = string.Empty;
        public Dictionary<string, object> Data { get; set; } = new();
    }

    /// <summary>
    /// Security alert for IIS monitoring
    /// </summary>
    public class IISSecurityAlert
    {
        public string AlertType { get; set; } = string.Empty;
        public string IPAddress { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
        public Dictionary<string, object> Evidence { get; set; } = new();
    }

    /// <summary>
    /// File validation report
    /// </summary>
    public class IISValidationReport
    {
        public Dictionary<string, bool> FileValidationResults { get; set; } = new();
        public Dictionary<string, List<string>> ValidationErrors { get; set; } = new();
        public Dictionary<string, IISFileMetadata> FileMetadata { get; set; } = new();
        public bool AllFilesValid => FileValidationResults.Values.All(v => v);
        public int ValidFilesCount => FileValidationResults.Values.Count(v => v);
        public int InvalidFilesCount => FileValidationResults.Values.Count(v => !v);
    }

    /// <summary>
    /// File metadata for IIS logs
    /// </summary>
    public class IISFileMetadata
    {
        public long FileSize { get; set; }
        public DateTimeOffset FileCreated { get; set; }
        public DateTimeOffset FileModified { get; set; }
        public int EstimatedEntryCount { get; set; }
        public string IISVersion { get; set; } = string.Empty;
        public List<string> FieldNames { get; set; } = new();
    }

    /// <summary>
    /// Export format options
    /// </summary>
    public enum IISExportFormat
    {
        Csv,
        Json,
        Excel,
        Xml,
        Html
    }
} 
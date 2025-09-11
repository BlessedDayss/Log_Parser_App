using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Interfaces
{
    /// <summary>
    /// Repository interface for IIS log data access operations
    /// Following Repository pattern for clean architecture
    /// Single Responsibility: Data access for IIS logs only
    /// </summary>
    public interface IIISRepository
    {
        /// <summary>
        /// Load IIS entries from multiple file paths asynchronously
        /// </summary>
        /// <param name="filePaths">Collection of file paths to load</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of IIS log entries</returns>
        Task<IEnumerable<IisLogEntry>> LoadIISLogsAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default);

        /// <summary>
        /// Load IIS entries from a single file path asynchronously
        /// </summary>
        /// <param name="filePath">File path to load</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of IIS log entries</returns>
        Task<IEnumerable<IisLogEntry>> LoadIISLogAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validate if files are IIS log format
        /// </summary>
        /// <param name="filePaths">File paths to validate</param>
        /// <returns>Dictionary with file path as key and validation result as value</returns>
        Task<Dictionary<string, bool>> ValidateIISFilesAsync(IEnumerable<string> filePaths);

        /// <summary>
        /// Get IIS log statistics for given entries
        /// </summary>
        /// <param name="entries">IIS log entries</param>
        /// <returns>Statistics object</returns>
        IISLogStatistics GetStatistics(IEnumerable<IisLogEntry> entries);

        /// <summary>
        /// Filter IIS entries based on criteria
        /// </summary>
        /// <param name="entries">Source entries</param>
        /// <param name="criteria">Filter criteria</param>
        /// <returns>Filtered entries</returns>
        IEnumerable<IisLogEntry> FilterEntries(IEnumerable<IisLogEntry> entries, IISFilterCriteria criteria);
    }

    /// <summary>
    /// IIS log statistics data model
    /// </summary>
    public class IISLogStatistics
    {
        public int TotalRequests { get; set; }
        public int ErrorRequests { get; set; }
        public int InfoRequests { get; set; }
        public int RedirectRequests { get; set; }
        public double ErrorRate { get; set; }
        public DateTimeOffset? FirstLogTime { get; set; }
        public DateTimeOffset? LastLogTime { get; set; }
        public TimeSpan LogDuration { get; set; }
        public Dictionary<int, int> StatusCodeDistribution { get; set; } = new();
        public Dictionary<string, int> MethodDistribution { get; set; } = new();
        public Dictionary<string, int> IPAddressDistribution { get; set; } = new();
        public long TotalBytesTransferred { get; set; }
        public double AverageResponseTime { get; set; }
    }

    /// <summary>
    /// IIS filter criteria container
    /// </summary>
    public class IISFilterCriteria
    {
        public List<IISFilterCriterion> Criteria { get; set; } = new();
        public IISFilterOperation Operation { get; set; } = IISFilterOperation.And;
    }

    /// <summary>
    /// Filter operation type
    /// </summary>
    public enum IISFilterOperation
    {
        And,
        Or
    }
} 
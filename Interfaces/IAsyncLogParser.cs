using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Interfaces
{
    /// <summary>
    /// Interface for asynchronous log parsing with streaming support
    /// Designed for performance optimization with large files
    /// </summary>
    public interface IAsyncLogParser
    {
        /// <summary>
        /// Parse log file asynchronously using streaming approach
        /// Returns IAsyncEnumerable for memory-efficient processing
        /// </summary>
        /// <param name="filePath">Path to the log file</param>
        /// <param name="cancellationToken">Cancellation token for operation cancellation</param>
        /// <returns>Async enumerable of log entries</returns>
        IAsyncEnumerable<LogEntry> ParseAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get current parsing progress information
        /// </summary>
        /// <returns>Progress information</returns>
        Task<LogParsingProgress> GetProgressAsync();

        /// <summary>
        /// Estimate total number of lines in file for progress calculation
        /// </summary>
        /// <param name="filePath">Path to the log file</param>
        /// <returns>Estimated line count</returns>
        Task<long> EstimateLinesTotalAsync(string filePath);
    }
} 
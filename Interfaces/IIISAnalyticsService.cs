using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Interfaces
{
    /// <summary>
    /// Service interface for IIS-specific analytics processing
    /// Implements Hybrid Memory-Stream Architecture for optimal performance
    /// </summary>
    public interface IIISAnalyticsService
    {
        /// <summary>
        /// Process IIS log entries and generate analytics with progress reporting
        /// </summary>
        /// <param name="logEntries">IIS log entries to analyze</param>
        /// <param name="progress">Progress reporting for UI updates</param>
        /// <param name="cancellationToken">Cancellation token for operation cancellation</param>
        /// <returns>Complete IIS analytics results</returns>
        Task<IISAnalyticsResult> ProcessAnalyticsAsync(
            IEnumerable<IisLogEntry> logEntries,
            IProgress<AnalyticsProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get TOP 3 HTTP status codes with counts
        /// </summary>
        Task<IISStatusAnalysis[]> GetTopStatusCodesAsync(
            IEnumerable<IisLogEntry> logEntries,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get TOP 3 longest requests by time-taken
        /// </summary>
        Task<IISLongestRequest[]> GetLongestRequestsAsync(
            IEnumerable<IisLogEntry> logEntries,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get HTTP methods distribution
        /// </summary>
        Task<IISMethodDistribution[]> GetHttpMethodsDistributionAsync(
            IEnumerable<IisLogEntry> logEntries,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get TOP 3 users by request count
        /// </summary>
        Task<IISUserActivity[]> GetTopUsersAsync(
            IEnumerable<IisLogEntry> logEntries,
            CancellationToken cancellationToken = default);
    }
} 
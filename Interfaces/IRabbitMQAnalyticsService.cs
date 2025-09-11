namespace Log_Parser_App.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Log_Parser_App.Models;
    using Log_Parser_App.Models.Analytics;

    /// <summary>
    /// Interface for RabbitMQ analytics calculations and dashboard data generation
    /// </summary>
    public interface IRabbitMQAnalyticsService
    {
        /// <summary>
        /// Analyzes RabbitMQ consumers and returns their status information
        /// </summary>
        /// <param name="entries">Collection of RabbitMQ log entries to analyze</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Array of consumer status information</returns>
        Task<ConsumerStatusInfo[]> GetActiveConsumersAsync(
            IEnumerable<RabbitMqLogEntry> entries, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the most recent critical errors from RabbitMQ logs
        /// </summary>
        /// <param name="entries">Collection of RabbitMQ log entries to analyze</param>
        /// <param name="count">Maximum number of critical errors to return (default: 5)</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Array of critical error information, ordered by recency</returns>
        Task<CriticalErrorInfo[]> GetRecentCriticalErrorsAsync(
            IEnumerable<RabbitMqLogEntry> entries, 
            int count = 5,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Analyzes user account activity patterns, focusing on authentication issues
        /// </summary>
        /// <param name="entries">Collection of RabbitMQ log entries to analyze</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Array of account activity information with risk assessment</returns>
        Task<AccountActivityInfo[]> GetAccountActivityAnalysisAsync(
            IEnumerable<RabbitMqLogEntry> entries,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a timeline of warning-level messages for trend analysis
        /// </summary>
        /// <param name="entries">Collection of RabbitMQ log entries to analyze</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Array of warning timeline information bucketed by time</returns>
        Task<WarningTimelineInfo[]> GetSystemWarningsTimelineAsync(
            IEnumerable<RabbitMqLogEntry> entries,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs anomaly detection and pattern analysis on RabbitMQ logs
        /// </summary>
        /// <param name="entries">Collection of RabbitMQ log entries to analyze</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Anomaly insights with detected patterns and recommendations</returns>
        Task<AnomalyInsightInfo> GetAnomaliesInsightAsync(
            IEnumerable<RabbitMqLogEntry> entries,
            CancellationToken cancellationToken = default);
    }
} 
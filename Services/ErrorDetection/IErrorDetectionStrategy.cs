using System.Collections.Generic;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Services.ErrorDetection
{
    /// <summary>
    /// Strategy interface for detecting errors in different log types
    /// Implements Strategy pattern for SOLID compliance
    /// </summary>
    public interface IErrorDetectionStrategy
    {
        /// <summary>
        /// The log format type this strategy handles
        /// </summary>
        LogFormatType SupportedLogType { get; }

        /// <summary>
        /// Detects error entries from standard log entries
        /// </summary>
        /// <param name="logEntries">Collection of log entries to analyze</param>
        /// <returns>Collection of entries that are considered errors</returns>
        Task<IEnumerable<LogEntry>> DetectErrorsAsync(IEnumerable<LogEntry> logEntries);

        /// <summary>
        /// Checks if a single log entry is considered an error
        /// </summary>
        /// <param name="logEntry">Log entry to check</param>
        /// <returns>True if the entry is considered an error</returns>
        bool IsError(LogEntry logEntry);

        /// <summary>
        /// Gets error criteria description for this strategy
        /// </summary>
        /// <returns>Human-readable description of error detection criteria</returns>
        string GetErrorCriteriaDescription();
    }

    /// <summary>
    /// Specialized strategy interface for IIS log error detection
    /// </summary>
    public interface IIISErrorDetectionStrategy : IErrorDetectionStrategy
    {
        /// <summary>
        /// Detects error entries from IIS log entries
        /// </summary>
        /// <param name="iisLogEntries">Collection of IIS log entries to analyze</param>
        /// <returns>Collection of IIS entries that are considered errors</returns>
        Task<IEnumerable<IisLogEntry>> DetectIISErrorsAsync(IEnumerable<IisLogEntry> iisLogEntries);

        /// <summary>
        /// Checks if a single IIS log entry is considered an error
        /// </summary>
        /// <param name="iisLogEntry">IIS log entry to check</param>
        /// <returns>True if the entry is considered an error</returns>
        bool IsIISError(IisLogEntry iisLogEntry);
    }

    /// <summary>
    /// Specialized strategy interface for RabbitMQ log error detection
    /// </summary>
    public interface IRabbitMQErrorDetectionStrategy : IErrorDetectionStrategy
    {
        /// <summary>
        /// Detects error entries from RabbitMQ log entries
        /// </summary>
        /// <param name="rabbitMqLogEntries">Collection of RabbitMQ log entries to analyze</param>
        /// <returns>Collection of RabbitMQ entries that are considered errors</returns>
        Task<IEnumerable<RabbitMqLogEntry>> DetectRabbitMQErrorsAsync(IEnumerable<RabbitMqLogEntry> rabbitMqLogEntries);

        /// <summary>
        /// Checks if a single RabbitMQ log entry is considered an error
        /// </summary>
        /// <param name="rabbitMqLogEntry">RabbitMQ log entry to check</param>
        /// <returns>True if the entry is considered an error</returns>
        bool IsRabbitMQError(RabbitMqLogEntry rabbitMqLogEntry);
    }
} 
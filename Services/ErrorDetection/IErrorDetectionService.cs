using System.Collections.Generic;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Services.ErrorDetection
{
    /// <summary>
    /// Service for detecting errors in different types of log entries
    /// Follows SOLID principles with Strategy pattern for different log types
    /// </summary>
    public interface IErrorDetectionService
    {
        /// <summary>
        /// Detects error entries from a collection of log entries based on the log format type
        /// </summary>
        /// <param name="logEntries">Collection of log entries to analyze</param>
        /// <param name="logFormatType">Type of log format (Standard, IIS, RabbitMQ)</param>
        /// <returns>Collection of entries that are considered errors</returns>
        Task<IEnumerable<LogEntry>> DetectErrorsAsync(IEnumerable<LogEntry> logEntries, LogFormatType logFormatType);

        /// <summary>
        /// Detects error entries from IIS log entries
        /// </summary>
        /// <param name="iisLogEntries">Collection of IIS log entries to analyze</param>
        /// <returns>Collection of IIS entries that are considered errors</returns>
        Task<IEnumerable<IisLogEntry>> DetectIISErrorsAsync(IEnumerable<IisLogEntry> iisLogEntries);

        /// <summary>
        /// Detects error entries from RabbitMQ log entries
        /// </summary>
        /// <param name="rabbitMqLogEntries">Collection of RabbitMQ log entries to analyze</param>
        /// <returns>Collection of RabbitMQ entries that are considered errors</returns>
        Task<IEnumerable<RabbitMqLogEntry>> DetectRabbitMQErrorsAsync(IEnumerable<RabbitMqLogEntry> rabbitMqLogEntries);

        /// <summary>
        /// Checks if a single log entry is considered an error based on the log format type
        /// </summary>
        /// <param name="logEntry">Log entry to check</param>
        /// <param name="logFormatType">Type of log format</param>
        /// <returns>True if the entry is considered an error</returns>
        bool IsError(LogEntry logEntry, LogFormatType logFormatType);

        /// <summary>
        /// Checks if a single IIS log entry is considered an error
        /// </summary>
        /// <param name="iisLogEntry">IIS log entry to check</param>
        /// <returns>True if the entry is considered an error</returns>
        bool IsIISError(IisLogEntry iisLogEntry);

        /// <summary>
        /// Checks if a single RabbitMQ log entry is considered an error
        /// </summary>
        /// <param name="rabbitMqLogEntry">RabbitMQ log entry to check</param>
        /// <returns>True if the entry is considered an error</returns>
        bool IsRabbitMQError(RabbitMqLogEntry rabbitMqLogEntry);

        /// <summary>
        /// Gets the error detection strategy for a specific log format type
        /// </summary>
        /// <param name="logFormatType">Type of log format</param>
        /// <returns>Error detection strategy instance</returns>
        IErrorDetectionStrategy GetStrategy(LogFormatType logFormatType);
    }
} 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.ErrorDetection
{
    /// <summary>
    /// Error detection strategy for RabbitMQ log entries
    /// Detects errors based on EffectiveLevel containing "error" or "fatal"
    /// </summary>
    public class RabbitMQLogErrorDetectionStrategy : BaseErrorDetectionStrategy, IRabbitMQErrorDetectionStrategy
    {
        private static readonly string[] ErrorLevels = { "error", "fatal", "critical" };
        private static readonly string[] WarningLevels = { "warn", "warning" };

        public RabbitMQLogErrorDetectionStrategy(ILogger<RabbitMQLogErrorDetectionStrategy> logger) 
            : base(logger)
        {
        }

        /// <summary>
        /// The log format type this strategy handles
        /// </summary>
        public override LogFormatType SupportedLogType => LogFormatType.RabbitMQ;

        /// <summary>
        /// Checks if a single log entry is considered an error
        /// For RabbitMQ logs: delegates to RabbitMQ-specific method for proper handling
        /// </summary>
        /// <param name="logEntry">Log entry to check</param>
        /// <returns>True if the entry is considered an error</returns>
        public override bool IsError(LogEntry logEntry)
        {
            // For generic LogEntry, check both Level and Message fields
            if (logEntry == null)
                return false;

            try
            {
                // Check Level field first
                if (SafeStringEqualsAny(logEntry.Level, ErrorLevels))
                    return true;

                // Check message content for RabbitMQ-specific error indicators
                return ContainsRabbitMQErrorIndicators(logEntry.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking if RabbitMQ log entry is error");
                return false;
            }
        }

        /// <summary>
        /// Checks if a single RabbitMQ log entry is considered an error
        /// </summary>
        /// <param name="rabbitMqLogEntry">RabbitMQ log entry to check</param>
        /// <returns>True if the entry is considered an error</returns>
        public bool IsRabbitMQError(RabbitMqLogEntry rabbitMqLogEntry)
        {
            try
            {
                if (rabbitMqLogEntry == null)
                    return false;

                // Check EffectiveLevel field for error indicators
                var effectiveLevel = rabbitMqLogEntry.EffectiveLevel?.ToLowerInvariant();
                
                if (SafeStringEqualsAny(effectiveLevel, ErrorLevels))
                {
                    _logger.LogTrace("RabbitMQ log entry identified as error: Level={Level}, Message={Message}", 
                        rabbitMqLogEntry.EffectiveLevel, 
                        rabbitMqLogEntry.Message?.Substring(0, Math.Min(rabbitMqLogEntry.Message.Length, 100)));
                    return true;
                }

                // Check for RabbitMQ-specific error patterns in message
                if (ContainsRabbitMQErrorIndicators(rabbitMqLogEntry.Message))
                    return true;

                // Check if it's a warning that should be treated as error in certain contexts
                if (SafeStringEqualsAny(effectiveLevel, WarningLevels) && 
                    ContainsCriticalRabbitMQWarnings(rabbitMqLogEntry.Message))
                {
                    _logger.LogTrace("RabbitMQ log entry identified as critical warning: Level={Level}, Message={Message}", 
                        rabbitMqLogEntry.EffectiveLevel, 
                        rabbitMqLogEntry.Message?.Substring(0, Math.Min(rabbitMqLogEntry.Message.Length, 100)));
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking if RabbitMQ log entry is error");
                return false;
            }
        }

        /// <summary>
        /// Detects error entries from RabbitMQ log entries
        /// </summary>
        /// <param name="rabbitMqLogEntries">Collection of RabbitMQ log entries to analyze</param>
        /// <returns>Collection of RabbitMQ entries that are considered errors</returns>
        public async Task<IEnumerable<RabbitMqLogEntry>> DetectRabbitMQErrorsAsync(IEnumerable<RabbitMqLogEntry> rabbitMqLogEntries)
        {
            try
            {
                var entries = rabbitMqLogEntries as RabbitMqLogEntry[] ?? rabbitMqLogEntries.ToArray();
                
                _logger.LogDebug("Starting RabbitMQ log error detection for {Count} entries", entries.Length);

                var errorEntries = await Task.Run(() => 
                    entries.Where(IsRabbitMQError).ToList());

                // Log statistics by error type
                var fatalErrors = errorEntries.Count(e => SafeStringEquals(e.EffectiveLevel, "fatal"));
                var errors = errorEntries.Count(e => SafeStringEquals(e.EffectiveLevel, "error"));
                var criticalWarnings = errorEntries.Count - fatalErrors - errors;

                _logger.LogInformation("RabbitMQ log error detection completed: {TotalErrors} errors ({Fatal} fatal, {Errors} error, {CriticalWarnings} critical warnings) from {TotalEntries} entries",
                    errorEntries.Count, fatalErrors, errors, criticalWarnings, entries.Length);

                LogDetectionStatistics(entries.Length, errorEntries.Count);

                return errorEntries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during RabbitMQ error detection");
                return Enumerable.Empty<RabbitMqLogEntry>();
            }
        }

        /// <summary>
        /// Gets error criteria description for this strategy
        /// </summary>
        /// <returns>Human-readable description of error detection criteria</returns>
        public override string GetErrorCriteriaDescription()
        {
            return "RabbitMQ logs: EffectiveLevel contains 'error', 'fatal', or 'critical', or message contains RabbitMQ-specific error indicators";
        }

        /// <summary>
        /// Enhanced validation for RabbitMQ log entries
        /// </summary>
        /// <param name="logEntry">Log entry to validate</param>
        /// <returns>True if entry is valid for RabbitMQ error detection</returns>
        protected override bool IsValidLogEntry(LogEntry logEntry)
        {
            if (!base.IsValidLogEntry(logEntry))
                return false;

            // RabbitMQ logs should have at minimum a message or level
            return !string.IsNullOrEmpty(logEntry.Message) || !string.IsNullOrEmpty(logEntry.Level);
        }

        /// <summary>
        /// Checks if the message contains RabbitMQ-specific error indicators
        /// </summary>
        /// <param name="message">Message to check</param>
        /// <returns>True if message contains RabbitMQ error indicators</returns>
        private bool ContainsRabbitMQErrorIndicators(string? message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            var rabbitMQErrorIndicators = new[]
            {
                "connection_closed", "channel_closed", "queue_error", "exchange_error",
                "binding_error", "consumer_error", "publisher_error", "timeout",
                "authentication_failure", "authorization_failure", "permission_denied",
                "resource_alarm", "disk_alarm", "memory_alarm", "node_down",
                "cluster_partition", "failed_to_start", "shutdown_error"
            };

            var lowerMessage = message.ToLowerInvariant();
            return rabbitMQErrorIndicators.Any(indicator => lowerMessage.Contains(indicator));
        }

        /// <summary>
        /// Checks if the message contains critical RabbitMQ warnings that should be treated as errors
        /// </summary>
        /// <param name="message">Message to check</param>
        /// <returns>True if message contains critical warning indicators</returns>
        private bool ContainsCriticalRabbitMQWarnings(string? message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            var criticalWarningIndicators = new[]
            {
                "high_memory_watermark", "disk_space_alarm", "cluster_partition_warning",
                "node_unreachable", "connection_limit_reached", "queue_overflow",
                "message_dropped", "publisher_blocked"
            };

            var lowerMessage = message.ToLowerInvariant();
            return criticalWarningIndicators.Any(indicator => lowerMessage.Contains(indicator));
        }

        /// <summary>
        /// Specialized error detection for mixed RabbitMQ log formats
        /// </summary>
        /// <param name="logEntries">Collection of log entries to analyze</param>
        /// <returns>Collection of entries that are considered errors</returns>
        public override async Task<IEnumerable<LogEntry>> DetectErrorsAsync(IEnumerable<LogEntry> logEntries)
        {
            var entries = logEntries as LogEntry[] ?? logEntries.ToArray();
            
            _logger.LogDebug("Starting RabbitMQ-compatible log error detection for {Count} entries", entries.Length);

            var errorEntries = await base.DetectErrorsAsync(entries);
            var errorList = errorEntries.ToList();

            // Log additional statistics for RabbitMQ logs
            var levelBasedErrors = entries.Count(e => SafeStringEqualsAny(e.Level, ErrorLevels));
            var messageBasedErrors = errorList.Count - levelBasedErrors;

            _logger.LogInformation("RabbitMQ-compatible log error detection completed: {TotalErrors} errors ({LevelBased} level-based, {MessageBased} message-based) from {TotalEntries} entries",
                errorList.Count, levelBasedErrors, messageBasedErrors, entries.Length);

            LogDetectionStatistics(entries.Length, errorList.Count);

            return errorList;
        }
    }
} 
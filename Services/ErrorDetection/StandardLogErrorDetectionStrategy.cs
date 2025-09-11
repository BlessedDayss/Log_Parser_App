using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.ErrorDetection
{
    /// <summary>
    /// Error detection strategy for standard log entries
    /// Detects errors based on Level field containing "error"
    /// </summary>
    public class StandardLogErrorDetectionStrategy : BaseErrorDetectionStrategy
    {
        private static readonly string[] ErrorLevels = { "error", "fatal", "critical" };

        public StandardLogErrorDetectionStrategy(ILogger<StandardLogErrorDetectionStrategy> logger) 
            : base(logger)
        {
        }

        /// <summary>
        /// The log format type this strategy handles
        /// </summary>
        public override LogFormatType SupportedLogType => LogFormatType.Standard;

        /// <summary>
        /// Checks if a single log entry is considered an error
        /// For standard logs: Level must be "error", "fatal", or "critical"
        /// </summary>
        /// <param name="logEntry">Log entry to check</param>
        /// <returns>True if the entry is considered an error</returns>
        public override bool IsError(LogEntry logEntry)
        {
            try
            {
                if (!IsValidLogEntry(logEntry))
                    return false;

                // CRITICAL: Always check for "0 Error" false positives FIRST, regardless of Level
                if (!string.IsNullOrEmpty(logEntry.Message))
                {
                    var lowerMessage = logEntry.Message.ToLowerInvariant();
                    if (lowerMessage.Contains("0 error") || lowerMessage.Contains("0 errors"))
                    {
                        _logger.LogTrace("Standard log entry excluded due to '0 Error' pattern: Level={Level}, Message={Message}", 
                            logEntry.Level, logEntry.Message.Substring(0, Math.Min(logEntry.Message.Length, 100)));
                        return false;
                    }
                }

                // Check if Level field indicates an error
                if (SafeStringEqualsAny(logEntry.Level, ErrorLevels))
                {
                    _logger.LogTrace("Standard log entry identified as error: Level={Level}, Message={Message}", 
                        logEntry.Level, logEntry.Message?.Substring(0, Math.Min(logEntry.Message.Length, 100)));
                    return true;
                }

                // Additional check: look for error indicators in the message ONLY if Level is missing or NULL
                // Do NOT check INFO/DEBUG levels - they should stay as INFO/DEBUG
                if (string.IsNullOrEmpty(logEntry.Level))
                {
                    return ContainsErrorIndicators(logEntry.Message);
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking if standard log entry is error");
                return false;
            }
        }

        /// <summary>
        /// Gets error criteria description for this strategy
        /// </summary>
        /// <returns>Human-readable description of error detection criteria</returns>
        public override string GetErrorCriteriaDescription()
        {
            return "Standard logs: Level field contains 'error', 'fatal', or 'critical', or message contains error indicators when level is missing/generic";
        }

        /// <summary>
        /// Enhanced validation for standard log entries
        /// </summary>
        /// <param name="logEntry">Log entry to validate</param>
        /// <returns>True if entry is valid for standard error detection</returns>
        protected override bool IsValidLogEntry(LogEntry logEntry)
        {
            if (!base.IsValidLogEntry(logEntry))
                return false;

            // Standard logs should have at minimum a message or level
            if (string.IsNullOrEmpty(logEntry.Message) && string.IsNullOrEmpty(logEntry.Level))
            {
                _logger.LogTrace("Standard log entry missing both message and level fields");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if the message contains error indicators
        /// </summary>
        /// <param name="message">Message to check</param>
        /// <returns>True if message contains error indicators</returns>
        private bool ContainsErrorIndicators(string? message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            var lowerMessage = message.ToLowerInvariant();

            // CRITICAL: Exclude "0 Error" false positives
            if (lowerMessage.Contains("0 error") || lowerMessage.Contains("0 errors"))
            {
                _logger.LogTrace("Standard log entry excluded due to '0 Error' pattern: {MessagePreview}", 
                    message.Substring(0, Math.Min(message.Length, 100)));
                return false;
            }

            var errorIndicators = new[]
            {
                "error", "exception", "failed", "failure", "fault", "critical", 
                "fatal", "panic", "crash", "abort", "terminated", "timeout",
                "invalid", "corrupt", "broken", "unavailable", "unreachable"
            };

            var hasErrorIndicator = errorIndicators.Any(indicator => lowerMessage.Contains(indicator));

            if (hasErrorIndicator)
            {
                _logger.LogTrace("Standard log entry identified as error by message content: {MessagePreview}", 
                    message.Substring(0, Math.Min(message.Length, 100)));
            }

            return hasErrorIndicator;
        }

        /// <summary>
        /// Specialized error detection with additional context logging
        /// </summary>
        /// <param name="logEntries">Collection of log entries to analyze</param>
        /// <returns>Collection of entries that are considered errors</returns>
        public override async Task<IEnumerable<LogEntry>> DetectErrorsAsync(IEnumerable<LogEntry> logEntries)
        {
            var entries = logEntries as LogEntry[] ?? logEntries.ToArray();
            
            _logger.LogDebug("Starting standard log error detection for {Count} entries", entries.Length);

            var errorEntries = await base.DetectErrorsAsync(entries);
            var errorList = errorEntries.ToList();

            // Log additional statistics for standard logs
            var levelBasedErrors = entries.Count(e => SafeStringEqualsAny(e.Level, ErrorLevels));
            var messageBasedErrors = errorList.Count - levelBasedErrors;

            _logger.LogInformation("Standard log error detection completed: {TotalErrors} errors ({LevelBased} level-based, {MessageBased} message-based) from {TotalEntries} entries",
                errorList.Count, levelBasedErrors, messageBasedErrors, entries.Length);

            LogDetectionStatistics(entries.Length, errorList.Count);

            return errorList;
        }
    }
} 
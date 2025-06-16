using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.ErrorDetection
{
    /// <summary>
    /// Error detection strategy for IIS log entries
    /// Detects errors based on HTTP status codes >= 400
    /// </summary>
    public class IISLogErrorDetectionStrategy : BaseErrorDetectionStrategy, IIISErrorDetectionStrategy
    {
        private const int ErrorStatusThreshold = 400;
        private static readonly int[] CriticalStatusCodes = { 500, 502, 503, 504 };

        public IISLogErrorDetectionStrategy(ILogger<IISLogErrorDetectionStrategy> logger) 
            : base(logger)
        {
        }

        /// <summary>
        /// The log format type this strategy handles
        /// </summary>
        public override LogFormatType SupportedLogType => LogFormatType.IIS;

        /// <summary>
        /// Checks if a single log entry is considered an error
        /// For IIS logs: treats as generic LogEntry, delegates to IIS-specific method
        /// </summary>
        /// <param name="logEntry">Log entry to check</param>
        /// <returns>True if the entry is considered an error</returns>
        public override bool IsError(LogEntry logEntry)
        {
            // For generic LogEntry, try to extract status from message or use level
            if (logEntry == null)
                return false;

            try
            {
                // Try to extract HTTP status from message field
                var statusFromMessage = ExtractStatusFromMessage(logEntry.Message);
                if (statusFromMessage.HasValue)
                {
                    return statusFromMessage.Value >= ErrorStatusThreshold;
                }

                // Fallback to level-based detection
                return SafeStringEqualsAny(logEntry.Level, new[] { "error", "critical", "fatal" });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking if IIS log entry is error");
                return false;
            }
        }

        /// <summary>
        /// Checks if a single IIS log entry is considered an error
        /// </summary>
        /// <param name="iisLogEntry">IIS log entry to check</param>
        /// <returns>True if the entry is considered an error</returns>
        public bool IsIISError(IisLogEntry iisLogEntry)
        {
            try
            {
                if (iisLogEntry == null)
                    return false;

                var httpStatus = iisLogEntry.HttpStatus ?? 0;

                // HTTP status >= 400 indicates an error
                var isError = httpStatus >= ErrorStatusThreshold;

                if (isError)
                {
                    var severity = CriticalStatusCodes.Contains(httpStatus) ? "Critical" : "Error";
                    _logger.LogTrace("IIS log entry identified as {Severity}: Status={Status}, Method={Method}, Uri={Uri}", 
                        severity, httpStatus, iisLogEntry.Method, iisLogEntry.UriStem);
                }

                return isError;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking if IIS log entry is error");
                return false;
            }
        }

        /// <summary>
        /// Detects error entries from IIS log entries
        /// </summary>
        /// <param name="iisLogEntries">Collection of IIS log entries to analyze</param>
        /// <returns>Collection of IIS entries that are considered errors</returns>
        public async Task<IEnumerable<IisLogEntry>> DetectIISErrorsAsync(IEnumerable<IisLogEntry> iisLogEntries)
        {
            try
            {
                var entries = iisLogEntries as IisLogEntry[] ?? iisLogEntries.ToArray();
                
                _logger.LogDebug("Starting IIS log error detection for {Count} entries", entries.Length);

                var errorEntries = await Task.Run(() => 
                    entries.Where(IsIISError).ToList());

                // Log statistics by error type
                var clientErrors = errorEntries.Count(e => e.HttpStatus >= 400 && e.HttpStatus < 500);
                var serverErrors = errorEntries.Count(e => e.HttpStatus >= 500);

                _logger.LogInformation("IIS log error detection completed: {TotalErrors} errors ({ClientErrors} 4xx, {ServerErrors} 5xx) from {TotalEntries} entries",
                    errorEntries.Count, clientErrors, serverErrors, entries.Length);

                LogDetectionStatistics(entries.Length, errorEntries.Count);

                return errorEntries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during IIS error detection");
                return Enumerable.Empty<IisLogEntry>();
            }
        }

        /// <summary>
        /// Gets error criteria description for this strategy
        /// </summary>
        /// <returns>Human-readable description of error detection criteria</returns>
        public override string GetErrorCriteriaDescription()
        {
            return $"IIS logs: HTTP status code >= {ErrorStatusThreshold} (4xx client errors, 5xx server errors)";
        }

        /// <summary>
        /// Enhanced validation for IIS log entries
        /// </summary>
        /// <param name="logEntry">Log entry to validate</param>
        /// <returns>True if entry is valid for IIS error detection</returns>
        protected override bool IsValidLogEntry(LogEntry logEntry)
        {
            if (!base.IsValidLogEntry(logEntry))
                return false;

            // For IIS logs, we need either a message with status or other IIS indicators
            return !string.IsNullOrEmpty(logEntry.Message) || !string.IsNullOrEmpty(logEntry.Source);
        }

        /// <summary>
        /// Attempts to extract HTTP status code from log message
        /// </summary>
        /// <param name="message">Log message to parse</param>
        /// <returns>HTTP status code if found, null otherwise</returns>
        private int? ExtractStatusFromMessage(string? message)
        {
            if (string.IsNullOrEmpty(message))
                return null;

            // Common IIS log patterns for status codes
            var patterns = new[]
            {
                @"\s(\d{3})\s",  // Space-separated status code
                @"status:(\d{3})", // "status:404" format
                @"HTTP/\d\.\d\s(\d{3})", // HTTP protocol with status
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(message, pattern);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var status))
                {
                    return status;
                }
            }

            return null;
        }

        /// <summary>
        /// Specialized error detection for mixed IIS log formats
        /// </summary>
        /// <param name="logEntries">Collection of log entries to analyze</param>
        /// <returns>Collection of entries that are considered errors</returns>
        public override async Task<IEnumerable<LogEntry>> DetectErrorsAsync(IEnumerable<LogEntry> logEntries)
        {
            var entries = logEntries as LogEntry[] ?? logEntries.ToArray();
            
            _logger.LogDebug("Starting IIS-compatible log error detection for {Count} entries", entries.Length);

            var errorEntries = await base.DetectErrorsAsync(entries);
            var errorList = errorEntries.ToList();

            // Log additional statistics for IIS logs
            var statusBasedErrors = entries.Count(e => ExtractStatusFromMessage(e.Message) >= ErrorStatusThreshold);
            var levelBasedErrors = errorList.Count - statusBasedErrors;

            _logger.LogInformation("IIS-compatible log error detection completed: {TotalErrors} errors ({StatusBased} status-based, {LevelBased} level-based) from {TotalEntries} entries",
                errorList.Count, statusBasedErrors, levelBasedErrors, entries.Length);

            LogDetectionStatistics(entries.Length, errorList.Count);

            return errorList;
        }
    }
} 
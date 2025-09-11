using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.ErrorDetection
{
    /// <summary>
    /// Base abstract class for error detection strategies
    /// Provides common functionality and enforces consistent error detection patterns
    /// </summary>
    public abstract class BaseErrorDetectionStrategy : IErrorDetectionStrategy
    {
        protected readonly ILogger _logger;

        protected BaseErrorDetectionStrategy(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// The log format type this strategy handles
        /// </summary>
        public abstract LogFormatType SupportedLogType { get; }

        /// <summary>
        /// Detects error entries from standard log entries
        /// </summary>
        /// <param name="logEntries">Collection of log entries to analyze</param>
        /// <returns>Collection of entries that are considered errors</returns>
        public virtual async Task<IEnumerable<LogEntry>> DetectErrorsAsync(IEnumerable<LogEntry> logEntries)
        {
            try
            {
                _logger.LogDebug("Detecting errors for {LogType} using {StrategyType}", 
                    SupportedLogType, GetType().Name);

                var entries = logEntries as LogEntry[] ?? logEntries.ToArray();
                var startTime = DateTime.UtcNow;

                var errorEntries = await Task.Run(() => 
                    entries.Where(IsError).ToList());

                var duration = DateTime.UtcNow - startTime;
                _logger.LogDebug("Error detection completed for {LogType} in {Duration}ms. Found {ErrorCount} errors from {TotalCount} entries",
                    SupportedLogType, duration.TotalMilliseconds, errorEntries.Count, entries.Length);

                return errorEntries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during error detection for {LogType}", SupportedLogType);
                return Enumerable.Empty<LogEntry>();
            }
        }

        /// <summary>
        /// Checks if a single log entry is considered an error
        /// Must be implemented by derived classes with specific error criteria
        /// </summary>
        /// <param name="logEntry">Log entry to check</param>
        /// <returns>True if the entry is considered an error</returns>
        public abstract bool IsError(LogEntry logEntry);

        /// <summary>
        /// Gets error criteria description for this strategy
        /// Must be implemented by derived classes to describe their error detection logic
        /// </summary>
        /// <returns>Human-readable description of error detection criteria</returns>
        public abstract string GetErrorCriteriaDescription();

        /// <summary>
        /// Protected helper method to safely check string values with null handling
        /// </summary>
        /// <param name="value">String value to check</param>
        /// <param name="targetValue">Target value to compare against</param>
        /// <param name="comparisonType">Type of string comparison to use</param>
        /// <returns>True if values match, false if null or no match</returns>
        protected static bool SafeStringEquals(string? value, string targetValue, StringComparison comparisonType = StringComparison.OrdinalIgnoreCase)
        {
            return !string.IsNullOrEmpty(value) && value.Equals(targetValue, comparisonType);
        }

        /// <summary>
        /// Protected helper method to safely check if string contains target value
        /// </summary>
        /// <param name="value">String value to check</param>
        /// <param name="targetValue">Target value to search for</param>
        /// <param name="comparisonType">Type of string comparison to use</param>
        /// <returns>True if value contains target, false if null or not found</returns>
        protected static bool SafeStringContains(string? value, string targetValue, StringComparison comparisonType = StringComparison.OrdinalIgnoreCase)
        {
            return !string.IsNullOrEmpty(value) && value.Contains(targetValue, comparisonType);
        }

        /// <summary>
        /// Protected helper method to safely check if string matches any of the target values
        /// </summary>
        /// <param name="value">String value to check</param>
        /// <param name="targetValues">Array of target values to match against</param>
        /// <param name="comparisonType">Type of string comparison to use</param>
        /// <returns>True if value matches any target, false if null or no match</returns>
        protected static bool SafeStringEqualsAny(string? value, string[] targetValues, StringComparison comparisonType = StringComparison.OrdinalIgnoreCase)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return targetValues.Any(target => value.Equals(target, comparisonType));
        }

        /// <summary>
        /// Protected helper method to safely parse integer values
        /// </summary>
        /// <param name="value">String value to parse</param>
        /// <param name="defaultValue">Default value to return if parsing fails</param>
        /// <returns>Parsed integer or default value</returns>
        protected static int SafeParseInt(string? value, int defaultValue = 0)
        {
            return int.TryParse(value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// Protected helper method to validate log entry has required fields
        /// </summary>
        /// <param name="logEntry">Log entry to validate</param>
        /// <returns>True if entry has required fields for error detection</returns>
        protected virtual bool IsValidLogEntry(LogEntry logEntry)
        {
            if (logEntry == null)
            {
                _logger.LogWarning("Null log entry encountered during error detection");
                return false;
            }

            // Basic validation - derived classes can override for specific requirements
            return true;
        }

        /// <summary>
        /// Protected method to log error detection statistics
        /// </summary>
        /// <param name="totalEntries">Total number of entries processed</param>
        /// <param name="errorEntries">Number of error entries found</param>
        protected void LogDetectionStatistics(int totalEntries, int errorEntries)
        {
            var errorPercentage = totalEntries > 0 ? (double)errorEntries / totalEntries * 100 : 0;
            
            _logger.LogInformation("{StrategyType} detected {ErrorCount} errors from {TotalCount} entries ({ErrorPercentage:F2}%)",
                GetType().Name, errorEntries, totalEntries, errorPercentage);
        }
    }
} 
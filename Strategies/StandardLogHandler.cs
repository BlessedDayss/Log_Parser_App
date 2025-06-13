using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Log_Parser_App.Services;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Strategies
{
    /// <summary>
    /// Handler for standard log format processing
    /// Implements Strategy pattern for log type specific operations
    /// </summary>
    public class StandardLogHandler : ILogTypeHandler
    {
        private readonly ILogger<StandardLogHandler> _logger;
        private readonly ILogParserService _logParserService;

        public LogFormatType SupportedLogType => LogFormatType.Standard;

        public StandardLogHandler(ILogger<StandardLogHandler> logger, ILogParserService logParserService)
        {
            _logger = logger;
            _logParserService = logParserService;
        }

        /// <summary>
        /// Parse log file using standard log parsing logic
        /// </summary>
        public async Task<IEnumerable<LogEntry>> ParseLogFileAsync(string filePath)
        {
            try
            {
                _logger.LogDebug($"Parsing standard log file: {filePath}");

                var entries = new List<LogEntry>();
                await foreach (var entry in _logParserService.ParseLogFileAsync(filePath, default))
                {
                    // Apply standard log specific processing
                    ProcessStandardLogEntry(entry);
                    entries.Add(entry);
                }

                _logger.LogInformation($"Parsed {entries.Count} standard log entries from {filePath}");
                return entries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error parsing standard log file: {filePath}");
                throw;
            }
        }

        /// <summary>
        /// Validate if file is a standard log format
        /// </summary>
        public async Task<bool> CanHandleAsync(string filePath)
        {
            try
            {
                _logger.LogDebug($"Validating standard log format for: {filePath}");

                // Read first few lines to determine if it's standard format
                var lines = await System.IO.File.ReadAllLinesAsync(filePath);
                var sampleLines = lines.Take(10).ToArray();

                foreach (var line in sampleLines)
                {
                    if (IsStandardLogLine(line))
                    {
                        _logger.LogDebug($"File {filePath} identified as standard log format");
                        return true;
                    }
                }

                _logger.LogDebug($"File {filePath} is not a standard log format");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error validating standard log format for: {filePath}");
                return false;
            }
        }

        /// <summary>
        /// Get specific processing options for standard logs
        /// </summary>
        public LogProcessingOptions GetProcessingOptions()
        {
            return new LogProcessingOptions
            {
                LogType = LogFormatType.Standard,
                SupportedFields = new List<string>
                {
                    "Timestamp", "Level", "Message", "Source", "Thread", "Logger", 
                    "Exception", "StackTrace", "CorrelationId", "ErrorType"
                },
                DefaultFilters = new List<string> { "ERROR", "WARNING", "INFO" },
                RequiresSpecialParsing = false,
                SupportsRealTimeMonitoring = true,
                MaxFileSize = 500 * 1024 * 1024, // 500MB
                RecommendedBatchSize = 1000
            };
        }

        /// <summary>
        /// Apply standard log specific processing and enrichment
        /// </summary>
        public async Task<LogEntry> ProcessLogEntryAsync(LogEntry entry)
        {
            return await Task.Run(() =>
            {
                try
                {
                    ProcessStandardLogEntry(entry);
                    return entry;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing standard log entry: {entry.LineNumber}");
                    return entry;
                }
            });
        }

        /// <summary>
        /// Get validation rules specific to standard logs
        /// </summary>
        public IEnumerable<LogValidationRule> GetValidationRules()
        {
            return new List<LogValidationRule>
            {
                new LogValidationRule
                {
                    RuleName = "TimestampFormat",
                    Description = "Timestamp should be in valid DateTime format",
                    ValidationFunc = entry => entry.Timestamp != DateTime.MinValue,
                    Severity = ValidationSeverity.Warning
                },
                new LogValidationRule
                {
                    RuleName = "LogLevelRequired",
                    Description = "Log level should not be empty",
                    ValidationFunc = entry => !string.IsNullOrEmpty(entry.Level),
                    Severity = ValidationSeverity.Error
                },
                new LogValidationRule
                {
                    RuleName = "MessageRequired", 
                    Description = "Log message should not be empty",
                    ValidationFunc = entry => !string.IsNullOrEmpty(entry.Message),
                    Severity = ValidationSeverity.Error
                },
                new LogValidationRule
                {
                    RuleName = "MessageLength",
                    Description = "Log message should not exceed reasonable length",
                    ValidationFunc = entry => entry.Message.Length <= 10000,
                    Severity = ValidationSeverity.Warning
                }
            };
        }

        /// <summary>
        /// Extract metadata specific to standard logs
        /// </summary>
        public LogMetadata ExtractMetadata(IEnumerable<LogEntry> entries)
        {
            try
            {
                var entriesList = entries.ToList();
                _logger.LogDebug($"Extracting metadata from {entriesList.Count} standard log entries");

                if (!entriesList.Any())
                {
                    return CreateEmptyMetadata();
                }

                var metadata = new LogMetadata
                {
                    LogType = LogFormatType.Standard,
                    TotalEntries = entriesList.Count,
                    FirstLogTime = entriesList.Min(e => e.Timestamp),
                    LastLogTime = entriesList.Max(e => e.Timestamp),
                    LogLevels = entriesList.GroupBy(e => e.Level).ToDictionary(g => g.Key, g => g.Count()),
                    Sources = entriesList.Where(e => !string.IsNullOrEmpty(e.Source))
                                       .GroupBy(e => e.Source!)
                                       .ToDictionary(g => g.Key, g => g.Count()),
                    ErrorTypes = entriesList.Where(e => !string.IsNullOrEmpty(e.ErrorType))
                                           .GroupBy(e => e.ErrorType!)
                                           .ToDictionary(g => g.Key, g => g.Count()),
                    HasStackTraces = entriesList.Any(e => !string.IsNullOrEmpty(e.StackTrace)),
                    HasCorrelationIds = entriesList.Any(e => !string.IsNullOrEmpty(e.CorrelationId)),
                    AverageMessageLength = entriesList.Average(e => e.Message.Length),
                    LongestMessage = entriesList.OrderByDescending(e => e.Message.Length).FirstOrDefault()?.Message ?? "",
                    UniqueErrorCount = entriesList.Where(e => e.Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase))
                                                 .Select(e => e.Message)
                                                 .Distinct()
                                                 .Count()
                };

                // Add standard log specific metadata
                metadata.CustomProperties = new Dictionary<string, object>
                {
                    { "SupportsThreadInfo", entriesList.Any(e => !string.IsNullOrEmpty(e.Source)) },
                    { "SupportsLoggerNames", entriesList.Any(e => !string.IsNullOrEmpty(e.Source)) },
                    { "HasStructuredLogging", entriesList.Any(e => e.Message.Contains("{") && e.Message.Contains("}")) },
                    { "HasExceptions", entriesList.Any(e => !string.IsNullOrEmpty(e.StackTrace)) }
                };

                _logger.LogInformation($"Metadata extracted for standard logs: {metadata.TotalEntries} entries, {metadata.LogLevels.Count} levels");
                return metadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting standard log metadata");
                return CreateEmptyMetadata();
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Process a single standard log entry
        /// </summary>
        private void ProcessStandardLogEntry(LogEntry entry)
        {
            try
            {
                // Extract and separate stack trace from message if present
                if (!string.IsNullOrEmpty(entry.Message) && entry.Message.Contains("at "))
                {
                    var lines = entry.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    var messagePart = new List<string>();
                    var stackTracePart = new List<string>();
                    
                    bool inStackTrace = false;
                    foreach (var line in lines)
                    {
                        if (line.TrimStart().StartsWith("at ") && (line.Contains("(") || line.Contains("line")))
                        {
                            inStackTrace = true;
                        }

                        if (inStackTrace)
                        {
                            stackTracePart.Add(line);
                        }
                        else
                        {
                            messagePart.Add(line);
                        }
                    }

                    if (messagePart.Any())
                    {
                        entry.Message = string.Join(" ", messagePart).Trim();
                    }

                    if (stackTracePart.Any())
                    {
                        entry.StackTrace = string.Join("\n", stackTracePart);
                    }
                }

                // Determine error type from message
                if (string.IsNullOrEmpty(entry.ErrorType) && entry.Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase))
                {
                    entry.ErrorType = DetermineErrorType(entry.Message);
                }

                // Extract correlation ID from message if pattern exists
                if (string.IsNullOrEmpty(entry.CorrelationId))
                {
                    entry.CorrelationId = ExtractCorrelationId(entry.Message);
                }

                // Normalize log level
                entry.Level = NormalizeLogLevel(entry.Level);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error processing standard log entry {entry.LineNumber}");
            }
        }

        /// <summary>
        /// Check if a line matches standard log format
        /// </summary>
        private bool IsStandardLogLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            // Common patterns for standard logs
            var patterns = new[]
            {
                // 2024-01-01 12:00:00 [INFO] Message
                @"^\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}",
                // [2024-01-01 12:00:00] [INFO] Message  
                @"^\[\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\]",
                // INFO 2024-01-01 12:00:00 Message
                @"^(DEBUG|INFO|WARN|WARNING|ERROR|FATAL)\s+\d{4}-\d{2}-\d{2}",
                // 12:00:00.123 [INFO] Message
                @"^\d{2}:\d{2}:\d{2}\.\d{3}\s+\[(DEBUG|INFO|WARN|WARNING|ERROR|FATAL)\]"
            };

            foreach (var pattern in patterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(line, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determine error type from message content
        /// </summary>
        private string DetermineErrorType(string message)
        {
            if (string.IsNullOrEmpty(message))
                return "Unknown";

            var lowerMessage = message.ToLower();

            if (lowerMessage.Contains("nullreferenceexception") || lowerMessage.Contains("null reference"))
                return "NullReference";
            if (lowerMessage.Contains("argumentexception") || lowerMessage.Contains("argument"))
                return "ArgumentError";
            if (lowerMessage.Contains("timeout") || lowerMessage.Contains("timed out"))
                return "Timeout";
            if (lowerMessage.Contains("connection") || lowerMessage.Contains("network"))
                return "Network";
            if (lowerMessage.Contains("database") || lowerMessage.Contains("sql"))
                return "Database";
            if (lowerMessage.Contains("file") || lowerMessage.Contains("directory"))
                return "FileSystem";
            if (lowerMessage.Contains("permission") || lowerMessage.Contains("access denied"))
                return "Security";
            if (lowerMessage.Contains("validation") || lowerMessage.Contains("invalid"))
                return "Validation";

            return "Application";
        }

        /// <summary>
        /// Extract correlation ID from message
        /// </summary>
        private string? ExtractCorrelationId(string message)
        {
            if (string.IsNullOrEmpty(message))
                return null;

            // Common correlation ID patterns
            var patterns = new[]
            {
                @"correlation[_\-]?id[:\s]*([a-fA-F0-9\-]{32,36})",
                @"request[_\-]?id[:\s]*([a-fA-F0-9\-]{32,36})",
                @"trace[_\-]?id[:\s]*([a-fA-F0-9\-]{32,36})",
                @"\[([a-fA-F0-9\-]{32,36})\]"
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(message, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Normalize log level to standard format
        /// </summary>
        private string NormalizeLogLevel(string level)
        {
            if (string.IsNullOrEmpty(level))
                return "INFO";

            return level.ToUpper() switch
            {
                "TRACE" or "VERBOSE" or "VRB" => "TRACE",
                "DEBUG" or "DBG" => "DEBUG", 
                "INFORMATION" or "INFO" or "INF" => "INFO",
                "WARNING" or "WARN" or "WRN" => "WARNING",
                "ERROR" or "ERR" => "ERROR",
                "FATAL" or "CRITICAL" or "CRIT" => "FATAL",
                _ => level.ToUpper()
            };
        }

        /// <summary>
        /// Create empty metadata object
        /// </summary>
        private LogMetadata CreateEmptyMetadata()
        {
            return new LogMetadata
            {
                LogType = LogFormatType.Standard,
                TotalEntries = 0,
                FirstLogTime = DateTime.MinValue,
                LastLogTime = DateTime.MinValue,
                LogLevels = new Dictionary<string, int>(),
                Sources = new Dictionary<string, int>(),
                ErrorTypes = new Dictionary<string, int>(),
                HasStackTraces = false,
                HasCorrelationIds = false,
                AverageMessageLength = 0,
                LongestMessage = string.Empty,
                UniqueErrorCount = 0,
                CustomProperties = new Dictionary<string, object>()
            };
        }

        #endregion
    }
} 
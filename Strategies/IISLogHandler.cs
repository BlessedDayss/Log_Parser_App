using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Strategies
{
    /// <summary>
    /// Handler for IIS log format processing
    /// Implements Strategy pattern for log type specific operations
    /// Enhanced with performance optimization services
    /// </summary>
    public class IISLogHandler : ILogTypeHandler
    {
        private readonly ILogger<IISLogHandler> _logger;
        private readonly IIISLogParserService _iisLogParserService;
        private readonly IAsyncLogParser _asyncLogParser;
        private readonly ILogEntryPool _logEntryPool;
        private readonly IBackgroundProcessingService _backgroundProcessingService;
        private readonly IBatchProcessor<LogEntry> _batchProcessor;

        public LogFormatType SupportedLogType => LogFormatType.IIS;

        public IISLogHandler(
            ILogger<IISLogHandler> logger,
            IIISLogParserService iisLogParserService,
            IAsyncLogParser asyncLogParser,
            ILogEntryPool logEntryPool,
            IBackgroundProcessingService backgroundProcessingService,
            IBatchProcessor<LogEntry> batchProcessor) {
            _logger = logger;
            _iisLogParserService = iisLogParserService;
            _asyncLogParser = asyncLogParser;
            _logEntryPool = logEntryPool;
            _backgroundProcessingService = backgroundProcessingService;
            _batchProcessor = batchProcessor;
        }

        /// <summary>
        /// Parse IIS log file using optimized streaming approach
        /// </summary>
        public async Task<IEnumerable<LogEntry>> ParseLogFileAsync(string filePath) {
            try {
                _logger.LogDebug($"Parsing IIS log file with performance optimization: {filePath}");

                var logEntries = new List<LogEntry>();

                // Use streaming parser for better performance with large IIS files
                await foreach (var entry in _asyncLogParser.ParseAsync(filePath)) {
                    // Apply IIS-specific processing
                    ProcessIISLogEntry(entry);
                    logEntries.Add(entry);
                }

                _logger.LogInformation($"Parsed {logEntries.Count} IIS log entries from {filePath}");
                return logEntries;
            } catch (Exception ex) {
                _logger.LogError(ex, $"Error parsing IIS log file: {filePath}");
                throw;
            }
        }

        /// <summary>
        /// Validate if file is an IIS log format
        /// </summary>
        public async Task<bool> CanHandleAsync(string filePath) {
            try {
                _logger.LogDebug($"Validating IIS log format for: {filePath}");

                var lines = await System.IO.File.ReadAllLinesAsync(filePath);
                var headerLines = lines.Take(20).ToArray();

                // Look for IIS log headers
                foreach (var line in headerLines) {
                    if (IsIISLogHeader(line)) {
                        _logger.LogDebug($"File {filePath} identified as IIS log format");
                        return true;
                    }
                }

                // Check for typical IIS log patterns in data lines
                var dataLines = lines.Skip(5).Take(10).ToArray();
                foreach (var line in dataLines) {
                    if (IsIISLogDataLine(line)) {
                        _logger.LogDebug($"File {filePath} identified as IIS log format by data pattern");
                        return true;
                    }
                }

                _logger.LogDebug($"File {filePath} is not an IIS log format");
                return false;
            } catch (Exception ex) {
                _logger.LogWarning(ex, $"Error validating IIS log format for: {filePath}");
                return false;
            }
        }

        /// <summary>
        /// Get IIS-specific processing options
        /// </summary>
        public LogProcessingOptions GetProcessingOptions() {
            return new LogProcessingOptions {
                LogType = LogFormatType.IIS,
                SupportedFields = new List<string> {
                    "Timestamp", "IPAddress", "Method", "URI", "QueryString", "Port",
                    "UserName", "ClientIP", "UserAgent", "StatusCode", "SubStatus",
                    "BytesSent", "BytesReceived", "TimeTaken", "Referer", "Host"
                },
                DefaultFilters = new List<string> { "4xx", "5xx", "GET", "POST" },
                RequiresSpecialParsing = true,
                SupportsRealTimeMonitoring = true,
                MaxFileSize = 1024 * 1024 * 1024, // 1GB
                RecommendedBatchSize = 5000
            };
        }

        /// <summary>
        /// Apply IIS-specific processing and enrichment
        /// </summary>
        public async Task<LogEntry> ProcessLogEntryAsync(LogEntry entry) {
            return await Task.Run(() => {
                try {
                    ProcessIISLogEntry(entry);
                    return entry;
                } catch (Exception ex) {
                    _logger.LogError(ex, $"Error processing IIS log entry: {entry.LineNumber}");
                    return entry;
                }
            });
        }

        /// <summary>
        /// Get validation rules specific to IIS logs
        /// </summary>
        public IEnumerable<LogValidationRule> GetValidationRules() {
            return new List<LogValidationRule> {
                new LogValidationRule {
                    RuleName = "TimestampFormat",
                    Description = "Timestamp should be in valid IIS DateTime format",
                    ValidationFunc = entry => entry.Timestamp != DateTime.MinValue,
                    Severity = ValidationSeverity.Warning
                },
                new LogValidationRule {
                    RuleName = "StatusCodeRange",
                    Description = "HTTP status code should be in valid range (100-599)",
                    ValidationFunc = entry => ValidateStatusCode(entry.Message),
                    Severity = ValidationSeverity.Warning
                },
                new LogValidationRule {
                    RuleName = "HTTPMethodValid",
                    Description = "HTTP method should be valid",
                    ValidationFunc = entry => ValidateHTTPMethod(entry.Message),
                    Severity = ValidationSeverity.Warning
                },
                new LogValidationRule {
                    RuleName = "IPAddressFormat",
                    Description = "IP address should be in valid format",
                    ValidationFunc = entry => ValidateIPAddress(entry.Source),
                    Severity = ValidationSeverity.Warning
                }
            };
        }

        /// <summary>
        /// Extract IIS-specific metadata
        /// </summary>
        public LogMetadata ExtractMetadata(IEnumerable<LogEntry> entries) {
            try {
                var entriesList = entries.ToList();
                _logger.LogDebug($"Extracting metadata from {entriesList.Count} IIS log entries");

                if (!entriesList.Any()) {
                    return CreateEmptyMetadata();
                }

                // Extract HTTP methods from messages
                var httpMethods = ExtractHTTPMethods(entriesList);
                var statusCodes = ExtractStatusCodes(entriesList);
                var ipAddresses = ExtractIPAddresses(entriesList);
                var userAgents = ExtractUserAgents(entriesList);

                var metadata = new LogMetadata {
                    LogType = LogFormatType.IIS,
                    TotalEntries = entriesList.Count,
                    FirstLogTime = entriesList.Min(e => e.Timestamp),
                    LastLogTime = entriesList.Max(e => e.Timestamp),
                    LogLevels = new Dictionary<string, int> { { "INFO", entriesList.Count } }, // IIS logs are generally INFO level
                    Sources = ipAddresses,
                    ErrorTypes = statusCodes.Where(kvp => kvp.Key.StartsWith("4") || kvp.Key.StartsWith("5")).ToDictionary(kvp => $"HTTP {kvp.Key}", kvp => kvp.Value),
                    HasStackTraces = false, // IIS logs typically don't have stack traces
                    HasCorrelationIds = false, // Standard IIS logs don't have correlation IDs
                    AverageMessageLength = entriesList.Average(e => e.Message.Length),
                    LongestMessage = entriesList.OrderByDescending(e => e.Message.Length).FirstOrDefault()?.Message ?? "",
                    UniqueErrorCount = statusCodes.Where(kvp => kvp.Key.StartsWith("4") || kvp.Key.StartsWith("5")).Count()
                };

                // Add IIS-specific metadata
                metadata.CustomProperties = new Dictionary<string, object> {
                    { "HTTPMethods", httpMethods },
                    { "StatusCodes", statusCodes },
                    { "UniqueIPAddresses", ipAddresses.Count },
                    { "UserAgents", userAgents },
                    { "TotalTraffic", CalculateTotalTraffic(entriesList) },
                    { "ErrorRate", CalculateErrorRate(statusCodes) },
                    { "TopIPsByRequests", ipAddresses.OrderByDescending(kvp => kvp.Value).Take(10).ToDictionary(kvp => kvp.Key, kvp => kvp.Value) },
                    { "PopularEndpoints", ExtractPopularEndpoints(entriesList) }
                };

                _logger.LogInformation($"Metadata extracted for IIS logs: {metadata.TotalEntries} entries, {httpMethods.Count} methods");
                return metadata;
            } catch (Exception ex) {
                _logger.LogError(ex, "Error extracting IIS log metadata");
                return CreateEmptyMetadata();
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Convert IIS log entry to standard LogEntry format
        /// </summary>
        private LogEntry ConvertIISToLogEntry(IisLogEntry iisEntry) {
            var message = $"{iisEntry.Method} {iisEntry.UriStem}{iisEntry.UriQuery} - {iisEntry.HttpStatus} {iisEntry.Win32Status} {iisEntry.BytesSent}";

            return new LogEntry {
                Timestamp = iisEntry.DateTime?.DateTime ?? DateTime.MinValue,
                Level = DetermineLogLevel(iisEntry.HttpStatus ?? 200),
                Message = message,
                Source = iisEntry.ClientIPAddress ?? string.Empty,
                RawData = iisEntry.RawLine ?? string.Empty,
                LineNumber = 0, // IIS entries don't have line numbers by default
                CorrelationId = null, // IIS logs typically don't have correlation IDs
                ErrorType = DetermineErrorType(iisEntry.HttpStatus ?? 200),
                StackTrace = null // IIS logs don't have stack traces
            };
        }

        /// <summary>
        /// Process IIS-specific log entry
        /// </summary>
        private void ProcessIISLogEntry(LogEntry entry) {
            try {
                // Enrich entry with IIS-specific information
                EnrichWithRequestInfo(entry);
                EnrichWithPerformanceInfo(entry);
                EnrichWithSecurityInfo(entry);
            } catch (Exception ex) {
                _logger.LogWarning(ex, $"Error processing IIS log entry {entry.LineNumber}");
            }
        }

        /// <summary>
        /// Check if line is IIS log header
        /// </summary>
        private bool IsIISLogHeader(string line) {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            var iisHeaderPatterns = new[] {
                "#Software: Microsoft Internet Information Services",
                "#Version:",
                "#Date:",
                "#Fields:",
                "#Software: Internet Information Services"
            };

            return iisHeaderPatterns.Any(pattern => line.StartsWith(pattern, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Check if line is IIS log data line
        /// </summary>
        private bool IsIISLogDataLine(string line) {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                return false;

            // IIS log data lines typically have space-separated fields
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 10) // IIS logs typically have many fields
                return false;

            // Check for typical IIS patterns: timestamp, IP, method, URI, status
            if (parts.Length >= 5) {
                // Check for date pattern (yyyy-mm-dd)
                if (System.Text.RegularExpressions.Regex.IsMatch(parts[0], @"^\d{4}-\d{2}-\d{2}$")) {
                    // Check for time pattern (hh:mm:ss)
                    if (System.Text.RegularExpressions.Regex.IsMatch(parts[1], @"^\d{2}:\d{2}:\d{2}$")) {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Determine log level based on HTTP status code
        /// </summary>
        private string DetermineLogLevel(int statusCode) {
            return statusCode switch {
                >= 200 and < 300 => "INFO",
                >= 300 and < 400 => "INFO",
                >= 400 and < 500 => "WARNING",
                >= 500 => "ERROR",
                _ => "INFO"
            };
        }

        /// <summary>
        /// Determine error type from HTTP status code
        /// </summary>
        private string? DetermineErrorType(int statusCode) {
            return statusCode switch {
                400 => "BadRequest",
                401 => "Unauthorized",
                403 => "Forbidden",
                404 => "NotFound",
                408 => "Timeout",
                429 => "TooManyRequests",
                500 => "InternalServerError",
                501 => "NotImplemented",
                502 => "BadGateway",
                503 => "ServiceUnavailable",
                504 => "GatewayTimeout",
                >= 400 and < 500 => "ClientError",
                >= 500 => "ServerError",
                _ => null
            };
        }

        /// <summary>
        /// Enrich entry with request information
        /// </summary>
        private void EnrichWithRequestInfo(LogEntry entry) {
            // Extract method, URI, and query string from message
            // This would be expanded based on actual IIS log format parsing
        }

        /// <summary>
        /// Enrich entry with performance information
        /// </summary>
        private void EnrichWithPerformanceInfo(LogEntry entry) {
            // Extract time-taken, bytes sent/received from message
            // This would be expanded based on actual IIS log format parsing
        }

        /// <summary>
        /// Enrich entry with security information
        /// </summary>
        private void EnrichWithSecurityInfo(LogEntry entry) {
            // Extract user-agent, referrer, authentication info from message
            // This would be expanded based on actual IIS log format parsing
        }

        /// <summary>
        /// Extract HTTP methods from log entries
        /// </summary>
        private Dictionary<string, int> ExtractHTTPMethods(List<LogEntry> entries) {
            var methods = new Dictionary<string, int>();
            var httpMethods = new[] { "GET", "POST", "PUT", "DELETE", "HEAD", "OPTIONS", "PATCH", "TRACE" };

            foreach (var entry in entries) {
                foreach (var method in httpMethods) {
                    if (entry.Message.StartsWith(method + " ", StringComparison.OrdinalIgnoreCase)) {
                        methods[method] = methods.GetValueOrDefault(method, 0) + 1;
                        break;
                    }
                }
            }

            return methods;
        }

        /// <summary>
        /// Extract status codes from log entries
        /// </summary>
        private Dictionary<string, int> ExtractStatusCodes(List<LogEntry> entries) {
            var statusCodes = new Dictionary<string, int>();
            var statusCodePattern = @" - (\d{3}) ";

            foreach (var entry in entries) {
                var match = System.Text.RegularExpressions.Regex.Match(entry.Message, statusCodePattern);
                if (match.Success) {
                    var statusCode = match.Groups[1].Value;
                    statusCodes[statusCode] = statusCodes.GetValueOrDefault(statusCode, 0) + 1;
                }
            }

            return statusCodes;
        }

        /// <summary>
        /// Extract IP addresses from log entries
        /// </summary>
        private Dictionary<string, int> ExtractIPAddresses(List<LogEntry> entries) {
            return entries.Where(e => !string.IsNullOrEmpty(e.Source)).GroupBy(e => e.Source!).ToDictionary(g => g.Key, g => g.Count());
        }

        /// <summary>
        /// Extract user agents from log entries (simplified)
        /// </summary>
        private Dictionary<string, int> ExtractUserAgents(List<LogEntry> entries) {
            // This would be expanded to actually parse user agent from IIS log fields
            return new Dictionary<string, int>();
        }

        /// <summary>
        /// Calculate total traffic volume
        /// </summary>
        private long CalculateTotalTraffic(List<LogEntry> entries) {
            // This would parse bytes sent/received from IIS log entries
            return entries.Count * 1024; // Placeholder calculation
        }

        /// <summary>
        /// Calculate error rate from status codes
        /// </summary>
        private double CalculateErrorRate(Dictionary<string, int> statusCodes) {
            int totalRequests = statusCodes.Values.Sum();
            int errorRequests = statusCodes.Where(kvp => kvp.Key.StartsWith("4") || kvp.Key.StartsWith("5")).Sum(kvp => kvp.Value);

            return totalRequests > 0 ? (double)errorRequests / totalRequests * 100 : 0;
        }

        /// <summary>
        /// Extract popular endpoints
        /// </summary>
        private Dictionary<string, int> ExtractPopularEndpoints(List<LogEntry> entries) {
            var endpoints = new Dictionary<string, int>();
            string uriPattern = @"(GET|POST|PUT|DELETE|HEAD|OPTIONS|PATCH) ([^\s?]+)";

            foreach (var entry in entries) {
                var match = System.Text.RegularExpressions.Regex.Match(entry.Message, uriPattern);
                if (match.Success) {
                    string endpoint = match.Groups[2].Value;
                    endpoints[endpoint] = endpoints.GetValueOrDefault(endpoint, 0) + 1;
                }
            }

            return endpoints.OrderByDescending(kvp => kvp.Value).Take(20).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Validation methods
        /// </summary>
        private bool ValidateStatusCode(string message) {
            var match = System.Text.RegularExpressions.Regex.Match(message, @" - (\d{3}) ");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var statusCode)) {
                return statusCode >= 100 && statusCode <= 599;
            }
            return true; // Don't fail if we can't extract status code
        }

        private bool ValidateHTTPMethod(string message) {
            string[] validMethods = new[] { "GET", "POST", "PUT", "DELETE", "HEAD", "OPTIONS", "PATCH", "TRACE", "CONNECT" };
            return validMethods.Any(method => message.StartsWith(method + " ", StringComparison.OrdinalIgnoreCase));
        }

        private bool ValidateIPAddress(string? ipAddress) {
            if (string.IsNullOrEmpty(ipAddress))
                return true; // Don't fail on missing IP

            return System.Net.IPAddress.TryParse(ipAddress, out _);
        }

        /// <summary>
        /// Create empty metadata object
        /// </summary>
        private LogMetadata CreateEmptyMetadata() {
            return new LogMetadata {
                LogType = LogFormatType.IIS,
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
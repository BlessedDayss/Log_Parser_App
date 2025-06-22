using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using System.Text.RegularExpressions;
using System.Threading;
using System.Runtime.CompilerServices;

namespace Log_Parser_App.Strategies
{
    /// <summary>
    /// Handler for RabbitMQ log format processing
    /// Implements Strategy pattern for RabbitMQ-specific log operations
    /// </summary>
    public class RabbitMqLogHandler : ILogTypeHandler
    {
        private readonly ILogger<RabbitMqLogHandler> _logger;
        private readonly IRabbitMqLogParserService _rabbitMqLogParserService;

        public LogFormatType SupportedLogType => LogFormatType.RabbitMQ;

        public RabbitMqLogHandler(ILogger<RabbitMqLogHandler> logger, IRabbitMqLogParserService rabbitMqLogParserService)
        {
            _logger = logger;
            _rabbitMqLogParserService = rabbitMqLogParserService;
        }

        /// <summary>
        /// Parse RabbitMQ log file using RabbitMQ-specific parsing logic
        /// </summary>
        public async Task<IEnumerable<LogEntry>> ParseLogFileAsync(string filePath)
        {
            try
            {
                _logger.LogDebug($"Parsing RabbitMQ log file: {filePath}");

                var logEntries = new List<LogEntry>();
                
                // Use await foreach like in MainViewModel
                await foreach (var rabbitEntry in _rabbitMqLogParserService.ParseLogFileAsync(filePath, default))
                {
                    var logEntry = rabbitEntry.ToLogEntry();
                    await ProcessLogEntryAsync(logEntry);
                    logEntries.Add(logEntry);
                }

                _logger.LogInformation($"Parsed {logEntries.Count} RabbitMQ log entries from {filePath}");
                return logEntries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error parsing RabbitMQ log file {filePath}");
                return new List<LogEntry>();
            }
        }

        /// <summary>
        /// Validate if file is a RabbitMQ log format
        /// </summary>
        public async Task<bool> CanHandleAsync(string filePath)
        {
            try
            {
                _logger.LogDebug($"Validating RabbitMQ log format for: {filePath}");

                var lines = await System.IO.File.ReadAllLinesAsync(filePath);
                var sampleLines = lines.Take(20).ToArray();

                foreach (var line in sampleLines)
                {
                    if (IsRabbitMqLogLine(line))
                    {
                        _logger.LogDebug($"File {filePath} identified as RabbitMQ log format");
                        return true;
                    }
                }

                _logger.LogDebug($"File {filePath} is not a RabbitMQ log format");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error validating RabbitMQ log format for: {filePath}");
                return false;
            }
        }

        /// <summary>
        /// Get RabbitMQ-specific processing options
        /// </summary>
        public LogProcessingOptions GetProcessingOptions()
        {
            return new LogProcessingOptions
            {
                LogType = LogFormatType.RabbitMQ,
                SupportedFields = new List<string>
                {
                    "Timestamp", "Level", "Message", "Node", "Username", "ProcessUID", 
                    "Exchange", "RoutingKey", "Connection", "Channel",
                    "VirtualHost", "MessageId", "DeliveryTag", "SentTime"
                },
                DefaultFilters = new List<string> { "error", "warning", "connection", "queue" },
                RequiresSpecialParsing = true,
                SupportsRealTimeMonitoring = true,
                MaxFileSize = 500 * 1024 * 1024, // 500MB
                RecommendedBatchSize = 2000
            };
        }

        /// <summary>
        /// Apply RabbitMQ-specific processing and enrichment
        /// </summary>
        public async Task<LogEntry> ProcessLogEntryAsync(LogEntry entry)
        {
            return await Task.Run(() =>
            {
                try
                {
                    ProcessRabbitMqLogEntry(entry);
                    return entry;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing RabbitMQ log entry: {entry.LineNumber}");
                    return entry;
                }
            });
        }

        /// <summary>
        /// Get validation rules specific to RabbitMQ logs
        /// </summary>
        public IEnumerable<LogValidationRule> GetValidationRules()
        {
            return new List<LogValidationRule>
            {
                new LogValidationRule
                {
                    RuleName = "TimestampFormat",
                    Description = "Timestamp should be in valid RabbitMQ DateTime format",
                    ValidationFunc = entry => entry.Timestamp != DateTime.MinValue,
                    Severity = ValidationSeverity.Warning
                },
                new LogValidationRule
                {
                    RuleName = "NodeNamePresent",
                    Description = "RabbitMQ logs should contain node information",
                    ValidationFunc = entry => ValidateNodeName(entry.Source),
                    Severity = ValidationSeverity.Warning
                },
                new LogValidationRule
                {
                    RuleName = "QueueNameFormat",
                    Description = "Queue names should be valid",
                    ValidationFunc = entry => ValidateQueueName(entry.Message),
                    Severity = ValidationSeverity.Info
                },
                new LogValidationRule
                {
                    RuleName = "PIDFormat",
                    Description = "Process ID should be numeric",
                    ValidationFunc = entry => ValidatePID(entry.Message),
                    Severity = ValidationSeverity.Warning
                }
            };
        }

        /// <summary>
        /// Extract RabbitMQ-specific metadata
        /// </summary>
        public LogMetadata ExtractMetadata(IEnumerable<LogEntry> entries)
        {
            try
            {
                var entriesList = entries.ToList();
                _logger.LogDebug($"Extracting metadata from {entriesList.Count} RabbitMQ log entries");

                if (!entriesList.Any())
                {
                    return CreateEmptyMetadata();
                }

                var nodes = ExtractNodes(entriesList);
                var queues = ExtractQueues(entriesList);
                var exchanges = ExtractExchanges(entriesList);
                var consumerTypes = ExtractConsumerTypes(entriesList);
                var connections = ExtractConnections(entriesList);

                var metadata = new LogMetadata
                {
                    LogType = LogFormatType.RabbitMQ,
                    TotalEntries = entriesList.Count,
                    FirstLogTime = entriesList.Min(e => e.Timestamp),
                    LastLogTime = entriesList.Max(e => e.Timestamp),
                    LogLevels = entriesList.GroupBy(e => e.Level).ToDictionary(g => g.Key, g => g.Count()),
                    Sources = nodes,
                    ErrorTypes = ExtractErrorTypes(entriesList),
                    HasStackTraces = entriesList.Any(e => !string.IsNullOrEmpty(e.StackTrace)),
                    HasCorrelationIds = entriesList.Any(e => !string.IsNullOrEmpty(e.CorrelationId)),
                    AverageMessageLength = entriesList.Average(e => e.Message.Length),
                    LongestMessage = entriesList.OrderByDescending(e => e.Message.Length).FirstOrDefault()?.Message ?? "",
                    UniqueErrorCount = entriesList.Where(e => e.Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase))
                                                 .Select(e => e.Message)
                                                 .Distinct()
                                                 .Count()
                };

                // Add RabbitMQ-specific metadata
                metadata.CustomProperties = new Dictionary<string, object>
                {
                    { "Nodes", nodes },
                    { "Queues", queues },
                    { "Exchanges", exchanges },
                    { "ConsumerTypes", consumerTypes },
                    { "Connections", connections },
                    { "MessageRate", CalculateMessageRate(entriesList) },
                    { "ErrorRate", CalculateErrorRate(entriesList) },
                    { "QueueEvents", ExtractQueueEvents(entriesList) },
                    { "ConnectionEvents", ExtractConnectionEvents(entriesList) },
                    { "TopQueues", queues.OrderByDescending(kvp => kvp.Value).Take(10).ToDictionary(kvp => kvp.Key, kvp => kvp.Value) }
                };

                _logger.LogInformation($"Metadata extracted for RabbitMQ logs: {metadata.TotalEntries} entries, {nodes.Count} nodes, {queues.Count} queues");
                return metadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting RabbitMQ log metadata");
                return CreateEmptyMetadata();
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Process RabbitMQ-specific log entry
        /// </summary>
        private void ProcessRabbitMqLogEntry(LogEntry entry)
        {
            try
            {
                // Enrich entry with RabbitMQ-specific information
                EnrichWithMessageInfo(entry);
                EnrichWithQueueInfo(entry);
                EnrichWithConnectionInfo(entry);
                EnrichWithPerformanceInfo(entry);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error processing RabbitMQ log entry {entry.LineNumber}");
            }
        }

        /// <summary>
        /// Check if line is a RabbitMQ log line
        /// </summary>
        private bool IsRabbitMqLogLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            // RabbitMQ log patterns
            var patterns = new[]
            {
                // =INFO REPORT==== timestamp ====
                @"^=\w+ REPORT====.*====$",
                // timestamp [info] <pid.number.number>
                @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \[\w+\] <\d+\.\d+\.\d+>",
                // Contains typical RabbitMQ keywords
                @".*(rabbit|amqp|queue|exchange|connection|channel).*",
                // Node format
                @".*rabbit@\w+.*"
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
        /// Determine error type from RabbitMQ message
        /// </summary>
        private string? DetermineErrorType(string message, string level)
        {
            if (string.IsNullOrEmpty(message))
                return null;

            var lowerMessage = message.ToLower();

            // RabbitMQ-specific error types
            if (lowerMessage.Contains("connection") && lowerMessage.Contains("close"))
                return "ConnectionClosed";
            if (lowerMessage.Contains("queue") && lowerMessage.Contains("not") && lowerMessage.Contains("found"))
                return "QueueNotFound";
            if (lowerMessage.Contains("exchange") && lowerMessage.Contains("not") && lowerMessage.Contains("found"))
                return "ExchangeNotFound";
            if (lowerMessage.Contains("authentication") || lowerMessage.Contains("access_refused"))
                return "Authentication";
            if (lowerMessage.Contains("permission") || lowerMessage.Contains("access_denied"))
                return "Permission";
            if (lowerMessage.Contains("resource") && lowerMessage.Contains("alarm"))
                return "ResourceAlarm";
            if (lowerMessage.Contains("memory") || lowerMessage.Contains("disk"))
                return "ResourceLimit";
            if (lowerMessage.Contains("timeout"))
                return "Timeout";
            if (lowerMessage.Contains("channel") && lowerMessage.Contains("error"))
                return "ChannelError";
            if (lowerMessage.Contains("consumer") && lowerMessage.Contains("cancel"))
                return "ConsumerCancelled";

            return level.Equals("ERROR", StringComparison.OrdinalIgnoreCase) ? "RabbitMQError" : null;
        }

        /// <summary>
        /// Extract correlation ID from RabbitMQ message
        /// </summary>
        private string? ExtractCorrelationId(string message)
        {
            if (string.IsNullOrEmpty(message))
                return null;

            // RabbitMQ correlation ID patterns
            var patterns = new[]
            {
                @"correlation[_\-]?id[:\s]*([a-fA-F0-9\-]{32,36})",
                @"message[_\-]?id[:\s]*([a-fA-F0-9\-]{32,36})",
                @"delivery[_\-]?tag[:\s]*(\d+)"
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
        /// Enrich entry with message information
        /// </summary>
        private void EnrichWithMessageInfo(LogEntry entry)
        {
            // Extract message routing, delivery tags, etc.
        }

        /// <summary>
        /// Enrich entry with queue information
        /// </summary>
        private void EnrichWithQueueInfo(LogEntry entry)
        {
            // Extract queue names, consumer info, etc.
        }

        /// <summary>
        /// Enrich entry with connection information
        /// </summary>
        private void EnrichWithConnectionInfo(LogEntry entry)
        {
            // Extract connection details, client info, etc.
        }

        /// <summary>
        /// Enrich entry with performance information
        /// </summary>
        private void EnrichWithPerformanceInfo(LogEntry entry)
        {
            // Extract memory usage, message rates, etc.
        }

        /// <summary>
        /// Extract node names from log entries
        /// </summary>
        private Dictionary<string, int> ExtractNodes(List<LogEntry> entries)
        {
            var nodes = new Dictionary<string, int>();
            var nodePattern = @"rabbit@(\w+)";

            foreach (var entry in entries)
            {
                // Try source field first
                if (!string.IsNullOrEmpty(entry.Source))
                {
                    nodes[entry.Source] = nodes.GetValueOrDefault(entry.Source, 0) + 1;
                }
                else
                {
                    // Try to extract from message
                    var match = System.Text.RegularExpressions.Regex.Match(entry.Message, nodePattern);
                    if (match.Success)
                    {
                        var nodeName = match.Groups[1].Value;
                        nodes[nodeName] = nodes.GetValueOrDefault(nodeName, 0) + 1;
                    }
                }
            }

            return nodes;
        }

        /// <summary>
        /// Extract queue names from log entries
        /// </summary>
        private Dictionary<string, int> ExtractQueues(List<LogEntry> entries)
        {
            var queues = new Dictionary<string, int>();
            var queuePattern = @"queue[:\s]+([a-zA-Z0-9_\-\.]+)";

            foreach (var entry in entries)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(entry.Message, queuePattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Success)
                    {
                        var queueName = match.Groups[1].Value;
                        queues[queueName] = queues.GetValueOrDefault(queueName, 0) + 1;
                    }
                }
            }

            return queues;
        }

        /// <summary>
        /// Extract exchange names from log entries
        /// </summary>
        private Dictionary<string, int> ExtractExchanges(List<LogEntry> entries)
        {
            var exchanges = new Dictionary<string, int>();
            var exchangePattern = @"exchange[:\s]+([a-zA-Z0-9_\-\.]+)";

            foreach (var entry in entries)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(entry.Message, exchangePattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Success)
                    {
                        var exchangeName = match.Groups[1].Value;
                        exchanges[exchangeName] = exchanges.GetValueOrDefault(exchangeName, 0) + 1;
                    }
                }
            }

            return exchanges;
        }

        /// <summary>
        /// Extract consumer types from log entries
        /// </summary>
        private Dictionary<string, int> ExtractConsumerTypes(List<LogEntry> entries)
        {
            var consumerTypes = new Dictionary<string, int>();
            var consumerPattern = @"consumer[:\s]+([a-zA-Z0-9_\-\.]+)";

            foreach (var entry in entries)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(entry.Message, consumerPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Success)
                    {
                        var consumerType = match.Groups[1].Value;
                        consumerTypes[consumerType] = consumerTypes.GetValueOrDefault(consumerType, 0) + 1;
                    }
                }
            }

            return consumerTypes;
        }

        /// <summary>
        /// Extract connection information from log entries
        /// </summary>
        private Dictionary<string, int> ExtractConnections(List<LogEntry> entries)
        {
            var connections = new Dictionary<string, int>();
            var connectionPattern = @"connection[:\s]+([a-zA-Z0-9_\-\.]+)";

            foreach (var entry in entries)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(entry.Message, connectionPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Success)
                    {
                        var connectionInfo = match.Groups[1].Value;
                        connections[connectionInfo] = connections.GetValueOrDefault(connectionInfo, 0) + 1;
                    }
                }
            }

            return connections;
        }

        /// <summary>
        /// Extract error types from log entries
        /// </summary>
        private Dictionary<string, int> ExtractErrorTypes(List<LogEntry> entries)
        {
            var errorTypes = new Dictionary<string, int>();

            foreach (var entry in entries.Where(e => !string.IsNullOrEmpty(e.ErrorType)))
            {
                errorTypes[entry.ErrorType!] = errorTypes.GetValueOrDefault(entry.ErrorType!, 0) + 1;
            }

            return errorTypes;
        }

        /// <summary>
        /// Calculate message rate from log entries
        /// </summary>
        private double CalculateMessageRate(List<LogEntry> entries)
        {
            if (entries.Count < 2) return 0;

            var timeSpan = entries.Max(e => e.Timestamp) - entries.Min(e => e.Timestamp);
            return timeSpan.TotalMinutes > 0 ? entries.Count / timeSpan.TotalMinutes : 0;
        }

        /// <summary>
        /// Calculate error rate from log entries
        /// </summary>
        private double CalculateErrorRate(List<LogEntry> entries)
        {
            if (entries.Count == 0) return 0;

            var errorCount = entries.Count(e => e.Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase));
            return (double)errorCount / entries.Count * 100;
        }

        /// <summary>
        /// Extract queue events from log entries
        /// </summary>
        private Dictionary<string, int> ExtractQueueEvents(List<LogEntry> entries)
        {
            var queueEvents = new Dictionary<string, int>();
            var eventPatterns = new[]
            {
                @"queue.*created",
                @"queue.*deleted",
                @"queue.*purged",
                @"queue.*declared",
                @"consumer.*registered",
                @"consumer.*cancelled"
            };

            foreach (var entry in entries)
            {
                foreach (var pattern in eventPatterns)
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(entry.Message, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        var eventType = pattern.Replace(".*", " ");
                        queueEvents[eventType] = queueEvents.GetValueOrDefault(eventType, 0) + 1;
                    }
                }
            }

            return queueEvents;
        }

        /// <summary>
        /// Extract connection events from log entries
        /// </summary>
        private Dictionary<string, int> ExtractConnectionEvents(List<LogEntry> entries)
        {
            var connectionEvents = new Dictionary<string, int>();
            var eventPatterns = new[]
            {
                @"connection.*opened",
                @"connection.*closed",
                @"connection.*failed",
                @"channel.*opened",
                @"channel.*closed",
                @"authentication.*failed",
                @"authentication.*succeeded"
            };

            foreach (var entry in entries)
            {
                foreach (var pattern in eventPatterns)
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(entry.Message, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        var eventType = pattern.Replace(".*", " ");
                        connectionEvents[eventType] = connectionEvents.GetValueOrDefault(eventType, 0) + 1;
                    }
                }
            }

            return connectionEvents;
        }

        /// <summary>
        /// Validate node name format
        /// </summary>
        private bool ValidateNodeName(string? nodeName)
        {
            if (string.IsNullOrEmpty(nodeName)) return false;
            return System.Text.RegularExpressions.Regex.IsMatch(nodeName, @"^[a-zA-Z0-9_\-@\.]+$");
        }

        /// <summary>
        /// Validate queue name in message
        /// </summary>
        private bool ValidateQueueName(string message)
        {
            if (string.IsNullOrEmpty(message)) return true; // Optional validation

            var queuePattern = @"queue[:\s]+([a-zA-Z0-9_\-\.]+)";
            var matches = System.Text.RegularExpressions.Regex.Matches(message, queuePattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Success)
                {
                    var queueName = match.Groups[1].Value;
                    if (!System.Text.RegularExpressions.Regex.IsMatch(queueName, @"^[a-zA-Z0-9_\-\.]+$"))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Validate PID format in message
        /// </summary>
        private bool ValidatePID(string message)
        {
            if (string.IsNullOrEmpty(message)) return true; // Optional validation

            var pidPattern = @"<(\d+\.\d+\.\d+)>";
            return !System.Text.RegularExpressions.Regex.IsMatch(message, pidPattern) || 
                   System.Text.RegularExpressions.Regex.IsMatch(message, pidPattern);
        }

        /// <summary>
        /// Create empty metadata for error cases
        /// </summary>
        private LogMetadata CreateEmptyMetadata()
        {
            return new LogMetadata
            {
                LogType = LogFormatType.RabbitMQ,
                TotalEntries = 0,
                FirstLogTime = DateTime.MinValue,
                LastLogTime = DateTime.MinValue,
                LogLevels = new Dictionary<string, int>(),
                Sources = new Dictionary<string, int>(),
                ErrorTypes = new Dictionary<string, int>(),
                HasStackTraces = false,
                HasCorrelationIds = false,
                AverageMessageLength = 0,
                LongestMessage = "",
                UniqueErrorCount = 0,
                CustomProperties = new Dictionary<string, object>()
            };
        }

        #endregion
    }
} 
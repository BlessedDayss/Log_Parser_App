namespace Log_Parser_App.Services
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Log_Parser_App.Interfaces;
    using Log_Parser_App.Models;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Implementation of file type detection service
    /// Uses content analysis to determine appropriate file format
    /// </summary>
    public class FileTypeDetectionService : IFileTypeDetectionService
    {
        private readonly ILogger<FileTypeDetectionService> _logger;

        public FileTypeDetectionService(ILogger<FileTypeDetectionService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Analyzes file content to determine its format type
        /// Priority: IIS -> RabbitMQ -> Standard (no FileOptions fallback)
        /// </summary>
        public async Task<LogFormatType> DetectFileTypeAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                // Priority 1: Check if it's an IIS log
                if (await IsIISLogAsync(filePath, cancellationToken))
                {
                    _logger.LogInformation("File {FilePath} detected as IIS log", filePath);
                    return LogFormatType.IIS;
                }

                // Priority 2: Check if it's a RabbitMQ log (JSON with specific structure)
                if (await IsRabbitMQLogAsync(filePath, cancellationToken))
                {
                    _logger.LogInformation("File {FilePath} detected as RabbitMQ log", filePath);
                    return LogFormatType.RabbitMQ;
                }

                // Priority 3: Default to Standard for all remaining files (txt, log, config, xml, csv)
                _logger.LogInformation("File {FilePath} detected as Standard log", filePath);
                return LogFormatType.Standard;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting file type for {FilePath}, defaulting to Standard", filePath);
                return LogFormatType.Standard;
            }
        }

        public async Task<bool> IsIISLogAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                using var reader = new StreamReader(filePath);
                for (int i = 0; i < 10 && !reader.EndOfStream; i++)
                {
                    var line = await reader.ReadLineAsync();
                    if (cancellationToken.IsCancellationRequested) return false;

                    if (line != null && (
                        line.StartsWith("#Software: Microsoft Internet Information Services") ||
                        line.StartsWith("#Version:") ||
                        line.StartsWith("#Fields:") ||
                        line.Contains("sc-status")))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking if file is IIS log: {FilePath}", filePath);
                return false;
            }
        }

        public async Task<bool> IsRabbitMQLogAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                // Check file extension first for performance
                if (!filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                using var reader = new StreamReader(filePath);
                var content = await reader.ReadToEndAsync();
                if (cancellationToken.IsCancellationRequested) return false;

                // Try to parse as JSON and look for RabbitMQ-specific fields
                var jsonDocument = JsonDocument.Parse(content);
                var root = jsonDocument.RootElement;

                // Check for RabbitMQ log structure patterns
                if (root.ValueKind == JsonValueKind.Array)
                {
                    // Array of log entries
                    var firstElement = root.EnumerateArray().FirstOrDefault();
                    return HasRabbitMQLogFields(firstElement);
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    // Single log entry or wrapped structure
                    return HasRabbitMQLogFields(root);
                }

                return false;
            }
            catch (JsonException)
            {
                // Not valid JSON, can't be RabbitMQ log
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking if file is RabbitMQ log: {FilePath}", filePath);
                return false;
            }
        }

        public async Task<bool> IsStandardLogAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                using var reader = new StreamReader(filePath);
                for (int i = 0; i < 5 && !reader.EndOfStream; i++)
                {
                    var line = await reader.ReadLineAsync();
                    if (cancellationToken.IsCancellationRequested) return false;

                    if (line != null && HasLogPattern(line))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking if file is standard log: {FilePath}", filePath);
                return false;
            }
        }

        /// <summary>
        /// Checks if JSON element has RabbitMQ-specific log fields
        /// </summary>
        private bool HasRabbitMQLogFields(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object) return false;

            // Look for RabbitMQ-specific properties
            var rabbitMQFields = new[]
            {
                "timestamp", "level", "msg", "node", "pid", "queue", "exchange", "routing_key",
                "connection", "channel", "vhost", "user", "consumer_tag", "delivery_tag"
            };

            int fieldMatches = 0;
            foreach (var property in element.EnumerateObject())
            {
                if (rabbitMQFields.Contains(property.Name.ToLowerInvariant()))
                {
                    fieldMatches++;
                }
            }

            // Need at least 3 RabbitMQ-specific fields to consider it a RabbitMQ log
            return fieldMatches >= 3;
        }

        /// <summary>
        /// Checks if line contains standard log patterns
        /// </summary>
        private bool HasLogPattern(string line)
        {
            // Common log level patterns
            var logLevels = new[] { "ERROR", "WARN", "WARNING", "INFO", "DEBUG", "TRACE", "FATAL" };
            
            // Check for timestamp patterns
            var timestampPatterns = new[]
            {
                @"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}",  // 2024-01-15 08:15:23
                @"\d{2}/\d{2}/\d{4} \d{2}:\d{2}:\d{2}",  // 01/15/2024 08:15:23
                @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}",  // 2024-01-15T08:15:23
            };

            foreach (var pattern in timestampPatterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(line, pattern))
                {
                    // Also check if it contains log levels
                    foreach (var level in logLevels)
                    {
                        if (line.Contains(level, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    return true; // Has timestamp pattern, likely a log
                }
            }

            return false;
        }
    }
} 
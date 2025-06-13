namespace Log_Parser_App.Services
{
    using Log_Parser_App.Models;
    using Log_Parser_App.Interfaces;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;


    /// <summary>
    /// Service for parsing RabbitMQ JSON logs using streaming JsonDocument approach.
    /// Supports both single JSON objects and JSON arrays with efficient memory usage.
    /// </summary>
    public class RabbitMqLogParserService : IRabbitMqLogParserService
    {
        private readonly ILogger<RabbitMqLogParserService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public RabbitMqLogParserService(ILogger<RabbitMqLogParserService> logger) {
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<RabbitMqLogEntry> ParseLogFileAsync(
            string filePath,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
            if (!File.Exists(filePath)) {
                yield break;
            }

            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            await foreach (var entry in ParseStreamAsync(fileStream, cancellationToken)) {
                yield return entry;
            }
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<RabbitMqLogEntry> ParseLogFilesAsync(
            IEnumerable<string> filePaths,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
            foreach (var filePath in filePaths) {
                await foreach (var entry in ParseLogFileAsync(filePath, cancellationToken)) {
                    yield return entry;
                }
            }
        }

        /// <inheritdoc />
        public async Task<bool> IsValidRabbitMqLogFileAsync(string filePath) {
            if (!File.Exists(filePath))
                return false;

            try {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var document = await JsonDocument.ParseAsync(fileStream);

                // Check if it's a valid JSON structure
                var root = document.RootElement;

                // Accept either array of objects or single object
                if (root.ValueKind == JsonValueKind.Array) {
                    // Check if array contains objects
                    foreach (var element in root.EnumerateArray()) {
                        if (element.ValueKind == JsonValueKind.Object)
                            return true;
                    }
                } else if (root.ValueKind == JsonValueKind.Object) {
                    return true;
                }

                return false;
            } catch (JsonException) {
                return false;
            } catch (Exception) {
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<int> GetEstimatedLogCountAsync(string filePath) {
            if (!File.Exists(filePath))
                return 0;

            try {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var document = await JsonDocument.ParseAsync(fileStream);

                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Array) {
                    return root.GetArrayLength();
                } else if (root.ValueKind == JsonValueKind.Object) {
                    return 1;
                }

                return 0;
            } catch {
                return 0;
            }
        }

        /// <summary>
        /// Internal method for parsing JSON stream with support for both arrays and single objects
        /// </summary>
        private async IAsyncEnumerable<RabbitMqLogEntry> ParseStreamAsync(
            Stream stream,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
            _logger.LogInformation("Starting to parse RabbitMQ log stream");

            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            _logger.LogInformation("JSON parsed successfully. Root element type: {ElementType}", root.ValueKind);

            if (root.ValueKind == JsonValueKind.Array) {
                var arrayLength = root.GetArrayLength();
                _logger.LogInformation("Processing JSON array with {ArrayLength} elements", arrayLength);

                // Handle JSON array of log entries
                int processedCount = 0;
                foreach (var element in root.EnumerateArray()) {
                    cancellationToken.ThrowIfCancellationRequested();

                    var entry = ParseJsonElement(element);
                    if (entry != null) {
                        processedCount++;
                        _logger.LogDebug("Successfully parsed entry {Index}: {Message}", processedCount, entry.Message ?? "No message");
                        yield return entry;
                    } else {
                        _logger.LogWarning("Failed to parse array element at index {Index}", processedCount);
                    }
                }

                _logger.LogInformation("Completed parsing array. Successfully processed {ProcessedCount} out of {TotalCount} entries", processedCount, arrayLength);
            } else if (root.ValueKind == JsonValueKind.Object) {
                _logger.LogInformation("Processing single JSON object");

                // Handle single JSON object
                var entry = ParseJsonElement(root);
                if (entry != null) {
                    _logger.LogInformation("Successfully parsed single entry: {Message}", entry.Message ?? "No message");
                    yield return entry;
                } else {
                    _logger.LogWarning("Failed to parse single JSON object");
                }
            } else {
                _logger.LogWarning("JSON root element is not an array or object, but {ElementType}", root.ValueKind);
            }
        }

        /// <summary>
        /// Parses a JsonElement into a RabbitMqLogEntry
        /// </summary>
        private RabbitMqLogEntry? ParseJsonElement(JsonElement element) {
            try {
                var rawJson = element.GetRawText();
                _logger.LogDebug("Attempting to parse JSON element: {RawJson}", rawJson);

                var entry = JsonSerializer.Deserialize<RabbitMqLogEntry>(element, _jsonOptions);
                if (entry != null) {
                    // Store raw JSON for debugging and additional data preservation
                    entry.RawJson = rawJson;
                    _logger.LogDebug("Successfully deserialized entry. Timestamp: {Timestamp}, Level: {Level}, Node: {Node}", entry.Timestamp, entry.Level, entry.Node);
                } else {
                    _logger.LogWarning("JsonSerializer.Deserialize returned null for element");
                }
                return entry;
            } catch (JsonException ex) {
                _logger.LogError(ex, "JsonException occurred while parsing element");
                // Return null for invalid entries, allowing processing to continue
                return null;
            } catch (Exception ex) {
                _logger.LogError(ex, "Unexpected exception occurred while parsing element");
                return null;
            }
        }
    }
}

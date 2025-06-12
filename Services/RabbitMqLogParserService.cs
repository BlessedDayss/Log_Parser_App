using Log_Parser_App.Models;
using Log_Parser_App.Models.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Log_Parser_App.Services
{
    public class RabbitMqLogParserService : IRabbitMqLogParserService
    {
        private readonly ILogger<RabbitMqLogParserService> _logger;

        public RabbitMqLogParserService(ILogger<RabbitMqLogParserService> logger)
        {
            _logger = logger;
        }

        public async IAsyncEnumerable<LogEntry> ParseLogFileAsync(string filePath, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                _logger.LogError("RabbitMQ log file path is invalid or file does not exist: {FilePath}", filePath);
                yield break;
            }

            await using FileStream fs = File.OpenRead(filePath);

            JsonDocumentOptions docOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

            JsonDocument document;
            try
            {
                document = await JsonDocument.ParseAsync(fs, docOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse JSON document from file {FilePath}", filePath);
                yield break;
            }

            using (document)
            {
                if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in document.RootElement.EnumerateArray())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var logEntry = ProcessElement(element);
                        if (logEntry != null)
                        {
                            if (string.IsNullOrEmpty(logEntry.Source))
                                logEntry.Source = System.IO.Path.GetFileName(filePath);
                            yield return logEntry;
                        }
                    }
                }
                else if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var logEntry = ProcessElement(document.RootElement);
                    if (logEntry != null)
                    {
                        if (string.IsNullOrEmpty(logEntry.Source))
                            logEntry.Source = System.IO.Path.GetFileName(filePath);
                        yield return logEntry;
                    }
                }
                else
                {
                    _logger.LogWarning("Unexpected JSON root element kind {Kind} in file {FilePath}", document.RootElement.ValueKind, filePath);
                }
            }
        }

        private LogEntry? ProcessElement(JsonElement element)
        {
            try
            {
                // Case 1: flat structure compatible with RabbitMqLogEntry
                if (element.TryGetProperty("timestamp", out _))
                {
                    var rabbitEntry = JsonSerializer.Deserialize<RabbitMqLogEntry>(element.GetRawText());
                    if (rabbitEntry != null)
                    {
                        rabbitEntry.RawJson = element.GetRawText();
                        return rabbitEntry.ToLogEntry();
                    }
                }

                // Case 2: nested structure with "headers" object (MassTransit fault, etc.)
                if (element.TryGetProperty("headers", out var headersElem) && headersElem.ValueKind == JsonValueKind.Object)
                {
                    string? timestampStr = TryGetString(headersElem, "MT-Fault-Timestamp")
                                           ?? TryGetString(headersElem, "timestamp");
                    DateTimeOffset timestamp = DateTimeOffset.Now;
                    if (!string.IsNullOrEmpty(timestampStr) && DateTimeOffset.TryParse(timestampStr, out var ts))
                    {
                        timestamp = ts;
                    }

                    string level = "INFO";
                    var reason = TryGetString(headersElem, "MT-Reason");
                    if (string.Equals(reason, "fault", StringComparison.OrdinalIgnoreCase))
                        level = "ERROR";

                    string message = TryGetString(headersElem, "MT-Fault-Message")
                                     ?? TryGetString(element, "message")
                                     ?? string.Empty;

                    string source = TryGetString(headersElem, "MT-Host-MachineName")
                                   ?? "RabbitMQ";

                    string queue = element.TryGetProperty("properties", out var propsElem) ? TryGetString(propsElem, "exchange") ?? "" : "";
                    string format = TryGetString(headersElem, "Content-Type") ?? (element.TryGetProperty("properties", out propsElem) ? TryGetString(propsElem, "content_type") : "") ?? "";
                    string process = TryGetString(headersElem, "MT-Host-ProcessName") ?? "";
                    string consumer = TryGetString(headersElem, "MT-Fault-ConsumerType") ?? "";

                    // Append technical details to message for visibility
                    message += $"\nQueue: {queue}\nFormat: {format}\nProcess: {process}\nServer: {source}\nConsumer: {consumer}";

                    var logEntry = new LogEntry
                    {
                        Timestamp = timestamp.DateTime,
                        Level = level,
                        Message = message,
                        Source = source,
                        RawData = element.GetRawText(),
                        StackTrace = TryGetString(headersElem, "MT-Fault-StackTrace"),
                        Recommendation = TryGetString(headersElem, "MT-Fault-StackTrace")
                    };
                    return logEntry;
                }

                // Fallback: generic processing not possible
                _logger.LogDebug("Unknown RabbitMQ log element structure");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process RabbitMQ log element");
                return null;
            }
        }

        private static string? TryGetString(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.String)
                    return prop.GetString();
            }
            return null;
        }
    }
} 
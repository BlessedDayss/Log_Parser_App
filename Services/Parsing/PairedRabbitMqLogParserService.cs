namespace Log_Parser_App.Services.Parsing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Log_Parser_App.Models;
    using Microsoft.Extensions.Logging;

    public class PairedRabbitMqLogParserService : IPairedRabbitMqLogParserService
    {
        private readonly ILogger<PairedRabbitMqLogParserService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public PairedRabbitMqLogParserService(ILogger<PairedRabbitMqLogParserService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };
        }

        public async Task<List<RabbitMqLogEntry>> ParsePairedFilesAsync(
            IEnumerable<PairedFileData> pairedFiles, 
            CancellationToken cancellationToken = default)
        {
            var results = new List<RabbitMqLogEntry>();

            foreach (var pairedFile in pairedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var entry = await ParseSinglePairedFileAsync(pairedFile, cancellationToken);
                    if (entry != null)
                    {
                        results.Add(entry);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing paired file {MessageId}", pairedFile.MessageId);
                }
            }

            return results;
        }

        public async Task<RabbitMqLogEntry?> ParseSinglePairedFileAsync(
            PairedFileData pairedFile, 
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Attempting to parse paired file: MessageId={MessageId}, Status={Status}, MainFile={MainFile}, HeadersFile={HeadersFile}",
                pairedFile.MessageId, pairedFile.Status, pairedFile.MainFilePath, pairedFile.HeadersFilePath);

            if (pairedFile.Status == PairedFileStatus.Failed)
            {
                _logger.LogWarning("Cannot parse failed paired file: {MessageId} - {Error}", 
                    pairedFile.MessageId, pairedFile.ErrorMessage);
                return null;
            }

            try
            {
                RabbitMqLogEntry? entry = null;

                if (pairedFile.Status == PairedFileStatus.UnifiedJson)
                {
                    entry = await ParseUnifiedJsonWithHeadersAsync(pairedFile, cancellationToken);
                }
                else if (pairedFile.IsComplete)
                {
                    entry = await ParseCompletePairedFileAsync(pairedFile, cancellationToken);
                }
                else if (pairedFile.HasMainFileOnly)
                {
                    entry = await ParseMainFileOnlyAsync(pairedFile.MainFilePath, cancellationToken);
                }

                if (entry != null)
                {
                    _logger.LogDebug("Successfully parsed paired file {MessageId} with status {Status}", 
                        pairedFile.MessageId, pairedFile.Status);
                }

                return entry;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing paired file {MessageId}", pairedFile.MessageId);
                return null;
            }
        }

        public async Task<RabbitMqLogEntry?> ParseMainFileOnlyAsync(
            string mainFilePath, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(mainFilePath) || !File.Exists(mainFilePath))
            {
                return null;
            }

            try
            {
                var content = await File.ReadAllTextAsync(mainFilePath, cancellationToken);
                var entry = JsonSerializer.Deserialize<RabbitMqLogEntry>(content, _jsonOptions);

                if (entry != null)
                {
                    entry.RawJson = content;
                    _logger.LogDebug("Parsed main file only: {FilePath}", mainFilePath);
                }

                return entry;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing main file: {FilePath}", mainFilePath);
                return null;
            }
        }

        private async Task<RabbitMqLogEntry?> ParseCompletePairedFileAsync(
            PairedFileData pairedFile, 
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Parsing paired files: Main={MainFile}, Headers={HeadersFile}", 
                pairedFile.MainFilePath, pairedFile.HeadersFilePath ?? "NONE");
            Console.WriteLine($"[PARSER] Processing: {Path.GetFileName(pairedFile.MainFilePath)} + {(string.IsNullOrEmpty(pairedFile.HeadersFilePath) ? "NO HEADERS" : Path.GetFileName(pairedFile.HeadersFilePath))}");

            // Initialize extracted fields
            string? processUID = null;
            string? userName = null;
            DateTimeOffset? sentTime = null;
            string? faultMessage = null;
            string? stackTrace = null;
            DateTimeOffset? faultTimestamp = null;

            // Parse main file for processUID, userName, sentTime
            if (!string.IsNullOrEmpty(pairedFile.MainFilePath) && File.Exists(pairedFile.MainFilePath))
            {
                try
                {
                    var mainContent = await File.ReadAllTextAsync(pairedFile.MainFilePath, cancellationToken);
                    _logger.LogDebug("Main file content length: {Length}", mainContent.Length);

                    var mainDoc = JsonDocument.Parse(mainContent);
                    
                    // Extract processUId from message.processUId
                    if (mainDoc.RootElement.TryGetProperty("message", out var messageElement) &&
                        messageElement.TryGetProperty("processUId", out var processUIdElement))
                    {
                        processUID = processUIdElement.GetString();
                        _logger.LogDebug("Extracted processUID: {ProcessUID}", processUID);
                    }
                    else
                    {
                        _logger.LogWarning("Could not find message.processUId in main file");
                    }

                    // Extract sentTime from sentTime
                    if (mainDoc.RootElement.TryGetProperty("sentTime", out var sentTimeElement))
                    {
                        var sentTimeStr = sentTimeElement.GetString();
                        if (DateTimeOffset.TryParse(sentTimeStr, out var parsedSentTime))
                        {
                            sentTime = parsedSentTime;
                            _logger.LogDebug("Extracted sentTime: {SentTime}", sentTime);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Could not find sentTime in main file");
                    }

                    // Extract UserName from headers.Context.userContext.UserName
                    if (mainDoc.RootElement.TryGetProperty("headers", out var headersElement) &&
                        headersElement.TryGetProperty("Context", out var contextElement) &&
                        contextElement.TryGetProperty("userContext", out var userContextElement) &&
                        userContextElement.TryGetProperty("UserName", out var userNameElement))
                    {
                        userName = userNameElement.GetString();
                        _logger.LogDebug("Extracted userName: {UserName}", userName);
                    }
                    else
                    {
                        _logger.LogWarning("Could not find headers.Context.userContext.UserName in main file");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing main file: {FilePath}", pairedFile.MainFilePath);
                }
            }

            // Parse headers file for fault data
            if (!string.IsNullOrEmpty(pairedFile.HeadersFilePath) && File.Exists(pairedFile.HeadersFilePath))
            {
                try
                {
                    var headersContent = await File.ReadAllTextAsync(pairedFile.HeadersFilePath, cancellationToken);
                    _logger.LogDebug("Headers file content length: {Length}", headersContent.Length);

                    var headersDoc = JsonDocument.Parse(headersContent);

                    // Debug: Show all properties in headers file
                    Console.WriteLine($"[PARSER] Headers file properties:");
                    foreach (var prop in headersDoc.RootElement.EnumerateObject().Take(10))
                    {
                        Console.WriteLine($"[PARSER]   {prop.Name} = {prop.Value.GetRawText().Substring(0, Math.Min(100, prop.Value.GetRawText().Length))}");
                    }

                    // Extract MT-Fault fields from headers section
                    if (headersDoc.RootElement.TryGetProperty("headers", out var headersSection))
                    {
                        // Extract MT-Fault-Message from headers section
                        if (headersSection.TryGetProperty("MT-Fault-Message", out var faultMessageElement))
                        {
                            faultMessage = faultMessageElement.GetString();
                            Console.WriteLine($"[PARSER] ✅ Found MT-Fault-Message: {faultMessage?.Substring(0, Math.Min(100, faultMessage?.Length ?? 0))}...");
                            _logger.LogDebug("Extracted faultMessage: {FaultMessage}", faultMessage?.Substring(0, Math.Min(100, faultMessage?.Length ?? 0)));
                        }
                        else
                        {
                            Console.WriteLine($"[PARSER] ? MT-Fault-Message not found in headers section");
                            _logger.LogDebug("Could not find MT-Fault-Message in headers section");
                        }

                        // Extract MT-Fault-StackTrace from headers section
                        if (headersSection.TryGetProperty("MT-Fault-StackTrace", out var stackTraceElement))
                        {
                            stackTrace = stackTraceElement.GetString();
                            Console.WriteLine($"[PARSER] ✅ Found MT-Fault-StackTrace: {stackTrace?.Length ?? 0} characters");
                            _logger.LogDebug("Extracted stackTrace length: {Length}", stackTrace?.Length ?? 0);
                        }
                        else
                        {
                            Console.WriteLine($"[PARSER] ? MT-Fault-StackTrace not found in headers section");
                            _logger.LogDebug("Could not find MT-Fault-StackTrace in headers section");
                        }

                        // Extract MT-Fault-Timestamp from headers section
                        if (headersSection.TryGetProperty("MT-Fault-Timestamp", out var faultTimestampElement))
                        {
                            var timestampStr = faultTimestampElement.GetString();
                            if (DateTimeOffset.TryParse(timestampStr, out var parsedFaultTime))
                            {
                                faultTimestamp = parsedFaultTime;
                                _logger.LogDebug("Extracted faultTimestamp: {FaultTimestamp}", faultTimestamp);
                            }
                        }
                        else
                        {
                            _logger.LogDebug("Could not find MT-Fault-Timestamp in headers section");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[PARSER] ? Headers section not found in file");
                        _logger.LogDebug("Could not find headers section in headers file");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing headers file: {FilePath}", pairedFile.HeadersFilePath);
                }
            }
            else
            {
                _logger.LogWarning("Headers file not found or path is empty: {HeadersPath}", pairedFile.HeadersFilePath);
            }

            // Create simplified entry with extracted data
            var simplifiedEntry = RabbitMqLogEntry.CreateSimplified(
                processUID: processUID,
                userName: userName,
                sentTime: sentTime,
                faultMessage: faultMessage,
                stackTrace: stackTrace,
                faultTimestamp: faultTimestamp
            );

            _logger.LogInformation("Created entry - ProcessUID: {ProcessUID}, UserName: {UserName}, SentTime: {SentTime}, HasFaultMessage: {HasFaultMessage}, HasStackTrace: {HasStackTrace}",
                processUID, userName, sentTime, !string.IsNullOrEmpty(faultMessage), !string.IsNullOrEmpty(stackTrace));

            return simplifiedEntry;
        }

        private Task<RabbitMqLogEntry?> TryParseAsUnifiedJsonAsync(string content, string filePath)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var doc = JsonDocument.Parse(content);
                    var root = doc.RootElement;

                    // Check if this looks like a unified RabbitMQ message (has message, sentTime, headers)
                    if (!root.TryGetProperty("message", out _) || 
                        !root.TryGetProperty("sentTime", out _) ||
                        !root.TryGetProperty("headers", out _))
                    {
                        return null;
                    }

                    // Create base entry
                    var entry = new RabbitMqLogEntry
                    {
                        RawJson = content
                    };

                    // Extract processUId from message.processUId
                    if (root.TryGetProperty("message", out var messageElement) &&
                        messageElement.TryGetProperty("processUId", out var processUIdElement))
                    {
                        entry.ProcessUID = processUIdElement.GetString();
                    }

                    // Extract sentTime from sentTime
                    if (root.TryGetProperty("sentTime", out var sentTimeElement))
                    {
                        if (DateTimeOffset.TryParse(sentTimeElement.GetString(), out var sentTime))
                        {
                            entry.SentTime = sentTime;
                            entry.Timestamp = sentTime.DateTime;
                        }
                    }

                    // Extract UserName from headers.Context.userContext.UserName
                    if (root.TryGetProperty("headers", out var headersElement) &&
                        headersElement.TryGetProperty("Context", out var contextElement) &&
                        contextElement.TryGetProperty("userContext", out var userContextElement) &&
                        userContextElement.TryGetProperty("UserName", out var userNameElement))
                    {
                        entry.UserName = userNameElement.GetString();
                    }

                    // Extract other standard fields - we'll set via Properties for consistency
                    if (root.TryGetProperty("messageId", out var messageIdElement))
                    {
                        if (entry.Properties == null)
                            entry.Properties = new RabbitMqProperties();
                        entry.Properties.MessageId = messageIdElement.GetString();
                    }

                    if (root.TryGetProperty("messageType", out var messageTypeElement) && messageTypeElement.ValueKind == JsonValueKind.Array)
                    {
                        var types = messageTypeElement.EnumerateArray()
                            .Where(x => x.ValueKind == JsonValueKind.String)
                            .Select(x => x.GetString())
                            .Where(x => !string.IsNullOrEmpty(x))
                            .ToArray();
                        
                        if (types.Length > 0)
                        {
                            // Use the most specific message type (last one usually)
                            var mainType = types.LastOrDefault();
                            if (!string.IsNullOrEmpty(mainType))
                            {
                                // Clean up the URN format to get readable name
                                if (mainType.StartsWith("urn:message:"))
                                {
                                    var cleanType = mainType.Substring("urn:message:".Length);
                                    entry.Message = cleanType;
                                }
                                else
                                {
                                    entry.Message = mainType;
                                }
                            }
                            entry.Level = "INFO"; // Default level for message types
                        }
                    }
                    
                    // Also try to extract destination for Node if not set later
                    if (root.TryGetProperty("destinationAddress", out var destinationElement))
                    {
                        var destination = destinationElement.GetString();
                        if (!string.IsNullOrEmpty(destination))
                        {
                            // Extract queue name from RabbitMQ URL for Node
                            var match = Regex.Match(destination, @"/([^/\?]+)(\?|$)");
                            if (match.Success)
                            {
                                entry.Node = match.Groups[1].Value;
                            }
                        }
                    }

                    return entry;
                }
                catch
                {
                    // If parsing fails, this is not a unified JSON format
                    return null;
                }
            });
        }

        private async Task<RabbitMqLogEntry?> ParseUnifiedJsonWithHeadersAsync(
            PairedFileData pairedFile, 
            CancellationToken cancellationToken)
        {
            Console.WriteLine($"[PARSER] Parsing unified JSON with headers: {pairedFile.MessageId}");
            
            var mainContent = await File.ReadAllTextAsync(pairedFile.MainFilePath, cancellationToken);
            
            // Parse as unified JSON first
            var entry = await TryParseAsUnifiedJsonAsync(mainContent, pairedFile.MainFilePath);
            if (entry == null)
            {
                _logger.LogWarning("Failed to parse unified JSON for {MessageId}", pairedFile.MessageId);
                return null;
            }

            // If headers file exists, merge additional information
            if (!string.IsNullOrEmpty(pairedFile.HeadersFilePath) && File.Exists(pairedFile.HeadersFilePath))
            {
                try
                {
                    var headersContent = await File.ReadAllTextAsync(pairedFile.HeadersFilePath, cancellationToken);
                    using var headersDoc = JsonDocument.Parse(headersContent);
                    
                    // Merge any additional headers information that might not be in main file
                    await MergeAdditionalHeadersAsync(entry, headersDoc.RootElement);
                    
                    Console.WriteLine($"[PARSER] Enhanced unified JSON with additional headers for {pairedFile.MessageId}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to merge additional headers for {MessageId}, using main file only", pairedFile.MessageId);
                }
            }

            _logger.LogDebug("Parsed unified JSON with headers: ProcessUID={ProcessUID}, UserName={UserName}, SentTime={SentTime}",
                entry.ProcessUID, entry.UserName, entry.SentTime);
            
            return entry;
        }

        private Task MergeAdditionalHeadersAsync(RabbitMqLogEntry entry, JsonElement headersRoot)
        {
            // Merge additional information from separate headers file
            
            if (headersRoot.TryGetProperty("headers", out var headersElement))
            {
                // Extract ConsumerType from MT-Fault-ConsumerType for Node field
                if (headersElement.TryGetProperty("MT-Fault-ConsumerType", out var consumerTypeElement))
                {
                    entry.Node = consumerTypeElement.GetString();
                }
                
                // Extract ExceptionType from MT-Fault-ExceptionType for StackTrace field
                if (headersElement.TryGetProperty("MT-Fault-ExceptionType", out var exceptionTypeElement))
                {
                    var exceptionType = exceptionTypeElement.GetString();
                    
                    // Extract exception details for better StackTrace
                    if (headersElement.TryGetProperty("MT-Fault-Message", out var faultMessageElement))
                    {
                        var faultMessage = faultMessageElement.GetString();
                        entry.StackTrace = $"{exceptionType}: {faultMessage}";
                    }
                    else
                    {
                        entry.StackTrace = exceptionType;
                    }
                }
                
                // Extract other fault details
                if (headersElement.TryGetProperty("MT-Fault-StackTrace", out var stackTraceElement))
                {
                    var fullStackTrace = stackTraceElement.GetString();
                    if (!string.IsNullOrEmpty(fullStackTrace))
                    {
                        // Append full stack trace if available
                        if (!string.IsNullOrEmpty(entry.StackTrace))
                        {
                            entry.StackTrace += "\n" + fullStackTrace;
                        }
                        else
                        {
                            entry.StackTrace = fullStackTrace;
                        }
                    }
                }
                
                // Extract Content-Type and other metadata for better Message field
                if (headersElement.TryGetProperty("Content-Type", out var contentTypeElement))
                {
                    var contentType = contentTypeElement.GetString();
                    if (!string.IsNullOrEmpty(contentType) && string.IsNullOrEmpty(entry.Message))
                    {
                        entry.Message = $"Content-Type: {contentType}";
                    }
                }
            }
            
            return Task.CompletedTask;
        }

        // Test method to debug header parsing
        public async Task<RabbitMqLogEntry?> TestHeaderParsingAsync(string headersFilePath)
        {
            if (!File.Exists(headersFilePath))
            {
                _logger.LogError("Test headers file not found: {FilePath}", headersFilePath);
                return null;
            }

            try
            {
                var headersContent = await File.ReadAllTextAsync(headersFilePath);
                _logger.LogInformation("Test headers content: {Content}", headersContent);

                var headersDoc = JsonDocument.Parse(headersContent);
                _logger.LogInformation("Headers JSON parsed successfully");

                string? faultMessage = null;
                string? stackTrace = null;
                DateTimeOffset? faultTimestamp = null;

                // Debug: List all properties in the headers file
                foreach (var property in headersDoc.RootElement.EnumerateObject())
                {
                    _logger.LogInformation("Found property: {Name} = {Value}", 
                        property.Name, property.Value.GetRawText());
                }

                // Extract MT-Fault fields from headers section
                if (headersDoc.RootElement.TryGetProperty("headers", out var headersSection))
                {
                    // Extract MT-Fault-Message from headers section
                    if (headersSection.TryGetProperty("MT-Fault-Message", out var faultMessageElement))
                    {
                        faultMessage = faultMessageElement.GetString();
                        _logger.LogInformation("SUCCESS: Extracted faultMessage: {FaultMessage}", faultMessage);
                    }
                    else
                    {
                        _logger.LogInformation("INFO: Could not find MT-Fault-Message in headers section");
                    }

                    // Extract MT-Fault-StackTrace from headers section
                    if (headersSection.TryGetProperty("MT-Fault-StackTrace", out var stackTraceElement))
                    {
                        stackTrace = stackTraceElement.GetString();
                        _logger.LogInformation("SUCCESS: Extracted stackTrace length: {Length}", stackTrace?.Length ?? 0);
                    }
                    else
                    {
                        _logger.LogInformation("INFO: Could not find MT-Fault-StackTrace in headers section");
                    }

                    // Extract MT-Fault-Timestamp from headers section
                    if (headersSection.TryGetProperty("MT-Fault-Timestamp", out var faultTimestampElement))
                    {
                        var timestampStr = faultTimestampElement.GetString();
                        if (DateTimeOffset.TryParse(timestampStr, out var parsedFaultTime))
                        {
                            faultTimestamp = parsedFaultTime;
                            _logger.LogInformation("SUCCESS: Extracted faultTimestamp: {FaultTimestamp}", faultTimestamp);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("INFO: Could not find MT-Fault-Timestamp in headers section");
                    }
                }
                else
                {
                    _logger.LogWarning("FAILED: Could not find headers section in headers file");
                }

                // Create test entry
                var testEntry = RabbitMqLogEntry.CreateSimplified(
                    faultMessage: faultMessage,
                    stackTrace: stackTrace,
                    faultTimestamp: faultTimestamp
                );

                _logger.LogInformation("Test entry created - EffectiveMessage: {Message}, EffectiveStackTrace: {StackTrace}",
                    testEntry.EffectiveMessage, testEntry.EffectiveStackTrace?.Substring(0, Math.Min(100, testEntry.EffectiveStackTrace?.Length ?? 0)));

                return testEntry;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in test header parsing");
                return null;
            }
        }
    }
}

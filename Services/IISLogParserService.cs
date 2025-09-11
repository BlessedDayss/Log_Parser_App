namespace Log_Parser_App.Services
{
    using Log_Parser_App.Models;
    using Log_Parser_App.Interfaces;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;

    public class IISLogParserService : IIISLogParserService
    {
        private readonly ILogger<IISLogParserService> _logger;

        public IISLogParserService(ILogger<IISLogParserService> logger) {
            _logger = logger;
        }

        public async IAsyncEnumerable<IisLogEntry> ParseLogFileAsync(string filePath, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) {
                _logger.LogError("IIS log file path is invalid or file does not exist: {FilePath}", filePath);
                yield break;
            }

            StreamReader? streamReader = null;
            try {
                streamReader = new StreamReader(filePath);
            } catch (Exception ex) {
                _logger.LogError(ex, "Error opening IIS log file {FilePath}.", filePath);
                yield break;
            }

            await foreach (var entry in ProcessStreamAsync(streamReader, cancellationToken, filePath)) {
                yield return entry;
            }
        }

        private async IAsyncEnumerable<IisLogEntry> ProcessStreamAsync(StreamReader streamReader, [EnumeratorCancellation] CancellationToken cancellationToken, string filePath) {
            List<string>? fieldNames = null;
            int lineNumber = 0;
            int dataLinesSeen = 0;
            
            try {
                string? line;
                while ((line = await streamReader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null) {
                    cancellationToken.ThrowIfCancellationRequested();
                    lineNumber++;

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (line.StartsWith("#Fields:")) {
                        fieldNames = line.Substring("#Fields:".Length).Trim().Split(' ').ToList();
                        _logger.LogInformation("IIS Log Fields detected in {FilePath}: {Fields}", filePath, string.Join(", ", fieldNames));
                        continue;
                    }

                    if (line.StartsWith("#")) {
                        _logger.LogDebug("Skipping comment line {LineNumber} in {FilePath}: {LineContent}", lineNumber, filePath, line);
                        continue;
                    }

                    // If no #Fields directive found but we have data lines, try to infer standard IIS format
                    if (fieldNames == null && dataLinesSeen == 0) {
                        // Try to infer field structure from first data line
                        var parts = line.Split(' ');
                        if (parts.Length >= 10) { // Minimum expected fields for IIS log
                            // Use standard IIS Extended Log Format field order
                            fieldNames = GetStandardIISFields(parts.Length);
                            _logger.LogInformation("No #Fields directive found in {FilePath}. Inferring standard IIS format with {FieldCount} fields: {Fields}", 
                                filePath, fieldNames.Count, string.Join(", ", fieldNames));
                        }
                    }

                    if (fieldNames == null) {
                        _logger.LogWarning("Skipping data line {LineNumber} in {FilePath} because field structure could not be determined. Line: {LineContent}", 
                            lineNumber, filePath, line);
                        continue;
                    }

                    dataLinesSeen++;
                    IisLogEntry? entry = ParseLogLine(line, fieldNames, lineNumber, filePath);
                    if (entry != null) {
                        yield return entry;
                    }
                }
            } finally {
                streamReader?.Dispose();
                _logger.LogDebug("StreamReader for {FilePath} disposed.", filePath);
            }
        }

        private List<string> GetStandardIISFields(int fieldCount) {
            var standardFields = new List<string> {
                "date",
                "time",
                "cs-method",
                "cs-uri-query",
                "s-port",
                "cs-username",
                "time-taken",
                "cs(user-agent)",
                "sc-status",
                "s-ip",
                "cs-uri-stem",
                "c-ip",
                "cs(referer)",
                "sc-substatus",
                "sc-win32-status"
            };

            return standardFields.Take(fieldCount).ToList();
        }

        private IisLogEntry? ParseLogLine(string line, List<string> fieldNames, int lineNumber, string filePath) {
            try {
                var values = SplitLogLine(line);
                
                if (values.Length != fieldNames.Count) {
                    if (values.Length < fieldNames.Count) {
                        var paddedValues = new string[fieldNames.Count];
                        Array.Copy(values, paddedValues, Math.Min(values.Length, fieldNames.Count));
                        for (int i = values.Length; i < fieldNames.Count; i++) {
                            paddedValues[i] = "-";
                        }
                        values = paddedValues;
                    } else if (values.Length > fieldNames.Count) {
                        var truncatedValues = new string[fieldNames.Count];
                        Array.Copy(values, truncatedValues, fieldNames.Count);
                        values = truncatedValues;
                    }
                }

                var entry = new IisLogEntry { RawLine = line };
                string? dateStr = null;
                string? timeStr = null;
                
                for (int i = 0; i < fieldNames.Count && i < values.Length; i++) {
                    var fieldNameKey = fieldNames[i].ToLowerInvariant();
                    var value = values[i];

                    if (fieldNameKey == "date") {
                        dateStr = value;
                        continue;
                    }
                    if (fieldNameKey == "time") {
                        timeStr = value;
                        continue;
                    }

                    if (entry.DateTime == null && dateStr != null && timeStr != null) {
                        if (DateTimeOffset.TryParse($"{dateStr} {timeStr}",
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                                out var dateTimeOffset)) {
                            entry.DateTime = dateTimeOffset;
                        }
                    }
                    SetEntryProperty(entry, fieldNameKey, value, lineNumber, filePath);
                }

                if (entry.DateTime == null && dateStr != null && timeStr != null) {
                    if (DateTimeOffset.TryParse($"{dateStr} {timeStr}",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                            out var dateTimeOffset)) {
                        entry.DateTime = dateTimeOffset;
                    }
                }
                return entry;
            } catch (Exception ex) {
                _logger.LogError(ex, "Error parsing IIS log entry at line {LineNumber} in {FilePath}. Line: {LineContent}", lineNumber, filePath, line);
                return new IisLogEntry { RawLine = line, ClientIPAddress = $"Error parsing line: {ex.Message}" };
            }
        }

        private string[] SplitLogLine(string line) {
            var parts = new List<string>();
            var currentPart = new System.Text.StringBuilder();
            bool inQuotes = false;
            
            for (int i = 0; i < line.Length; i++) {
                char c = line[i];
                
                if (c == '"') {
                    inQuotes = !inQuotes;
                    currentPart.Append(c);
                } else if (c == ' ' && !inQuotes) {
                    if (currentPart.Length > 0) {
                        parts.Add(currentPart.ToString());
                        currentPart.Clear();
                    }
                } else {
                    currentPart.Append(c);
                }
            }
            
            if (currentPart.Length > 0) {
                parts.Add(currentPart.ToString());
            }
            
            return parts.ToArray();
        }

        private void SetEntryProperty(IisLogEntry entry, string fieldName, string value, int lineNumber, string filePath) {
            if (value == "-") {
                value = string.Empty;
            }

            try {
                switch (fieldName) {
                    case "date":
                    case "time":
                        break;
                    case "s-sitename":
                        entry.ServiceName = value;
                        break;
                    case "s-computername":
                        entry.ServerName = value;
                        break;
                    case "cs-method":
                        entry.Method = value;
                        break;
                    case "cs-uri-stem":
                        entry.UriStem = value;
                        break;
                    case "cs-uri-query":
                        entry.UriQuery = value;
                        break;
                    case "s-port":
                        if (int.TryParse(value, out int port))
                            entry.ServerPort = port;
                        else if (!string.IsNullOrEmpty(value))
                            _logger.LogWarning("Could not parse s-port '{Value}' to int at line {LineNumber} in {FilePath}.", value, lineNumber, filePath);
                        break;
                    case "cs-username":
                        entry.UserName = value;
                        break;
                    case "c-ip":
                        entry.ClientIPAddress = value;
                        break;
                    case "cs-version":
                    case "cs(version)":
                        entry.ProtocolVersion = value;
                        break;
                    case "cs(user-agent)":
                        entry.UserAgent = value == "-" ? "Not Specified" : value;
                        break;
                    case "cs(referer)":
                        break;
                    case "cs(cookie)":
                        entry.Cookie = value;
                        break;
                    case "cs-host":
                        entry.Host = value;
                        break;
                    case "sc-status":
                        if (int.TryParse(value, out int status))
                            entry.HttpStatus = status;
                        else if (!string.IsNullOrEmpty(value))
                            _logger.LogWarning("Could not parse sc-status '{Value}' to int at line {LineNumber} in {FilePath}.", value, lineNumber, filePath);
                        break;
                    case "sc-substatus":
                        break;
                    case "sc-win32-status":
                        if (int.TryParse(value, out int win32Status))
                            entry.Win32Status = win32Status;
                        else if (!string.IsNullOrEmpty(value))
                            _logger.LogWarning("Could not parse sc-win32-status '{Value}' to int at line {LineNumber} in {FilePath}.", value, lineNumber, filePath);
                        break;
                    case "sc-bytes":
                        if (long.TryParse(value, out long bytesSent))
                            entry.BytesSent = bytesSent;
                        else if (!string.IsNullOrEmpty(value))
                            _logger.LogWarning("Could not parse sc-bytes '{Value}' to long at line {LineNumber} in {FilePath}.", value, lineNumber, filePath);
                        break;
                    case "cs-bytes":
                        if (long.TryParse(value, out long bytesReceived))
                            entry.BytesReceived = bytesReceived;
                        else if (!string.IsNullOrEmpty(value))
                            _logger.LogWarning("Could not parse cs-bytes '{Value}' to long at line {LineNumber} in {FilePath}.", value, lineNumber, filePath);
                        break;
                    case "time-taken":
                        if (int.TryParse(value, out int timeTaken))
                            entry.TimeTaken = timeTaken;
                        else if (!string.IsNullOrEmpty(value))
                            _logger.LogWarning("Could not parse time-taken '{Value}' to int at line {LineNumber} in {FilePath}.", value, lineNumber, filePath);
                        break;
                    case "s-ip":
                        entry.ServerIPAddress = value;
                        break;
                    default:
                        break;
                }
            } catch (Exception ex) {
                _logger.LogError(ex, "Error setting property for field '{FieldName}' with value '{Value}' at line {LineNumber} in {FilePath}.", fieldName, value, lineNumber, filePath);
            }
        }
    }
}

using Log_Parser_App.Models;
using Log_Parser_App.Models.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Log_Parser_App.Services
{
    public class IISLogParserService : IIISLogParserService
    {
        private readonly ILogger<IISLogParserService> _logger;

        public IISLogParserService(ILogger<IISLogParserService> logger)
        {
            _logger = logger;
        }

        public async IAsyncEnumerable<IISLogEntry> ParseLogFileAsync(string filePath, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                _logger.LogError("IIS log file path is invalid or file does not exist: {FilePath}", filePath);
                yield break;
            }

            StreamReader? streamReader = null;
            try
            {
                streamReader = new StreamReader(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening IIS log file {FilePath}.", filePath);
                yield break;
            }

            await foreach (var entry in ProcessStreamAsync(streamReader, cancellationToken, filePath))
            {
                yield return entry;
            }
        }

        private async IAsyncEnumerable<IISLogEntry> ProcessStreamAsync(StreamReader streamReader, [EnumeratorCancellation] CancellationToken cancellationToken, string filePath)
        {
            List<string>? fieldNames = null;
            int lineNumber = 0;
            try
            {
                string? line;
                while ((line = await streamReader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    lineNumber++;

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (line.StartsWith("#Fields:"))
                    {
                        fieldNames = line.Substring("#Fields:".Length).Trim().Split(' ').ToList();
                        _logger.LogInformation("IIS Log Fields detected in {FilePath}: {Fields}", filePath, string.Join(", ", fieldNames));
                        continue;
                    }

                    if (line.StartsWith("#"))
                    {
                        _logger.LogDebug("Skipping comment line {LineNumber} in {FilePath}: {LineContent}", lineNumber, filePath, line);
                        continue;
                    }

                    if (fieldNames == null)
                    {
                        _logger.LogWarning("Skipping data line {LineNumber} in {FilePath} because #Fields directive has not been found yet.", lineNumber, filePath);
                        continue;
                    }

                    IISLogEntry? entry = ParseLogLine(line, fieldNames, lineNumber, filePath);
                    if (entry != null)
                    {
                        yield return entry;
                    }
                }
            }
            finally
            {
                streamReader?.Dispose();
                _logger.LogDebug("StreamReader for {FilePath} disposed.", filePath);
            }
        }

        private IISLogEntry? ParseLogLine(string line, List<string> fieldNames, int lineNumber, string filePath)
        {
            try
            {
                var values = line.Split(' ');
                if (values.Length != fieldNames.Count)
                {
                    _logger.LogWarning("Skipping data line {LineNumber} in {FilePath} due to mismatch between field count ({FieldCount}) and value count ({ValueCount}). Line: {LineContent}",
                        lineNumber, filePath, fieldNames.Count, values.Length, line);
                    return null;
                }

                var entry = new IISLogEntry { RawLine = line };
                string? dateStr = null;
                string? timeStr = null;

                for (int i = 0; i < fieldNames.Count; i++)
                {
                    var fieldNameKey = fieldNames[i].ToLowerInvariant();
                    var value = values[i];

                    if (fieldNameKey == "date") { dateStr = value; continue; }
                    if (fieldNameKey == "time") { timeStr = value; continue; }
                    
                    if (entry.DateTime == null && dateStr != null && timeStr != null) 
                    {
                        if (DateTimeOffset.TryParse($"{dateStr} {timeStr}", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dateTimeOffset))
                        {
                            entry.DateTime = dateTimeOffset;
                        }
                        else
                        {
                            _logger.LogWarning("Could not parse date/time '{Date} {Time}' at line {LineNumber} in {FilePath}.", dateStr, timeStr, lineNumber, filePath);
                        }
                    }
                    SetEntryProperty(entry, fieldNameKey, value, lineNumber, filePath);
                }
                
                if (entry.DateTime == null && dateStr != null && timeStr != null)
                {
                    if (DateTimeOffset.TryParse($"{dateStr} {timeStr}", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dateTimeOffset))
                    {
                        entry.DateTime = dateTimeOffset;
                    }
                    else
                    {
                        _logger.LogWarning("Could not parse date/time '{Date} {Time}' (end of loop) at line {LineNumber} in {FilePath}.", dateStr, timeStr, lineNumber, filePath);
                    }
                }
                return entry;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing IIS log entry at line {LineNumber} in {FilePath}. Line: {LineContent}", lineNumber, filePath, line);
                return new IISLogEntry { RawLine = line, ClientIPAddress = $"Error parsing line: {ex.Message}" };
            }
        }

        private void SetEntryProperty(IISLogEntry entry, string fieldName, string value, int lineNumber, string filePath)
        {
            if (value == "-") 
            {
                value = string.Empty; 
            }

            try
            {
                switch (fieldName)
                {
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
                        if (int.TryParse(value, out int port)) entry.ServerPort = port;
                        else if (!string.IsNullOrEmpty(value)) _logger.LogWarning("Could not parse s-port '{Value}' to int at line {LineNumber} in {FilePath}.", value, lineNumber, filePath);
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
                        entry.UserAgent = value;
                        break;
                    case "cs(cookie)":
                        entry.Cookie = value;
                        break;
                    case "cs(referer)":
                    case "cs-referer": 
                        entry.Referer = value;
                        break;
                    case "cs-host":
                        entry.Host = value;
                        break;
                    case "sc-status":
                        if (int.TryParse(value, out int status)) entry.HttpStatus = status;
                        else if (!string.IsNullOrEmpty(value)) _logger.LogWarning("Could not parse sc-status '{Value}' to int at line {LineNumber} in {FilePath}.", value, lineNumber, filePath);
                        break;
                    case "sc-substatus": 
                        break;
                    case "sc-win32-status":
                        if (int.TryParse(value, out int win32Status)) entry.Win32Status = win32Status;
                        else if (!string.IsNullOrEmpty(value)) _logger.LogWarning("Could not parse sc-win32-status '{Value}' to int at line {LineNumber} in {FilePath}.", value, lineNumber, filePath);
                        break;
                    case "sc-bytes":
                        if (long.TryParse(value, out long bytesSent)) entry.BytesSent = bytesSent;
                        else if (!string.IsNullOrEmpty(value)) _logger.LogWarning("Could not parse sc-bytes '{Value}' to long at line {LineNumber} in {FilePath}.", value, lineNumber, filePath);
                        break;
                    case "cs-bytes":
                        if (long.TryParse(value, out long bytesReceived)) entry.BytesReceived = bytesReceived;
                        else if (!string.IsNullOrEmpty(value)) _logger.LogWarning("Could not parse cs-bytes '{Value}' to long at line {LineNumber} in {FilePath}.", value, lineNumber, filePath);
                        break;
                    case "time-taken":
                        if (int.TryParse(value, out int timeTaken)) entry.TimeTaken = timeTaken;
                        else if (!string.IsNullOrEmpty(value)) _logger.LogWarning("Could not parse time-taken '{Value}' to int at line {LineNumber} in {FilePath}.", value, lineNumber, filePath);
                        break;
                    case "s-ip":
                        entry.ServerIPAddress = value;
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting property for field '{FieldName}' with value '{Value}' at line {LineNumber} in {FilePath}.", fieldName, value, lineNumber, filePath);
            }
        }
    }
} 
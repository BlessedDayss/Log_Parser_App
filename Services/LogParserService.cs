using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LogParserApp.Models;
using Microsoft.Extensions.Logging;

namespace LogParserApp.Services
{
    /// <summary>
    /// Implementation of the log parsing service
    /// </summary>
    public class LogParserService : ILogParserService
    {
        private readonly ILogger<LogParserService> _logger;
        private static readonly Regex CommonLogFormat = new(
            @"^(\S+) \S+ \S+ \[([^:]+)[^\]]+\] ""[^""]*"" (\d+) (\d+)",
            RegexOptions.Compiled);
        
        private static readonly Regex StandardLogFormat = new(
            @"^(\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}(?:\.\d+)?)\s+(\w+)\s+\[([^\]]+)\]\s+(.*)",
            RegexOptions.Compiled);

        private static readonly Regex ConfigUpdateLogFormat = new(
            @"\[(\d{2}:\d{2}:\d{2})\]\s+(\d+)\)\s+(?:\[?([^\]]+)\]?)\s+(?:when|Error|Warning)(.*)",
            RegexOptions.Compiled);

        private static readonly string[] DateFormats = {
            "yyyy-MM-dd HH:mm:ss,fff",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-dd HH:mm:ss",
            "dd/MMM/yyyy:HH:mm:ss",
            "yyyy/MM/dd HH:mm:ss"
        };

        public LogParserService(ILogger<LogParserService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<LogEntry>> ParseLogFileAsync(string filePath)
        {
            _logger.LogInformation("Parsing log file: {FilePath}", filePath);
            
            if (!File.Exists(filePath))
            {
                _logger.LogError("File not found: {FilePath}", filePath);
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            // Check file size
            var fileInfo = new FileInfo(filePath);
            var fileSizeMb = fileInfo.Length / (1024.0 * 1024.0);
            _logger.LogInformation("File size: {FileSizeMB:F2} MB", fileSizeMb);
            
            // Optimized loading for large files
            var logFormat = await DetectLogFormatAsync(filePath);
            var logEntries = new List<LogEntry>();

            // Use optimized buffered reading
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 
                                                  bufferSize: 65536, useAsync: true);
            using var reader = new StreamReader(fileStream, detectEncodingFromByteOrderMarks: true);
            
            string? line;
            int lineCount = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineCount++;
                    
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        var entry = ParseLogLine(line, logFormat, filePath, lineCount);
                        if (entry != null)
                        {
                            logEntries.Add(entry);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parsing log line {LineNumber}: {Line}", lineCount, line);
                    }
                    
                    // Progress report for large files
                    if (lineCount % 50000 == 0)
                    {
                        _logger.LogInformation("Processing log file: parsed {Count} lines so far ({ElapsedMs} ms)", 
                                              lineCount, sw.ElapsedMilliseconds);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading log file: {FilePath}", filePath);
                throw;
            }
            finally
            {
                sw.Stop();
            }

            _logger.LogInformation("Parsed {Count} log entries from {LineCount} lines in {ElapsedMs} ms", 
                                  logEntries.Count, lineCount, sw.ElapsedMilliseconds);
            return logEntries;
        }

        /// <inheritdoc />
        public Task<IEnumerable<LogEntry>> ExecuteQueryAsync(IEnumerable<LogEntry> logEntries, string query)
        {
            _logger.LogInformation("Executing query: {Query}", query);
            
            if (string.IsNullOrWhiteSpace(query))
                return Task.FromResult(logEntries);

            try
            {
                // Normalize query for more stable processing
                string normalizedQuery = query.ToUpperInvariant();
                
                // Check if query contains SELECT
                if (normalizedQuery.Contains("SELECT"))
                {
                    // If query only contains SELECT without WHERE or FROM, return all logs
                    if (!normalizedQuery.Contains("WHERE") && !normalizedQuery.Contains("FROM"))
                    {
                        _logger.LogInformation("Query contains only SELECT, returning all logs");
                        return Task.FromResult(logEntries);
                    }
                }
                
                // Simple implementation of SQL-like query with filtering
                bool hasWhereCondition = normalizedQuery.Contains("WHERE");
                bool hasFromCondition = normalizedQuery.Contains("FROM");
                
                // If query contains only WHERE or FROM without =, perform simple search
                if (!hasWhereCondition && !hasFromCondition && query.Contains("="))
                {
                    try 
                    {
                        // Simple search in format "Field = 'Value'"
                        var parts = query.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var fieldName = parts[0].Trim().Trim('"', '\'');
                            var fieldValue = parts[1].Trim().Trim('"', '\'');
                            
                            // Select the appropriate field for filtering
                            if (fieldName.Equals("Message", StringComparison.OrdinalIgnoreCase) || 
                                fieldName.Equals("Message", StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.LogInformation("Simple search by message: '{0}'", fieldValue);
                                return Task.FromResult(logEntries.Where(e => e.Message.Equals(fieldValue, StringComparison.OrdinalIgnoreCase) || 
                                                              e.Message.Contains(fieldValue, StringComparison.OrdinalIgnoreCase)));
                            }
                            else if (fieldName.Equals("Level", StringComparison.OrdinalIgnoreCase) || 
                                     fieldName.Equals("Level", StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.LogInformation("Simple search by level: '{0}'", fieldValue);
                                return Task.FromResult(logEntries.Where(e => e.Level.Equals(fieldValue, StringComparison.OrdinalIgnoreCase)));
                            }
                            else if (fieldName.Equals("Source", StringComparison.OrdinalIgnoreCase) || 
                                     fieldName.Equals("Source", StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.LogInformation("Simple search by source: '{0}'", fieldValue);
                                return Task.FromResult(logEntries.Where(e => e.Source.Equals(fieldValue, StringComparison.OrdinalIgnoreCase)));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error in simple search");
                    }
                }
                
                if (hasWhereCondition || hasFromCondition)
                {
                    string conditionKeyword = hasWhereCondition ? "WHERE" : "FROM";
                    var whereIndex = normalizedQuery.IndexOf(conditionKeyword) + conditionKeyword.Length;
                    var wherePart = query.Substring(whereIndex).Trim();
                    var normalizedWherePart = normalizedQuery.Substring(whereIndex).Trim();
                    
                    // Filter by Level
                    if ((normalizedWherePart.Contains("LEVEL", StringComparison.OrdinalIgnoreCase) || 
                        normalizedWherePart.Contains("LEVEL", StringComparison.OrdinalIgnoreCase)) && 
                        normalizedWherePart.Contains("="))
                    {
                        var level = ExtractValueFromCondition(normalizedWherePart, "LEVEL") 
                            ?? ExtractValueFromCondition(normalizedWherePart, "LEVEL");
                        if (!string.IsNullOrEmpty(level))
                            logEntries = logEntries.Where(e => e.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
                    }
                    
                    // Filter by Source
                    if ((normalizedWherePart.Contains("SOURCE", StringComparison.OrdinalIgnoreCase) || 
                        normalizedWherePart.Contains("SOURCE", StringComparison.OrdinalIgnoreCase)) && 
                        normalizedWherePart.Contains("="))
                    {
                        var source = ExtractValueFromCondition(normalizedWherePart, "SOURCE") 
                            ?? ExtractValueFromCondition(normalizedWherePart, "SOURCE");
                        if (!string.IsNullOrEmpty(source))
                            logEntries = logEntries.Where(e => e.Source.Equals(source, StringComparison.OrdinalIgnoreCase));
                    }
                    
                    // Filter by message text
                    if (normalizedWherePart.Contains("MESSAGE", StringComparison.OrdinalIgnoreCase) || 
                        normalizedWherePart.Contains("MESSAGE", StringComparison.OrdinalIgnoreCase))
                    {
                        // Filter LIKE
                        if (normalizedWherePart.Contains("LIKE"))
                        {
                            var messageText = ExtractValueFromLikeCondition(normalizedWherePart, "MESSAGE") 
                                ?? ExtractValueFromLikeCondition(normalizedWherePart, "MESSAGE");
                            if (!string.IsNullOrEmpty(messageText))
                                logEntries = logEntries.Where(e => e.Message.Contains(messageText, StringComparison.OrdinalIgnoreCase));
                        }
                        // Filter by exact match
                        else if (normalizedWherePart.Contains("="))
                        {
                            var messageText = ExtractValueFromCondition(normalizedWherePart, "MESSAGE") 
                                ?? ExtractValueFromCondition(normalizedWherePart, "MESSAGE");
                            if (!string.IsNullOrEmpty(messageText))
                            {
                                _logger.LogInformation("Exact matching by message: '{0}'", messageText);
                                
                                // Use exact comparison for = operator
                                logEntries = logEntries.Where(e => e.Message.Equals(messageText, StringComparison.OrdinalIgnoreCase));
                                
                                // Logging results
                                _logger.LogInformation("Filtering results: found {0} entries", logEntries.Count());
                            }
                            else
                            {
                                _logger.LogWarning("Failed to extract message text from query: {0}", wherePart);
                            }
                        }
                    }
                    
                    // Filter by time
                    if (normalizedWherePart.Contains("TIMESTAMP", StringComparison.OrdinalIgnoreCase))
                    {
                        if (normalizedWherePart.Contains(">"))
                        {
                            var dateString = ExtractValueFromTimeCondition(normalizedWherePart, "TIMESTAMP", ">");
                            if (DateTime.TryParse(dateString, out var date))
                            {
                                logEntries = logEntries.Where(e => e.Timestamp > date);
                            }
                        }
                        
                        if (normalizedWherePart.Contains("<"))
                        {
                            var dateString = ExtractValueFromTimeCondition(normalizedWherePart, "TIMESTAMP", "<");
                            if (DateTime.TryParse(dateString, out var date))
                            {
                                logEntries = logEntries.Where(e => e.Timestamp < date);
                            }
                        }
                    }
                }

                return Task.FromResult(logEntries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing query: {Query}", query);
                return Task.FromResult(logEntries);
            }
        }

        /// <inheritdoc />
        public async Task<string> DetectLogFormatAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            // Check if file name contains "UpdateConfiguration"
            if (filePath.Contains("UpdateConfiguration"))
            {
                _logger.LogInformation("Determined log format: ConfigUpdate by file name");
                return "ConfigUpdate";
            }

            // Read first few lines to determine format
            var sample = new List<string>();
            using (var reader = new StreamReader(filePath))
            {
                for (int i = 0; i < 10 && !reader.EndOfStream; i++)
                {
                    sample.Add(await reader.ReadLineAsync() ?? string.Empty);
                }
            }

            // Check log formats
            foreach (var line in sample)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                    
                if (ConfigUpdateLogFormat.IsMatch(line))
                {
                    _logger.LogInformation("Determined log format: ConfigUpdate by content");
                    return "ConfigUpdate";
                }
                
                if (StandardLogFormat.IsMatch(line))
                {
                    _logger.LogInformation("Determined log format: Standard");
                    return "Standard";
                }
                
                if (CommonLogFormat.IsMatch(line))
                {
                    _logger.LogInformation("Determined log format: Common");
                    return "Common";
                }
                
                if (line.Contains(",") && line.Split(',').Length > 3)
                {
                    _logger.LogInformation("Determined log format: CSV");
                    return "CSV";
                }
            }

            _logger.LogInformation("Unable to determine log format, using Unknown");
            return "Unknown";
        }

        private LogEntry? ParseLogLine(string line, string format, string filePath, int lineCount)
        {
            try
            {
                // Special ConfigUpdate format
                if (format.Equals("ConfigUpdate", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseConfigUpdateFormat(line, filePath, lineCount);
                }
                
                // Check for separate word Error, not part of another word
                if (Regex.IsMatch(line, @"\bError\b|\bERROR\b|\berror\b"))
                {
                    _logger.LogDebug("Found exact word Error: '{0}'", line.Substring(0, Math.Min(50, line.Length)));
                    return new LogEntry
                    {
                        Timestamp = DateTime.Now,
                        Level = "ERROR", // Force ERROR
                        Source = ExtractSourceFromXmlStyleLog(line),
                        Message = line,
                        RawData = line,
                        FilePath = filePath,
                        LineNumber = lineCount
                    };
                }
                
                // Check for separate word Warning, not part of another word
                if (Regex.IsMatch(line, @"\bWarning\b|\bWARNING\b|\bwarning\b"))
                {
                    _logger.LogDebug("Found exact word Warning: '{0}'", line.Substring(0, Math.Min(50, line.Length)));
                    return new LogEntry
                    {
                        Timestamp = DateTime.Now,
                        Level = "WARNING", // Force WARNING
                        Source = ExtractSourceFromXmlStyleLog(line),
                        Message = line,
                        RawData = line,
                        FilePath = filePath,
                        LineNumber = lineCount
                    };
                }
                
                // Determine format and parse
                switch (format.ToLowerInvariant())
                {
                    case "standard":
                        return ParseStandardLogFormat(line, filePath, lineCount);
                    case "common":
                        return ParseCommonLogFormat(line, filePath, lineCount);
                    case "csv":
                        return ParseCsvLogFormat(line, filePath, lineCount);
                    case "simple":
                        // Simple format with tabulation or spaces
                        return ParseSimpleFormat(line, filePath, lineCount);
                    default:
                        // Try to determine format automatically
                        var entry = ParseStandardLogFormat(line, filePath, lineCount);
                        if (entry != null) return entry;
                        
                        entry = ParseCommonLogFormat(line, filePath, lineCount);
                        if (entry != null) return entry;
                        
                        entry = ParseCsvLogFormat(line, filePath, lineCount);
                        if (entry != null) return entry;
                        
                        // If none of the formats fit, try simple format
                        return ParseSimpleFormat(line, filePath, lineCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing line: {Line}", line);
                
                // In case of parsing error, create a simplified record
                return new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = "INFO",
                    Source = "Parser",
                    Message = line, // Save the entire line as message
                    FilePath = filePath,
                    LineNumber = lineCount
                };
            }
        }

        private LogEntry? ParseStandardLogFormat(string line, string filePath, int lineCount)
        {
            var match = StandardLogFormat.Match(line);
            if (!match.Success)
                return null;

            if (!DateTime.TryParseExact(match.Groups[1].Value, DateFormats, 
                    null, System.Globalization.DateTimeStyles.None, out var timestamp))
            {
                timestamp = DateTime.Now;
            }

            // Extract and normalize log level
            string level = match.Groups[2].Value;
            string message = match.Groups[4].Value;
            
            // Check for exact words Error/Warning in message
            if (Regex.IsMatch(message, @"\bError\b|\bERROR\b|\berror\b"))
            {
                level = "ERROR";
                _logger.LogDebug("Found exact word Error in message: '{0}'", message.Substring(0, Math.Min(30, message.Length)));
            }
            else if (Regex.IsMatch(message, @"\bWarning\b|\bWARNING\b|\bwarning\b"))
            {
                level = "WARNING";
                _logger.LogDebug("Found exact word Warning in message: '{0}'", message.Substring(0, Math.Min(30, message.Length)));
            }
            else
            {
                // Normalize level normally
                level = DetermineLogLevel(level, message);
            }

            return new LogEntry
            {
                Timestamp = timestamp,
                Level = level,
                Source = match.Groups[3].Value,
                Message = message,
                RawData = line,
                FilePath = filePath,
                LineNumber = lineCount
            };
        }

        private LogEntry? ParseCommonLogFormat(string line, string filePath, int lineCount)
        {
            var match = CommonLogFormat.Match(line);
            if (!match.Success)
                return null;

            if (!DateTime.TryParseExact(match.Groups[2].Value, DateFormats,
                    null, System.Globalization.DateTimeStyles.None, out var timestamp))
            {
                timestamp = DateTime.Now;
            }

            // Determine log level from status code and message
            string message = $"Status: {match.Groups[3].Value}, Size: {match.Groups[4].Value}";
            string level = DetermineLogLevel("INFO", message);

            return new LogEntry
            {
                Timestamp = timestamp,
                Level = level,
                Source = match.Groups[1].Value,
                Message = message,
                RawData = line,
                FilePath = filePath,
                LineNumber = lineCount
            };
        }

        private LogEntry? ParseCsvLogFormat(string line, string filePath, int lineCount)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            var parts = line.Split(',');
            if (parts.Length < 3)
                return null;

            if (!DateTime.TryParseExact(parts[0], DateFormats, 
                    null, System.Globalization.DateTimeStyles.None, out var timestamp))
            {
                timestamp = DateTime.Now;
            }

            string level = parts.Length > 1 ? parts[1] : "INFO";
            string source = parts.Length > 2 ? parts[2] : "";
            string message = parts.Length > 3 ? parts[3] : "";

            // Normalize log level
            level = DetermineLogLevel(level, message);

            return new LogEntry
            {
                Timestamp = timestamp,
                Level = level,
                Source = source,
                Message = message,
                RawData = line,
                FilePath = filePath,
                LineNumber = lineCount
            };
        }

        /// <summary>
        /// Determines log level based on message or set level
        /// </summary>
        private string DetermineLogLevel(string originalLevel, string message)
        {
            // Check for separate words Error or Warning in message
            // Use word boundaries \b for exact match
            if (Regex.IsMatch(message, @"\bError\b|\bERROR\b|\berror\b"))
            {
                _logger.LogDebug("Found exact word Error in message");
                return "ERROR";
            }
            
            if (Regex.IsMatch(message, @"\bWarning\b|\bWARNING\b|\bwarning\b"))
            {
                _logger.LogDebug("Found exact word Warning in message");
                return "WARNING";
            }
            
            // Strictly check message content for exact match with Error/Warning
            if (message.Equals("Error", StringComparison.Ordinal) || 
                message.Equals("ERROR", StringComparison.Ordinal) || 
                message.Equals("error", StringComparison.Ordinal))
            {
                _logger.LogDebug("Exact match of message with word Error: '{0}'", message);
                return "ERROR";
            }
            
            if (message.Equals("Warning", StringComparison.Ordinal) || 
                message.Equals("WARNING", StringComparison.Ordinal) || 
                message.Equals("warning", StringComparison.Ordinal))
            {
                _logger.LogDebug("Exact match of message with word Warning: '{0}'", message);
                return "WARNING";
            }
            
            // Normalize original level for comparison
            var normalizedLevel = originalLevel.Trim().ToUpperInvariant();
            
            // Check standard log levels
            if (normalizedLevel == "ERROR" || normalizedLevel == "ERR")
            {
                return "ERROR";
            }
            
            if (normalizedLevel == "WARNING" || normalizedLevel == "WARN")
            {
                return "WARNING";
            }
            
            // Other levels remain as they are
            return normalizedLevel == "DEBUG" || normalizedLevel == "TRACE" ? normalizedLevel : "INFO";
        }

        private string ExtractValueFromCondition(string wherePart, string fieldName)
        {
            // Make quotes optional to support queries without quotes
            var pattern = $@"{fieldName}\s*=\s*(?:['""])?([^'""]*)(?:['""])?";
            _logger.LogDebug("Applying regular expression: {0} to text: {1}", pattern, wherePart);
            
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var match = regex.Match(wherePart);
            
            var result = match.Success ? match.Groups[1].Value.Trim() : string.Empty;
            _logger.LogDebug("Extracted result for {0}: '{1}', success: {2}", fieldName, result, match.Success);
            
            return result;
        }

        private string ExtractValueFromLikeCondition(string wherePart, string fieldName)
        {
            var pattern = $@"{fieldName}\s+LIKE\s+(?:['""])?%?([^'""]*)%?(?:['""])?";
            _logger.LogDebug("Applying LIKE regular expression: {0} to text: {1}", pattern, wherePart);
            
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var match = regex.Match(wherePart);
            
            var result = match.Success ? match.Groups[1].Value.Trim() : string.Empty;
            _logger.LogDebug("LIKE extracted result for {0}: '{1}', success: {2}", fieldName, result, match.Success);
            
            return result;
        }

        private string ExtractValueFromTimeCondition(string wherePart, string fieldName, string operation)
        {
            var pattern = $@"{fieldName}\s*{Regex.Escape(operation)}\s*(?:['""])?([^'""]*)(?:['""])?";
            _logger.LogDebug("Applying TIME regular expression: {0} to text: {1}", pattern, wherePart);
            
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var match = regex.Match(wherePart);
            
            var result = match.Success ? match.Groups[1].Value.Trim() : string.Empty;
            _logger.LogDebug("Extracted TIME result for {0}: '{1}', success: {2}", fieldName, result, match.Success);
            
            return result;
        }

        /// <inheritdoc />
        public Task<IEnumerable<LogEntry>> FilterErrorsAsync(IEnumerable<LogEntry> logEntries)
        {
            _logger.LogInformation("Filtering errors in log entries");
            
            // Filter entries with ERROR level
            var filteredEntries = logEntries.Where(e => 
                e.Level.Trim().ToUpperInvariant() == "ERROR").ToList();
            
            _logger.LogInformation("Found {Count} ERROR entries", filteredEntries.Count);
            
            return Task.FromResult<IEnumerable<LogEntry>>(filteredEntries);
        }

        private LogEntry? ParseSimpleFormat(string line, string filePath, int lineCount)
        {
            try
            {
                // Try to extract timestamp from the beginning of the line
                DateTime timestamp = DateTime.Now;
                string message = line;
                string source = "System";
                string level = "INFO";
                
                // Try to parse timestamp if line starts with date
                var dateMatch = Regex.Match(line, @"^(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}(?:[.,]\d{3})?)");
                if (dateMatch.Success)
                {
                    if (DateTime.TryParse(dateMatch.Groups[1].Value, out var parsedDate))
                    {
                        timestamp = parsedDate;
                        message = line.Substring(dateMatch.Groups[1].Value.Length).Trim();
                    }
                }
                
                // Определяем уровень логирования на основе содержимого
                if (line.Contains("System.ApplicationException") || 
                    line.Contains("Exception:") || 
                    Regex.IsMatch(line, @"\bError\b|\bERROR\b|\berror\b"))
                {
                    level = "ERROR";
                    _logger.LogDebug("Found error indicator in message: {Message}", 
                        line.Substring(0, Math.Min(50, line.Length)));
                }
                else if (Regex.IsMatch(line, @"\bWarning\b|\bWARNING\b|\bwarning\b"))
                {
                    level = "WARNING";
                }
                
                return new LogEntry
                {
                    Timestamp = timestamp,
                    Level = level,
                    Source = source,
                    Message = message,
                    RawData = line,
                    FilePath = filePath,
                    LineNumber = lineCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing simple format: {Line}", 
                    line.Substring(0, Math.Min(50, line.Length)));
                return null;
            }
        }

        /// <summary>
        /// Extracts source from XML-like log
        /// </summary>
        private string ExtractSourceFromXmlStyleLog(string line)
        {
            try
            {
                // Search for text between number and word [Error] or [Warning]
                var match = Regex.Match(line, @"\d+\)\s+(\[.*?\]|\w+)\s+(?:\[Error\]|\[Warning\]|Error\]|Warning\])");
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
            catch
            {
                // Ignore extraction errors
            }
            
            return "XMLLog";
        }

        private LogEntry? ParseConfigUpdateFormat(string line, string filePath, int lineCount)
        {
            try
            {
                // Get current date for timestamp
                var today = DateTime.Today;
                var timestamp = today;
                
                // Search for time in [HH:MM:SS] format
                var timeMatch = Regex.Match(line, @"\[(\d{2}:\d{2}:\d{2})\]");
                if (timeMatch.Success)
                {
                    var timeString = timeMatch.Groups[1].Value;
                    if (TimeSpan.TryParse(timeString, out var timeSpan))
                    {
                        timestamp = today.Add(timeSpan);
                    }
                }
                
                // Extract number
                string numberStr = "";
                var numberMatch = Regex.Match(line, @"\[\d{2}:\d{2}:\d{2}\]\s+(\d+)\)");
                if (numberMatch.Success && numberMatch.Groups.Count > 1)
                {
                    numberStr = numberMatch.Groups[1].Value;
                }
                
                // Determine source
                string source = "Unknown";
                var sourceMatch = Regex.Match(line, @"\d+\)\s+(\[?[^\]]+\]?)\s+(?:\[?Error\]?|\[?Warning\]?|when)");
                if (sourceMatch.Success && sourceMatch.Groups.Count > 1)
                {
                    source = sourceMatch.Groups[1].Value.Trim();
                }
                
                // Check if string contains ONLY word Error or Warning
                // Search for exact word Error/Warning separately (not as part of another word)
                bool isError = Regex.IsMatch(line, @"\bError\b|\bERROR\b|\berror\b");
                bool isWarning = Regex.IsMatch(line, @"\bWarning\b|\bWARNING\b|\bwarning\b");
                
                _logger.LogDebug("ConfigUpdate log: IsError={IsError}, IsWarning={IsWarning}, Source={Source}", 
                    isError, isWarning, source);
                
                return new LogEntry
                {
                    Timestamp = timestamp,
                    Level = isError ? "ERROR" : (isWarning ? "WARNING" : "INFO"),
                    Source = source,
                    Message = line,
                    RawData = line,
                    FilePath = filePath,
                    LineNumber = lineCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing ConfigUpdate format: {Line}", 
                    line.Substring(0, Math.Min(line.Length, 50)));
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<PackageLogEntry>> ParsePackageLogFileAsync(string filePath)
        {
            _logger.LogInformation("Parsing package log file: {FilePath}", filePath);
            
            if (!File.Exists(filePath))
            {
                _logger.LogError("File not found: {FilePath}", filePath);
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            // Check file size
            var fileInfo = new FileInfo(filePath);
            var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);
            _logger.LogInformation("File size: {FileSizeMB:F2} MB", fileSizeMB);
            
            var packageLogEntries = new List<PackageLogEntry>();
            var currentPackage = new PackageLogEntry();
            bool isCollectingPackageInfo = false;

            // Use optimized buffered reading
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 
                                                 bufferSize: 65536, useAsync: true);
            using var reader = new StreamReader(fileStream, detectEncodingFromByteOrderMarks: true);
            
            string? line;
            int lineCount = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineCount++;
                    
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        if (isCollectingPackageInfo && !string.IsNullOrEmpty(currentPackage.PackageId))
                        {
                            packageLogEntries.Add(currentPackage);
                            currentPackage = new PackageLogEntry();
                            isCollectingPackageInfo = false;
                        }
                        continue;
                    }

                    // Package operation start pattern
                    if (line.Contains("Installing package") || line.Contains("Updating package") || 
                        line.Contains("Removing package") || line.Contains("Restoring package"))
                    {
                        if (isCollectingPackageInfo && !string.IsNullOrEmpty(currentPackage.PackageId))
                        {
                            packageLogEntries.Add(currentPackage);
                        }
                        
                        currentPackage = new PackageLogEntry
                        {
                            Timestamp = DateTime.Now,
                            RawData = line,
                            FilePath = filePath,
                            LineNumber = lineCount
                        };
                        
                        // Extract operation type
                        if (line.Contains("Installing")) currentPackage.Operation = "install";
                        else if (line.Contains("Updating")) currentPackage.Operation = "update";
                        else if (line.Contains("Removing")) currentPackage.Operation = "remove";
                        else if (line.Contains("Restoring")) currentPackage.Operation = "restore";
                        
                        // Extract package ID and version
                        var packageMatch = Regex.Match(line, @"(?:Installing|Updating|Removing|Restoring) package '([^']+)'(?: version '([^']+)')?");
                        if (packageMatch.Success)
                        {
                            currentPackage.PackageId = packageMatch.Groups[1].Value;
                            if (packageMatch.Groups.Count > 2 && !string.IsNullOrEmpty(packageMatch.Groups[2].Value))
                            {
                                currentPackage.Version = packageMatch.Groups[2].Value;
                            }
                            currentPackage.Source = "Package Manager";
                            currentPackage.Message = line;
                        }
                        
                        isCollectingPackageInfo = true;
                    }
                    // Dependencies information
                    else if (isCollectingPackageInfo && line.Contains("Dependencies:"))
                    {
                        currentPackage.Dependencies = line.Replace("Dependencies:", "").Trim();
                    }
                    // Status information
                    else if (isCollectingPackageInfo && 
                             (line.Contains("successfully") || line.Contains("failed") || line.Contains("error")))
                    {
                        currentPackage.Status = line.Trim();
                        
                        // Set log level based on status
                        if (line.Contains("successfully"))
                        {
                            currentPackage.Level = "INFO";
                        }
                        else if (line.Contains("failed") || line.Contains("error"))
                        {
                            currentPackage.Level = "ERROR";
                        }
                        else if (line.Contains("warning"))
                        {
                            currentPackage.Level = "WARNING";
                        }
                        
                        currentPackage.Message += Environment.NewLine + line;
                    }
                    // Additional information
                    else if (isCollectingPackageInfo)
                    {
                        currentPackage.Message += Environment.NewLine + line;
                    }
                    // Standalone log entry
                    else
                    {
                        var entry = ParseLogLine(line, "simple", filePath, lineCount);
                        if (entry != null)
                        {
                            // Проверяем сообщение на наличие ошибки
                            if (line.Contains("System.ApplicationException") || 
                                line.Contains("Exception:") || 
                                Regex.IsMatch(line, @"\bError\b|\bERROR\b|\berror\b"))
                            {
                                entry.Level = "ERROR";
                            }
                            
                            var packageEntry = new PackageLogEntry
                            {
                                Timestamp = entry.Timestamp,
                                Level = entry.Level,
                                Source = entry.Source,
                                Message = entry.Message,
                                RawData = entry.RawData,
                                FilePath = filePath,
                                LineNumber = lineCount
                            };
                            packageLogEntries.Add(packageEntry);
                        }
                    }
                    
                    // Progress report for large files
                    if (lineCount % 50000 == 0)
                    {
                        _logger.LogInformation("Processing package log file: parsed {Count} lines so far ({ElapsedMs} ms)", 
                                              lineCount, sw.ElapsedMilliseconds);
                    }
                }
                
                // Add the last package if we were collecting info
                if (isCollectingPackageInfo && !string.IsNullOrEmpty(currentPackage.PackageId))
                {
                    packageLogEntries.Add(currentPackage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading package log file: {FilePath}", filePath);
                throw;
            }
            finally
            {
                sw.Stop();
            }

            _logger.LogInformation("Parsed {Count} package log entries from {LineCount} lines in {ElapsedMs} ms", 
                                  packageLogEntries.Count, lineCount, sw.ElapsedMilliseconds);
            return packageLogEntries;
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services
{
    public partial class LogParserService(ILogger<LogParserService> logger) : ILogParserService
    {
        private static readonly Regex CommonLogFormat = MyRegex10();
        
        private static readonly Regex StandardLogFormat = MyRegex9();

        private static readonly Regex ConfigUpdateLogFormat = MyRegex11();

        private static readonly string[] DateFormats = {
            "yyyy-MM-dd HH:mm:ss,fff",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-dd HH:mm:ss",
            "dd/MMM/yyyy:HH:mm:ss",
            "yyyy/MM/dd HH:mm:ss"
        };
        
        public async Task<IEnumerable<LogEntry>> ParseLogFileAsync(string filePath)
        {
            logger.LogInformation("Parsing log file: {FilePath}", filePath);
            
            if (!File.Exists(filePath))
            {
                logger.LogError("File not found: {FilePath}", filePath);
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            var fileInfo = new FileInfo(filePath);
            var fileSizeMb = fileInfo.Length / (1024.0 * 1024.0);
            logger.LogInformation("File size: {FileSizeMB:F2} MB", fileSizeMb);
            
            var logFormat = await DetectLogFormatAsync(filePath);
            var logEntries = new List<LogEntry>();

            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 
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
                        else
                        {
                            // If parsing fails, create a basic log entry with the raw line
                            logEntries.Add(new LogEntry
                            {
                                Timestamp = DateTime.MinValue,
                                Level = "UNKNOWN",
                                Source = "Unknown",
                                Message = line,
                                LineNumber = lineCount,
                                FilePath = filePath
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        // If parsing completely fails, create a basic log entry with error information
                        logEntries.Add(new LogEntry
                        {
                            Timestamp = DateTime.MinValue,
                            Level = "PARSE_ERROR",
                            Source = "LogParser",
                            Message = $"Failed to parse line: {line}. Error: {ex.Message}",
                            LineNumber = lineCount,
                            FilePath = filePath
                        });
                    }
                    
                    if (lineCount % 50000 == 0)
                    {
                        logger.LogInformation("Processing log file: parsed {Count} lines so far ({ElapsedMs} ms)", 
                                              lineCount, sw.ElapsedMilliseconds);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error reading log file: {FilePath}", filePath);
                throw;
            }
            finally
            {
                sw.Stop();
            }

            logger.LogInformation("Parsed {Count} log entries from {LineCount} lines in {ElapsedMs} ms", 
                                  logEntries.Count, lineCount, sw.ElapsedMilliseconds);
            return logEntries;
        }

        public Task<IEnumerable<LogEntry>> ExecuteQueryAsync(IEnumerable<LogEntry> logEntries, string query)
        {
            logger.LogInformation("Executing query: {Query}", query);
            
            if (string.IsNullOrWhiteSpace(query))
                return Task.FromResult(logEntries);

            var result = logEntries as LogEntry[] ?? logEntries.ToArray();
            var enumerable = logEntries as LogEntry[] ?? result.ToArray();
            try
            {
                string normalizedQuery = query.ToUpperInvariant();
                
                if (normalizedQuery.Contains("SELECT"))
                {
                    if (!normalizedQuery.Contains("WHERE") && !normalizedQuery.Contains("FROM"))
                    {
                        logger.LogInformation("Query contains only SELECT, returning all logs");
                        return Task.FromResult<IEnumerable<LogEntry>>(result);
                    }
                }
                var hasWhereCondition = normalizedQuery.Contains("WHERE");
                var hasFromCondition = normalizedQuery.Contains("FROM");
                
                if (!hasWhereCondition && !hasFromCondition && query.Contains("="))
                {
                    try 
                    {
                        var parts = query.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var fieldName = parts[0].Trim().Trim('"', '\'');
                            var fieldValue = parts[1].Trim().Trim('"', '\'');
                            
                            if (fieldName.Equals("Message", StringComparison.OrdinalIgnoreCase) || 
                                fieldName.Equals("Message", StringComparison.OrdinalIgnoreCase))
                            {
                                logger.LogInformation("Simple search by message: '{0}'", fieldValue);
                                return Task.FromResult(enumerable.Where(e => e.Message.Equals(fieldValue, StringComparison.OrdinalIgnoreCase) || 
                                                                             e.Message.Contains(fieldValue, StringComparison.OrdinalIgnoreCase)));
                            }
                            else if (fieldName.Equals("Level", StringComparison.OrdinalIgnoreCase) || 
                                     fieldName.Equals("Level", StringComparison.OrdinalIgnoreCase))
                            {
                                logger.LogInformation("Simple search by level: '{0}'", fieldValue);
                                return Task.FromResult(enumerable.Where(e => e.Level.Equals(fieldValue, StringComparison.OrdinalIgnoreCase)));
                            }
                            else if (fieldName.Equals("Source", StringComparison.OrdinalIgnoreCase) || 
                                     fieldName.Equals("Source", StringComparison.OrdinalIgnoreCase))
                            {
                                logger.LogInformation("Simple search by source: '{0}'", fieldValue);
                                return Task.FromResult(enumerable.Where(e => e.Source.Equals(fieldValue, StringComparison.OrdinalIgnoreCase)));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Error in simple search");
                    }
                }

                if (!hasWhereCondition && !hasFromCondition) return Task.FromResult<IEnumerable<LogEntry>>(enumerable);
                {
                    string conditionKeyword = hasWhereCondition ? "WHERE" : "FROM";
                    var whereIndex = normalizedQuery.IndexOf(conditionKeyword, StringComparison.Ordinal) + conditionKeyword.Length;
                    var wherePart = query.Substring(whereIndex).Trim();
                    var normalizedWherePart = normalizedQuery[whereIndex..].Trim();
                    
                    if ((normalizedWherePart.Contains("LEVEL", StringComparison.OrdinalIgnoreCase) || 
                         normalizedWherePart.Contains("LEVEL", StringComparison.OrdinalIgnoreCase)) && 
                        normalizedWherePart.Contains('='))
                    {
                        var level = ExtractValueFromCondition(normalizedWherePart, "LEVEL");
                        if (!string.IsNullOrEmpty(level))
                            logEntries = enumerable.Where(e => e.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
                    }
                    
                    if ((normalizedWherePart.Contains("SOURCE", StringComparison.OrdinalIgnoreCase) || 
                         normalizedWherePart.Contains("SOURCE", StringComparison.OrdinalIgnoreCase)) && 
                        normalizedWherePart.Contains('='))
                    {
                        var source = ExtractValueFromCondition(normalizedWherePart, "SOURCE") 
                                     ?? ExtractValueFromCondition(normalizedWherePart, "SOURCE");
                        if (!string.IsNullOrEmpty(source))
                            logEntries = enumerable.Where(e => e.Source.Equals(source, StringComparison.OrdinalIgnoreCase));
                    }
                    
                    if (normalizedWherePart.Contains("MESSAGE", StringComparison.OrdinalIgnoreCase) || 
                        normalizedWherePart.Contains("MESSAGE", StringComparison.OrdinalIgnoreCase))
                    {
                        if (normalizedWherePart.Contains("LIKE"))
                        {
                            var messageText = ExtractValueFromLikeCondition(normalizedWherePart, "MESSAGE") 
                                              ?? ExtractValueFromLikeCondition(normalizedWherePart, "MESSAGE");
                            if (!string.IsNullOrEmpty(messageText))
                            {
                                var entries = enumerable.Where(e => e.Message.Contains(messageText, StringComparison.OrdinalIgnoreCase));
                            }
                        }
                        else if (normalizedWherePart.Contains('='))
                        {
                            var messageText = ExtractValueFromCondition(normalizedWherePart, "MESSAGE") 
                                              ?? ExtractValueFromCondition(normalizedWherePart, "MESSAGE");
                            if (!string.IsNullOrEmpty(messageText))
                            {
                                logger.LogInformation("Exact matching by message: '{0}'", messageText);
                                
                                logEntries = enumerable.Where(e => e.Message.Equals(messageText, StringComparison.OrdinalIgnoreCase));
                                
                                logger.LogInformation("Filtering results: found {0} entries", logEntries.Count());
                            }
                            else
                            {
                                logger.LogWarning("Failed to extract message text from query: {0}", wherePart);
                            }
                        }
                    }

                    if (!normalizedWherePart.Contains("TIMESTAMP", StringComparison.OrdinalIgnoreCase))
                        return Task.FromResult<IEnumerable<LogEntry>>(enumerable);
                    {
                        if (normalizedWherePart.Contains('>'))
                        {
                            var dateString = ExtractValueFromTimeCondition(normalizedWherePart, "TIMESTAMP", ">");
                            if (DateTime.TryParse(dateString, out var date))
                            {
                                logEntries = enumerable.Where(e => e.Timestamp > date);
                            }
                        }

                        if (!normalizedWherePart.Contains('<'))
                            return Task.FromResult<IEnumerable<LogEntry>>(enumerable);
                        {
                            var dateString = ExtractValueFromTimeCondition(normalizedWherePart, "TIMESTAMP", "<");
                            if (DateTime.TryParse(dateString, out var date))
                            {
                                logEntries = enumerable.Where(e => e.Timestamp < date);
                            }
                        }
                    }
                }

                return Task.FromResult<IEnumerable<LogEntry>>(enumerable);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error executing query: {Query}", query);
                return Task.FromResult<IEnumerable<LogEntry>>(enumerable);
            }
        }

        public async Task<string> DetectLogFormatAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            if (filePath.Contains("UpdateConfiguration"))
            {
                logger.LogInformation("Determined log format: ConfigUpdate by file name");
                return "ConfigUpdate";
            }

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
                    logger.LogInformation("Determined log format: ConfigUpdate by content");
                    return "ConfigUpdate";
                }
                
                if (StandardLogFormat.IsMatch(line))
                {
                    logger.LogInformation("Determined log format: Standard");
                    return "Standard";
                }
                
                if (CommonLogFormat.IsMatch(line))
                {
                    logger.LogInformation("Determined log format: Common");
                    return "Common";
                }
                
                if (line.Contains(",") && line.Split(',').Length > 3)
                {
                    logger.LogInformation("Determined log format: CSV");
                    return "CSV";
                }
            }

            logger.LogInformation("Unable to determine log format, using Unknown");
            return "Unknown";
        }

        private LogEntry? ParseLogLine(string line, string format, string filePath, int lineCount)
        {
            LogEntry? entry = null;
            try
            {
                // Determine format and parse
                switch (format.ToLowerInvariant())
                {
                    case "standard":
                        entry = ParseStandardLogFormat(line, filePath, lineCount);
                        break;
                    case "common":
                        entry = ParseCommonLogFormat(line, filePath, lineCount);
                        break;
                    case "csv":
                        entry = ParseCsvLogFormat(line, filePath, lineCount);
                        break;
                    case "simple":
                        entry = ParseSimpleFormat(line, filePath, lineCount);
                        break;
                    case "configupdate":
                        entry = ParseConfigUpdateFormat(line, filePath, lineCount);
                        break;
                    default:
                        entry = ParseStandardLogFormat(line, filePath, lineCount);
                        if (entry == null) entry = ParseCommonLogFormat(line, filePath, lineCount);
                        if (entry == null) entry = ParseCsvLogFormat(line, filePath, lineCount);
                        if (entry == null) entry = ParseSimpleFormat(line, filePath, lineCount);
                        break;
                }

                // --- Centralized Keyword Override --- START
                if (entry != null)
                {
                    try 
                    {
                        // Use Message if available, otherwise fall back to RawData for keyword check
                        string messageToCheck = entry.Message ?? entry.RawData ?? "";
                        
                        // First check if the message contains a result indicator at the end like "result: 'failed'"
                        var resultMatch = Regex.Match(messageToCheck, @"result:\s*'([^']+)'", RegexOptions.IgnoreCase);
                        if (resultMatch.Success && resultMatch.Groups.Count > 1)
                        {
                            string resultValue = resultMatch.Groups[1].Value.Trim();
                            logger.LogTrace("Checking result part in message (Line {LineNumber}): '{ResultValue}'", entry.LineNumber, resultValue);
                            
                            if (resultValue.Equals("failed", StringComparison.OrdinalIgnoreCase))
                            {
                                logger.LogDebug("Found 'failed' in result part for Line {LineNumber}. Setting Level to ERROR.", entry.LineNumber);
                                entry.Level = "ERROR";
                            }
                            else if (resultValue.Equals("successful", StringComparison.OrdinalIgnoreCase))
                            {
                                logger.LogDebug("Found 'successful' in result part for Line {LineNumber}. Setting Level to INFO.", entry.LineNumber);
                                entry.Level = "INFO";
                            }
                            else if (resultValue.Equals("skipped", StringComparison.OrdinalIgnoreCase))
                            {
                                logger.LogDebug("Found 'skipped' in result part for Line {LineNumber}. Setting Level to WARNING.", entry.LineNumber);
                                if (entry.Level != "ERROR") entry.Level = "WARNING";
                            }
                        }

                        // ПОТОМ проверяем основное сообщение, ЕСЛИ уровень еще не был установлен по Result
                        if (entry.Level != "ERROR" && entry.Level != "WARNING" && entry.Level != "INFO") // Проверяем, если уровень еще не установлен из Result
                        {
                            if (!string.IsNullOrEmpty(messageToCheck))
                            {
                            logger.LogTrace("Checking Message part for keywords (Line {LineNumber}): '{MessagePart}'", entry.LineNumber, messageToCheck);
                            if (messageToCheck.Contains("failed", StringComparison.OrdinalIgnoreCase) || messageToCheck.Contains("error", StringComparison.OrdinalIgnoreCase))
                            {
                                logger.LogDebug("Found 'failed' or 'error' in Message part for Line {LineNumber}. Setting Level to ERROR.", entry.LineNumber);
                                entry.Level = "ERROR";
                            }
                            else if (messageToCheck.Contains("successful", StringComparison.OrdinalIgnoreCase))
                            {
                                logger.LogDebug("Found 'successful' in Message part for Line {LineNumber}. Setting Level to INFO.", entry.LineNumber);
                                // Не меняем уровень, если он уже был, например, ERROR или WARNING
                                if (entry.Level != "ERROR" && entry.Level != "WARNING")
                                {
                                    entry.Level = "INFO";
                                }
                            }
                            else if (messageToCheck.Contains("warning", StringComparison.OrdinalIgnoreCase) || messageToCheck.Contains("skipped", StringComparison.OrdinalIgnoreCase))
                            {
                                logger.LogDebug("Found 'warning' or 'skipped' in Message part for Line {LineNumber}. Setting Level to WARNING.", entry.LineNumber);
                                // Не меняем уровень, если он уже Error
                                if (entry.Level != "ERROR")
                                {
                                    entry.Level = "WARNING";
                                }
                            }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error in keyword processing for line {LineNumber}", entry.LineNumber);
                     }
                }
                // --- Centralized Keyword Override --- END

                return entry; // Return the final entry (possibly null if parsing failed)
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error parsing line: {Line}", line);
                
                return new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = "INFO",
                    Source = "Parser",
                    Message = line, 
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

            string level = match.Groups[2].Value;
            string message = match.Groups[4].Value;
            
            if (MyRegex().IsMatch(message))
            {
                level = "ERROR";
                logger.LogDebug("Found exact word Error in message: '{0}'", message.Substring(0, Math.Min(30, message.Length)));
            }
            else if (Regex.IsMatch(message, @"\bWarning\b|\bWARNING\b|\bwarning\b"))
            {
                level = "WARNING";
                logger.LogDebug("Found exact word Warning in message: '{0}'", message.Substring(0, Math.Min(30, message.Length)));
            }
            else
            {
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
                logger.LogDebug("Found exact word Error in message");
                return "ERROR";
            }
            
            if (Regex.IsMatch(message, @"\bWarning\b|\bWARNING\b|\bwarning\b"))
            {
                logger.LogDebug("Found exact word Warning in message");
                return "WARNING";
            }
            
            // Strictly check message content for exact match with Error/Warning
            if (message.Equals("Error", StringComparison.Ordinal) || 
                message.Equals("ERROR", StringComparison.Ordinal) || 
                message.Equals("error", StringComparison.Ordinal))
            {
                logger.LogDebug("Exact match of message with word Error: '{0}'", message);
                return "ERROR";
            }
            
            if (message.Equals("Warning", StringComparison.Ordinal) || 
                message.Equals("WARNING", StringComparison.Ordinal) || 
                message.Equals("warning", StringComparison.Ordinal))
            {
                logger.LogDebug("Exact match of message with word Warning: '{0}'", message);
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
            logger.LogDebug("Applying regular expression: {0} to text: {1}", pattern, wherePart);
            
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var match = regex.Match(wherePart);
            
            var result = match.Success ? match.Groups[1].Value.Trim() : string.Empty;
            logger.LogDebug("Extracted result for {0}: '{1}', success: {2}", fieldName, result, match.Success);
            
            return result;
        }

        private string ExtractValueFromLikeCondition(string wherePart, string fieldName)
        {
            var pattern = $@"{fieldName}\s+LIKE\s+(?:['""])?%?([^'""]*)%?(?:['""])?";
            logger.LogDebug("Applying LIKE regular expression: {0} to text: {1}", pattern, wherePart);
            
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var match = regex.Match(wherePart);
            
            var result = match.Success ? match.Groups[1].Value.Trim() : string.Empty;
            logger.LogDebug("LIKE extracted result for {0}: '{1}', success: {2}", fieldName, result, match.Success);
            
            return result;
        }

        private string ExtractValueFromTimeCondition(string wherePart, string fieldName, string operation)
        {
            var pattern = $@"{fieldName}\s*{Regex.Escape(operation)}\s*(?:['""])?([^'""]*)(?:['""])?";
            logger.LogDebug("Applying TIME regular expression: {0} to text: {1}", pattern, wherePart);
            
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var match = regex.Match(wherePart);
            
            var result = match.Success ? match.Groups[1].Value.Trim() : string.Empty;
            logger.LogDebug("Extracted TIME result for {0}: '{1}', success: {2}", fieldName, result, match.Success);
            
            return result;
        }

        /// <inheritdoc />
        public Task<IEnumerable<LogEntry>> FilterErrorsAsync(IEnumerable<LogEntry> logEntries)
        {
            logger.LogInformation("Filtering errors in log entries");
            
            // Filter entries with ERROR level
            var filteredEntries = logEntries.Where(e => 
                e.Level.Trim().ToUpperInvariant() == "ERROR").ToList();
            
            logger.LogInformation("Found {Count} ERROR entries", filteredEntries.Count);
            
            return Task.FromResult<IEnumerable<LogEntry>>(filteredEntries);
        }

        private LogEntry? ParseSimpleFormat(string line, string filePath, int lineCount)
        {
            try
            {
                DateTime timestamp = DateTime.Now;
                var message = line;
                const string source = "System";
                var level = "INFO";
                
                var dateMatch = MyRegex8().Match(line);
                if (dateMatch.Success)
                {
                    if (DateTime.TryParse(dateMatch.Groups[1].Value, out var parsedDate))
                    {
                        timestamp = parsedDate;
                        message = line[dateMatch.Groups[1].Value.Length..].Trim();
                    }
                }
                
                if (line.Contains("System.ApplicationException") || 
                    line.Contains("Exception:") || 
                    Regex.IsMatch(line, @"\bError\b|\bERROR\b|\berror\b"))
                {
                    level = "ERROR";
                    logger.LogDebug("Found error indicator in message: {Message}", 
                        line[..Math.Min(50, line.Length)]);
                }
                else if (MyRegex7().IsMatch(line))
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
                logger.LogWarning(ex, "Error parsing simple format: {Line}", 
                    line.Substring(0, Math.Min(50, line.Length)));
                return null;
            }
        }
        
        private static string ExtractSourceFromXmlStyleLog(string line)
        {
            try
            {
                var match = MyRegex6().Match(line);
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
            catch
            {
                // ignored
            }

            return "XMLLog";
        }

        private LogEntry? ParseConfigUpdateFormat(string line, string filePath, int lineCount)
        {
            try
            {
                var today = DateTime.Today;
                var timestamp = today;
                
                var timeMatch = MyRegex5().Match(line);
                if (timeMatch.Success)
                {
                    var timeString = timeMatch.Groups[1].Value;
                    if (TimeSpan.TryParse(timeString, out var timeSpan))
                    {
                        timestamp = today.Add(timeSpan);
                    }
                }
                
                var numberMatch = MyRegex4().Match(line);
                if (numberMatch.Success && numberMatch.Groups.Count > 1)
                {
                }
                
                string source = "Unknown";
                var sourceMatch = MyRegex3().Match(line);
                if (sourceMatch is { Success: true, Groups.Count: > 1 })
                {
                    source = sourceMatch.Groups[1].Value.Trim();
                }
                
                bool isError = MyRegex1().IsMatch(line);
                bool isWarning = MyRegex2().IsMatch(line);
                
                string level = isError ? "ERROR" : (isWarning ? "WARNING" : "INFO");
                string message = line; // Используем всю строку как сообщение для поиска ключей
                
                logger.LogDebug("ConfigUpdate log: IsError={IsError}, IsWarning={IsWarning}, Source={Source}", 
                    isError, isWarning, source);
                
                return new LogEntry
                {
                    Timestamp = timestamp,
                    Level = level,
                    Source = source,
                    Message = line,
                    RawData = line,
                    FilePath = filePath,
                    LineNumber = lineCount
                };
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error parsing ConfigUpdate format: {Line}", 
                    line.Substring(0, Math.Min(line.Length, 50)));
                return null;
            }
        }

        public async Task<IEnumerable<PackageLogEntry>> ParsePackageLogFileAsync(string filePath)
        {
            logger.LogInformation("Parsing package log file: {FilePath}", filePath);
            
            if (!File.Exists(filePath))
            {
                logger.LogError("File not found: {FilePath}", filePath);
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            var fileInfo = new FileInfo(filePath);
            var fileSizeMb = fileInfo.Length / (1024.0 * 1024.0);
            logger.LogInformation("File size: {FileSizeMB:F2} MB", fileSizeMb);
            
            var packageLogEntries = new List<PackageLogEntry>();
            var currentPackage = new PackageLogEntry();
            bool isCollectingPackageInfo = false;

            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 
                                                 bufferSize: 65536, useAsync: true);
            using var reader = new StreamReader(fileStream, detectEncodingFromByteOrderMarks: true);

            var lineCount = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                while (await reader.ReadLineAsync() is { } line)
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
                        
                        if (line.Contains("Installing")) currentPackage.Operation = "install";
                        else if (line.Contains("Updating")) currentPackage.Operation = "update";
                        else if (line.Contains("Removing")) currentPackage.Operation = "remove";
                        else if (line.Contains("Restoring")) currentPackage.Operation = "restore";
                        
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
                    else switch (isCollectingPackageInfo)
                    {
                        case true when line.Contains("Dependencies:"):
                            currentPackage.Dependencies = line.Replace("Dependencies:", "").Trim();
                            break;
                        case true when 
                            (line.Contains("successfully") || line.Contains("failed") || line.Contains("error")):
                        {
                            currentPackage.Status = line.Trim();
                        
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
                            break;
                        }
                        case true:
                            currentPackage.Message += Environment.NewLine + line;
                            break;
                        default:
                        {
                            var entry = ParseLogLine(line, "simple", filePath, lineCount);
                            if (entry != null)
                            {
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

                            break;
                        }
                    }
                    
                    if (lineCount % 50000 == 0)
                    {
                        logger.LogInformation("Processing package log file: parsed {Count} lines so far ({ElapsedMs} ms)", 
                                              lineCount, sw.ElapsedMilliseconds);
                    }
                }
                
                if (isCollectingPackageInfo && !string.IsNullOrEmpty(currentPackage.PackageId))
                {
                    packageLogEntries.Add(currentPackage);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error reading package log file: {FilePath}", filePath);
                throw;
            }
            finally
            {
                sw.Stop();
            }

            logger.LogInformation("Parsed {Count} package log entries from {LineCount} lines in {ElapsedMs} ms", 
                                  packageLogEntries.Count, lineCount, sw.ElapsedMilliseconds);
            return packageLogEntries;
        }

        [GeneratedRegex(@"\bError\b|\bERROR\b|\berror\b")]
        private static partial Regex MyRegex();
        [GeneratedRegex(@"\bError\b|\bERROR\b|\berror\b")]
        private static partial Regex MyRegex1();
        [GeneratedRegex(@"\bWarning\b|\bWARNING\b|\bwarning\b")]
        private static partial Regex MyRegex2();
        [GeneratedRegex(@"\d+\)\s+(\[?[^\]]+\]?)\s+(?:\[?Error\]?|\[?Warning\]?|when)")]
        private static partial Regex MyRegex3();
        [GeneratedRegex(@"\[\d{2}:\d{2}:\d{2}\]\s+(\d+)\)")]
        private static partial Regex MyRegex4();
        [GeneratedRegex(@"\[(\d{2}:\d{2}:\d{2})\]")]
        private static partial Regex MyRegex5();
        [GeneratedRegex(@"\d+\)\s+(\[.*?\]|\w+)\s+(?:\[Error\]|\[Warning\]|Error\]|Warning\])")]
        private static partial Regex MyRegex6();
        [GeneratedRegex(@"\bWarning\b|\bWARNING\b|\bwarning\b")]
        private static partial Regex MyRegex7();
        [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}(?:[.,]\d{3})?)")]
        private static partial Regex MyRegex8();
        [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}(?:\.\d+)?)\s+(\w+)\s+\[([^\]]+)\]\s+(.*)", RegexOptions.Compiled)]
        private static partial Regex MyRegex9();
        [GeneratedRegex("""^(\S+) \S+ \S+ \[([^:]+)[^\]]+\] "[^"]*" (\d+) (\d+)""", RegexOptions.Compiled)]
        private static partial Regex MyRegex10();
        [GeneratedRegex(@"\[(\d{2}:\d{2}:\d{2})\]\s+(\d+)\)\s+(?:\[?([^\]]+)\]?)\s+(?:when|Error|Warning)(.*)", RegexOptions.Compiled)]
        private static partial Regex MyRegex11();
    }
}
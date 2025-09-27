using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services
{
    public class Log4NetParserService : ILog4NetParserService
    {
        private readonly ILogger<Log4NetParserService> _logger;

        public Log4NetParserService(ILogger<Log4NetParserService> logger)
        {
            _logger = logger;
        }

        public LogFormatType GetLogFormatType() => LogFormatType.Log4Net;

        public async Task<bool> ValidateLog4NetFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                // Read only first 50 lines for validation to avoid loading huge files
                var log4netPattern = @"^\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2},\d{3}";
                var validLines = 0;
                var totalLines = 0;
                const int maxLinesToCheck = 50;

                using var reader = new StreamReader(filePath);
                string? line;
                
                while ((line = await reader.ReadLineAsync()) != null && totalLines < maxLinesToCheck)
                {
                    totalLines++;
                    
                    if (!string.IsNullOrWhiteSpace(line) && Regex.IsMatch(line, log4netPattern))
                    {
                        validLines++;
                    }
                }

                // Consider valid if at least 10% of checked lines match the pattern
                return totalLines > 0 && (double)validLines / totalLines >= 0.1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating Log4Net file: {filePath}");
                return false;
            }
        }

        public async Task<List<Log4NetLogEntry>> ParseLog4NetFileAsync(string filePath)
        {
            var entries = new List<Log4NetLogEntry>();

            try
            {
                _logger.LogInformation($"Parsing Log4Net file: {filePath}");

                if (!await ValidateLog4NetFileAsync(filePath))
                {
                    throw new InvalidOperationException("Invalid Log4Net file format");
                }

                var currentEntry = new Log4NetLogEntry();
                var messageLines = new List<string>();
                var lineCount = 0;

                using var reader = new StreamReader(filePath);
                string? line;
                
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineCount++;
                    
                    // Yield control periodically to prevent UI freezing
                    if (lineCount % 1000 == 0)
                    {
                        await Task.Yield();
                    }

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // Check if this is a new log entry (starts with timestamp)
                    if (IsLogEntryStart(line))
                    {
                        // Save previous entry if it exists
                        if (!string.IsNullOrEmpty(currentEntry.Message) || messageLines.Any())
                        {
                            currentEntry.Message = string.Join(Environment.NewLine, messageLines);
                            entries.Add(currentEntry);
                        }

                        // Start new entry
                        currentEntry = ParseLogEntryHeader(line);
                        messageLines.Clear();
                    }
                    else
                    {
                        // This is a continuation of the message or exception
                        messageLines.Add(line);
                    }
                }

                // Add the last entry
                if (!string.IsNullOrEmpty(currentEntry.Message) || messageLines.Any())
                {
                    currentEntry.Message = string.Join(Environment.NewLine, messageLines);
                    entries.Add(currentEntry);
                }

                _logger.LogInformation($"Successfully parsed {entries.Count} Log4Net entries from {lineCount} lines");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to parse Log4Net file: {filePath}");
                throw;
            }

            return entries;
        }

        private bool IsLogEntryStart(string line)
        {
            // Log4Net entries typically start with a timestamp like: 2024-01-01 12:00:00,123
            return Regex.IsMatch(line, @"^\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2},\d{3}");
        }

        private Log4NetLogEntry ParseLogEntryHeader(string line)
        {
            // Parse the main log entry line
            // Format: timestamp [thread] level logger - message
            var pattern = @"^(?<date>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2},\d{3})\s+\[(?<thread>[^\]]+)\]\s+(?<level>\w+)\s+(?<logger>[^\s-]+)\s+-\s+(?<message>.+)?$";
            var match = Regex.Match(line, pattern);

            if (!match.Success)
            {
                _logger.LogWarning($"Could not parse log entry header: {line}");
                return new Log4NetLogEntry
                {
                    Date = DateTime.Now,
                    Message = line
                };
            }

            return new Log4NetLogEntry
            {
                Date = DateTime.ParseExact(match.Groups["date"].Value, "yyyy-MM-dd HH:mm:ss,fff", null),
                Thread = match.Groups["thread"].Value,
                Level = match.Groups["level"].Value,
                Logger = match.Groups["logger"].Value,
                Message = match.Groups["message"].Value ?? "",
                Host = Environment.MachineName,
                Site = "Default",
                User = Environment.UserName
            };
        }
    }
}
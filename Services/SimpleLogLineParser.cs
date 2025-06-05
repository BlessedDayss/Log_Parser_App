using System;
using Log_Parser_App.Models;
using Log_Parser_App.Models.Interfaces;

namespace Log_Parser_App.Services
{
    public class SimpleLogLineParser : ILogLineParser
    {
        public bool IsLogLine(string line)
        {
            return DateTime.TryParse(line.Substring(0, Math.Min(19, line.Length)), out _);
        }
        public LogEntry? Parse(string line, int lineNumber, string filePath)
        {
            if (!IsLogLine(line)) return null;

            DateTime timestamp;
            string message;

            if (line.Length < 19)
            {
                // IsLogLine was true, so the entire short line is a parsable date.
                if (!DateTime.TryParse(line, out timestamp))
                {
                    // Fallback, though IsLogLine implies it's parsable. Consider if this fallback is desired.
                    timestamp = DateTime.Now; 
                }
                message = string.Empty;
            }
            else // line.Length >= 19
            {
                var timePartStr = line.Substring(0, 19); // Safe, as line.Length >= 19
                if (!DateTime.TryParse(timePartStr, out timestamp))
                {
                    timestamp = DateTime.Now; // Original fallback
                }
                message = line.Substring(19).Trim(); // Message is everything after the first 19 chars
            }
            
            var level = "INFO";
            if (message.Contains("error", StringComparison.OrdinalIgnoreCase))
                level = "ERROR";
            else if (message.Contains("warning", StringComparison.OrdinalIgnoreCase))
                level = "WARNING";
            return new LogEntry
            {
                Timestamp = timestamp,
                Level = level,
                Message = message,
                FilePath = filePath,
                LineNumber = lineNumber
            };
        }
    }
} 
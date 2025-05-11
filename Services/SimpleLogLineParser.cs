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
            var timePart = line.Substring(0, 19);
            if (!DateTime.TryParse(timePart, out var timestamp)) timestamp = DateTime.Now;
            var message = line.Length > 19 ? line.Substring(19).Trim() : string.Empty;
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
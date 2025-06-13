namespace Log_Parser_App.Services
{
using System;
using System.Text.RegularExpressions;
using Log_Parser_App.Models;
using Log_Parser_App.Models.Interfaces;


    public class LogLineParser : ILogLineParser
    {
        private static readonly Regex TimeRegex = new(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}[,.]\d{3}");
        private static readonly Regex StandardLogFormat = new(@"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}[,.]\d{3})\s+(.*)", RegexOptions.Compiled);

        public bool IsLogLine(string line)
        {
            return TimeRegex.IsMatch(line);
        }

        public LogEntry? Parse(string line, int lineNumber, string filePath)
        {
            if (!IsLogLine(line))
                return null;
            var match = StandardLogFormat.Match(line);
            if (!match.Success)
                return null;
            DateTime timestamp;
            if (!DateTime.TryParse(match.Groups[1].Value.Replace(',', '.'), out timestamp))
                timestamp = DateTime.Now;
            var rest = match.Groups[2].Value;
            var level = "INFO";
            if (rest.Contains("error", StringComparison.OrdinalIgnoreCase))
                level = "ERROR";
            else if (rest.Contains("warning", StringComparison.OrdinalIgnoreCase))
                level = "WARNING";
            var entry = new LogEntry
            {
                Timestamp = timestamp,
                Level = level,
                Message = rest,
                FilePath = filePath,
                LineNumber = lineNumber
            };
            return entry;
        }
    }
} 
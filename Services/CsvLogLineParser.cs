namespace Log_Parser_App.Services
{
using System;
using Log_Parser_App.Models;
using Log_Parser_App.Models.Interfaces;


    public class CsvLogLineParser : ILogLineParser
    {
        public bool IsLogLine(string line) {
            string[] parts = line.Split(',');
            return parts.Length >= 4 && DateTime.TryParse(parts[0], out _);
        }

        public LogEntry? Parse(string line, int lineNumber, string filePath) {
            var parts = line.Split(',');
            if (parts.Length < 4)
                return null;

            if (!DateTime.TryParse(parts[0], out var timestamp))
                timestamp = DateTime.Now;
            string level = parts[1].Trim();
            string source = parts[2].Trim();
            string message = string.Join(",", parts, 3, parts.Length - 3).Trim();
            return new LogEntry {
                Timestamp = timestamp,
                Level = level,
                Source = source,
                Message = message,
                FilePath = filePath,
                LineNumber = lineNumber
            };
        }
    }
}
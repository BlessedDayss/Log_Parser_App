namespace Log_Parser_App.Services
{
using System;
using System.Text.RegularExpressions;
using Log_Parser_App.Models;
using Log_Parser_App.Interfaces;


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
            // Check for error/warning keywords but exclude "0 Error" and "0 Warning" false positives
            if (rest.Contains("error", StringComparison.OrdinalIgnoreCase) && !IsZeroErrorOrWarningFalsePositive(rest))
                level = "ERROR";
            else if (rest.Contains("warning", StringComparison.OrdinalIgnoreCase) && !IsZeroErrorOrWarningFalsePositive(rest))
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

        /// <summary>
        /// Checks if a line contains "0 Error" or "0 Warning" pattern which should not be treated as error/warning
        /// </summary>
        /// <param name="line">Log line to check</param>
        /// <returns>True if line contains "0 error", "0 errors", "0 warning", or "0 warnings" pattern</returns>
        private static bool IsZeroErrorOrWarningFalsePositive(string line) {
            if (string.IsNullOrEmpty(line))
                return false;
                
            var lowerLine = line.ToLowerInvariant();
            return lowerLine.Contains("0 error") || lowerLine.Contains("0 errors") ||
                   lowerLine.Contains("0 warning") || lowerLine.Contains("0 warnings");
        }
    }
} 

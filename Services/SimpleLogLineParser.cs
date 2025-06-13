namespace Log_Parser_App.Services
{
    using System;
    using System.Text.RegularExpressions;
    using Log_Parser_App.Models;
    using Log_Parser_App.Interfaces;


    public class SimpleLogLineParser : ILogLineParser
    {
        private static readonly Regex LogStartRegex = new Regex(@"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}|\[\d{2}:\d{2}:\d{2}\])", RegexOptions.Compiled);
        private static readonly Regex LevelRegex = new Regex(@"\b(INFO|ERROR|WARNING|DEBUG|TRACE|CRITICAL|VERBOSE)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SourceRegex1 = new Regex(@"\[([^]]+)\]", RegexOptions.Compiled);
        private static readonly Regex SourceRegex2 = new Regex(@"\(([^)]+)\)", RegexOptions.Compiled);

        public bool IsLogLine(string line) {
            return !string.IsNullOrEmpty(line) && LogStartRegex.IsMatch(line);
        }

        public LogEntry? Parse(string line, int lineNumber, string filePath) {
            if (string.IsNullOrEmpty(line))
                return null;

            if (!IsLogLine(line))
                return null;

            DateTime timestamp;
            string message;
            string level = "INFO"; 
            string source = "";

            if (line.StartsWith("[") && line.Length >= 10 && line.IndexOf(']') >= 0) {
                int timeEnd = line.IndexOf(']');
                string timePartStr = line.Substring(1, timeEnd - 1);

                if (DateTime.TryParse($"{DateTime.Today:yyyy-MM-dd} {timePartStr}", out timestamp)) {
                } else {
                    timestamp = DateTime.Now;
                }

                message = line.Substring(timeEnd + 1).Trim();
            } else if (line.Length < 19) {
                if (!DateTime.TryParse(line, out timestamp)) {
                    timestamp = DateTime.Now;
                }
                message = string.Empty;
            } else 
            {
                string timePartStr = line.Substring(0, 19);
                if (!DateTime.TryParse(timePartStr, out timestamp)) {
                    timestamp = DateTime.Now;
                }
                message = line.Substring(19).Trim();
            }

            var levelMatch = LevelRegex.Match(message);
            if (levelMatch.Success) {
                level = levelMatch.Value.ToUpper();
                if (message.StartsWith(levelMatch.Value + " ") || message.StartsWith("[" + levelMatch.Value + "]") || message.StartsWith("(" + levelMatch.Value + ")")) {
                    message = message.Substring(levelMatch.Index + levelMatch.Length).Trim();
                    if (message.StartsWith(":"))
                        message = message.Substring(1).Trim();
                }
            } else {
                var errorRegex = new Regex(@"\berror\b", RegexOptions.IgnoreCase);
                var warnRegex = new Regex(@"\bwarn\b", RegexOptions.IgnoreCase);
                var debugRegex = new Regex(@"\bdebug\b", RegexOptions.IgnoreCase);
                var traceRegex = new Regex(@"\btrace\b", RegexOptions.IgnoreCase);

                if (errorRegex.IsMatch(message))
                    level = "ERROR";
                else if (warnRegex.IsMatch(message))
                    level = "WARNING";
                else if (debugRegex.IsMatch(message))
                    level = "DEBUG";
                else if (traceRegex.IsMatch(message))
                    level = "TRACE";
            }

            var sourceMatch1 = SourceRegex1.Match(message);
            if (sourceMatch1.Success) {
                source = sourceMatch1.Groups[1].Value;
                if (!LevelRegex.IsMatch(source)) {
                    message = message.Replace(sourceMatch1.Value, "").Trim();
                }
            } else {
                var sourceMatch2 = SourceRegex2.Match(message);
                if (sourceMatch2.Success) {
                    source = sourceMatch2.Groups[1].Value;
                    if (!LevelRegex.IsMatch(source)) {
                        message = message.Replace(sourceMatch2.Value, "").Trim();
                    }
                }
            }

            message = Regex.Replace(message, @"\s+", " ");

            return new LogEntry {
                Timestamp = timestamp,
                Level = level,
                Message = message,
                Source = source,
                FilePath = filePath,
                LineNumber = lineNumber
            };
        }
    }
}

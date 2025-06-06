using System;
using System.Text.RegularExpressions;
using Log_Parser_App.Models;
using Log_Parser_App.Models.Interfaces;

namespace Log_Parser_App.Services
{
    public class SimpleLogLineParser : ILogLineParser
    {
        // Регулярное выражение для определения, является ли строка началом лог-записи
        private static readonly Regex LogStartRegex = new Regex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}", RegexOptions.Compiled);
        
        // Регулярное выражение для поиска уровня логирования
        private static readonly Regex LevelRegex = new Regex(@"\b(INFO|ERROR|WARNING|DEBUG|TRACE|CRITICAL|VERBOSE)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        // Регулярные выражения для поиска источника сообщения в разных форматах
        private static readonly Regex SourceRegex1 = new Regex(@"\[([^]]+)\]", RegexOptions.Compiled);
        private static readonly Regex SourceRegex2 = new Regex(@"\(([^)]+)\)", RegexOptions.Compiled);

        public bool IsLogLine(string line)
        {
            // Проверяем начинается ли строка с даты/времени
            return !string.IsNullOrEmpty(line) && LogStartRegex.IsMatch(line);
        }
        
        public LogEntry? Parse(string line, int lineNumber, string filePath)
        {
            if (string.IsNullOrEmpty(line)) return null;

            if (!IsLogLine(line)) return null;

            // Извлекаем timestamp
            DateTime timestamp;
            string message;
            string level = "INFO"; // По умолчанию
            string source = "";

            if (line.Length < 19)
            {
                if (!DateTime.TryParse(line, out timestamp))
                {
                    timestamp = DateTime.Now; 
                }
                message = string.Empty;
            }
            else // line.Length >= 19
            {
                var timePartStr = line.Substring(0, 19);
                if (!DateTime.TryParse(timePartStr, out timestamp))
                {
                    timestamp = DateTime.Now;
                }
                message = line.Substring(19).Trim();
            }
            
            // Определяем уровень логирования
            var levelMatch = LevelRegex.Match(message);
            if (levelMatch.Success)
            {
                level = levelMatch.Value.ToUpper();
                // Убираем уровень логирования из сообщения, если он в начале или окружен скобками
                if (message.StartsWith(levelMatch.Value + " ") || 
                    message.StartsWith("[" + levelMatch.Value + "]") || 
                    message.StartsWith("(" + levelMatch.Value + ")"))
                {
                    message = message.Substring(levelMatch.Index + levelMatch.Length).Trim();
                    if (message.StartsWith(":"))
                        message = message.Substring(1).Trim();
                }
            }
            else if (message.Contains("error", StringComparison.OrdinalIgnoreCase))
                level = "ERROR";
            else if (message.Contains("warn", StringComparison.OrdinalIgnoreCase))
                level = "WARNING";
            else if (message.Contains("debug", StringComparison.OrdinalIgnoreCase))
                level = "DEBUG";
            else if (message.Contains("trace", StringComparison.OrdinalIgnoreCase))
                level = "TRACE";
            
            // Пытаемся определить источник сообщения
            var sourceMatch1 = SourceRegex1.Match(message);
            if (sourceMatch1.Success)
            {
                source = sourceMatch1.Groups[1].Value;
                // Если источник не похож на уровень логирования, используем его
                if (!LevelRegex.IsMatch(source))
                {
                    // Убираем источник из сообщения
                    message = message.Replace(sourceMatch1.Value, "").Trim();
                }
            }
            else
            {
                var sourceMatch2 = SourceRegex2.Match(message);
                if (sourceMatch2.Success)
                {
                    source = sourceMatch2.Groups[1].Value;
                    if (!LevelRegex.IsMatch(source))
                    {
                        // Убираем источник из сообщения
                        message = message.Replace(sourceMatch2.Value, "").Trim();
                    }
                }
            }
            
            // Очищаем сообщение от двойных пробелов
            message = Regex.Replace(message, @"\s+", " ");
            
            return new LogEntry
            {
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
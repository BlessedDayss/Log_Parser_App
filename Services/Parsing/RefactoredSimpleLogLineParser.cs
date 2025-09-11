using System;
using System.Text.RegularExpressions;
using Log_Parser_App.Services.LevelDetection;

namespace Log_Parser_App.Services.Parsing
{
    /// <summary>
    /// Refactored SimpleLogLineParser using SOLID principles
    /// SOLID: Single Responsibility - only handles flexible timestamp format parsing
    /// Level detection is delegated to LevelDetectionService
    /// </summary>
    public class RefactoredSimpleLogLineParser : AbstractLogLineParser
    {
        private static readonly Regex LogStartRegex = new(@"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}|\[\d{2}:\d{2}:\d{2}\])", RegexOptions.Compiled);
        private static readonly Regex SourceRegex1 = new(@"\[([^]]+)\]", RegexOptions.Compiled);
        private static readonly Regex SourceRegex2 = new(@"\(([^)]+)\)", RegexOptions.Compiled);
        
        public RefactoredSimpleLogLineParser(LevelDetectionService levelDetectionService) 
            : base(levelDetectionService)
        {
        }
        
        public override bool IsLogLine(string line) => 
            !string.IsNullOrEmpty(line) && LogStartRegex.IsMatch(line);
        
        protected override ParsedLogData? ParseFormatSpecificData(string line)
        {
            if (string.IsNullOrEmpty(line))
                return null;
            
            DateTime timestamp;
            string message;
            string source = "";
            
            // Parse timestamp - handle both formats: "2025-06-17 09:20:32" and "[09:20:32]"
            if (line.StartsWith("[") && line.Length >= 10 && line.IndexOf(']') >= 0)
            {
                // Format: [09:20:32] message
                int timeEnd = line.IndexOf(']');
                string timePartStr = line.Substring(1, timeEnd - 1);

                if (DateTime.TryParse($"{DateTime.Today:yyyy-MM-dd} {timePartStr}", out timestamp))
                {
                    // Success - timestamp parsed
                }
                else
                {
                    timestamp = DateTime.Now;
                }

                message = line.Substring(timeEnd + 1).Trim();
            }
            else if (line.Length < 19)
            {
                // Too short to have full timestamp
                if (!DateTime.TryParse(line, out timestamp))
                {
                    timestamp = DateTime.Now;
                }
                message = string.Empty;
            }
            else
            {
                // Format: 2025-06-17 09:20:32 message
                string timePartStr = line.Substring(0, 19);
                if (!DateTime.TryParse(timePartStr, out timestamp))
                {
                    timestamp = DateTime.Now;
                }
                message = line.Substring(19).Trim();
            }
            
            // Extract source from message (patterns like [source] or (source))
            var sourceMatch1 = SourceRegex1.Match(message);
            if (sourceMatch1.Success)
            {
                source = sourceMatch1.Groups[1].Value;
                // Remove source from message if it's not a level indicator
                if (!IsLevelKeyword(source))
                {
                    message = message.Replace(sourceMatch1.Value, "").Trim();
                }
            }
            else
            {
                var sourceMatch2 = SourceRegex2.Match(message);
                if (sourceMatch2.Success)
                {
                    source = sourceMatch2.Groups[1].Value;
                    // Remove source from message if it's not a level indicator
                    if (!IsLevelKeyword(source))
                    {
                        message = message.Replace(sourceMatch2.Value, "").Trim();
                    }
                }
            }
            
            // Clean up multiple spaces
            message = Regex.Replace(message, @"\s+", " ");
            
            return new ParsedLogData
            {
                Timestamp = timestamp,
                Message = message,
                Source = source
            };
        }
        
        /// <summary>
        /// Checks if the extracted text is a log level keyword
        /// </summary>
        private static bool IsLevelKeyword(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
                
            var upperText = text.ToUpperInvariant();
            return upperText == "INFO" || upperText == "ERROR" || upperText == "WARNING" || 
                   upperText == "DEBUG" || upperText == "TRACE" || upperText == "CRITICAL" || 
                   upperText == "VERBOSE";
        }
    }
} 
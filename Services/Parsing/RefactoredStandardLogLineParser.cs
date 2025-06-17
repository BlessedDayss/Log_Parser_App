using System;
using System.Text.RegularExpressions;
using Log_Parser_App.Services.LevelDetection;

namespace Log_Parser_App.Services.Parsing
{
    /// <summary>
    /// Refactored StandardLogLineParser using SOLID principles
    /// SOLID: Single Responsibility - only handles timestamp format parsing
    /// Level detection is delegated to LevelDetectionService
    /// </summary>
    public class RefactoredStandardLogLineParser : AbstractLogLineParser
    {
        private static readonly Regex StandardLogRegex = new(@"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}[,.]\d{3})\s+(.*)", RegexOptions.Compiled);
        
        public RefactoredStandardLogLineParser(LevelDetectionService levelDetectionService) 
            : base(levelDetectionService)
        {
        }
        
        public override bool IsLogLine(string line) => StandardLogRegex.IsMatch(line);
        
        protected override ParsedLogData? ParseFormatSpecificData(string line)
        {
            var match = StandardLogRegex.Match(line);
            if (!match.Success)
                return null;
            
            // Parse timestamp
            DateTime timestamp;
            if (!DateTime.TryParse(match.Groups[1].Value.Replace(',', '.'), out timestamp))
                timestamp = DateTime.Now;
            
            // Extract message
            var message = match.Groups[2].Value.Trim();
            
            return new ParsedLogData
            {
                Timestamp = timestamp,
                Message = message,
                Source = string.Empty // Standard format doesn't have explicit source
            };
        }
    }
} 
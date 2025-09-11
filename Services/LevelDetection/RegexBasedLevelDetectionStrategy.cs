using System;
using System.Text.RegularExpressions;
using Log_Parser_App.Interfaces;

namespace Log_Parser_App.Services.LevelDetection
{
    /// <summary>
    /// Strategy that detects log level using regex patterns for structured logs
    /// SOLID: Single Responsibility - only handles regex-based level detection
    /// </summary>
    public class RegexBasedLevelDetectionStrategy : ILevelDetectionStrategy
    {
        private static readonly Regex LevelRegex = new(@"\b(INFO|ERROR|WARNING|DEBUG|TRACE|CRITICAL|VERBOSE)\b", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        public int Priority => 5; // Medium priority - runs after false positive but before keyword-based
        
        public string DetectLevel(string message, string rawLine)
        {
            if (string.IsNullOrEmpty(message))
                return "CONTINUE";
            
            var levelMatch = LevelRegex.Match(message);
            if (levelMatch.Success)
            {
                return levelMatch.Value.ToUpperInvariant();
            }
            
            // If no explicit level found in message, continue to next strategy
            return "CONTINUE";
        }
    }
} 
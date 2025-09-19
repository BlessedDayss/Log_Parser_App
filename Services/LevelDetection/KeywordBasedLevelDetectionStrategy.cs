using System;
using System.Text.RegularExpressions;
using Log_Parser_App.Interfaces;

namespace Log_Parser_App.Services.LevelDetection
{
    /// <summary>
    /// Strategy that detects log level based on keywords in the message
    /// SOLID: Single Responsibility - only handles keyword-based level detection
    /// Uses word boundaries (\b) to match only complete words, not substrings
    /// </summary>
    public class KeywordBasedLevelDetectionStrategy : ILevelDetectionStrategy
    {
        // Compiled regexes for performance - use word boundaries to match complete words only
        // Only singular forms to avoid false positives from plural forms like "errors", "warnings"
        private static readonly Regex ErrorKeywordsRegex = new(@"\b(error|exception|failed|critical|fatal|not\s+found|FileNotFoundException|Access\s+denied|NullReferenceException|stack\s*trace|InvalidNameException|invalid\s*name)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex WarningKeywordsRegex = new(@"\b(warning|warn)\b", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex DebugKeywordsRegex = new(@"\b(debug)\b", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TraceKeywordsRegex = new(@"\b(trace)\b", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        public int Priority => 10; // Lower priority - runs after false positive exclusion
        
        public string DetectLevel(string message, string rawLine)
        {
            if (string.IsNullOrEmpty(message))
                return "INFO";
            
            // Check for error keywords using word boundaries (singular forms only)
            // This will match "error", "Error", "ERROR" but NOT "usererror", "errorcode", "errors"
            if (ErrorKeywordsRegex.IsMatch(message))
            {
                return "ERROR";
            }
            
            // Check for warning keywords using word boundaries (singular forms only)
            // This will match "warning", "warn" but NOT "prewarning", "warning123", "warnings"
            if (WarningKeywordsRegex.IsMatch(message))
            {
                return "WARNING";
            }
            
            // Check for debug keywords using word boundaries
            if (DebugKeywordsRegex.IsMatch(message))
            {
                return "DEBUG";
            }
            
            // Check for trace keywords using word boundaries
            if (TraceKeywordsRegex.IsMatch(message))
            {
                return "TRACE";
            }
            
            // Default to INFO if no specific keywords found
            return "INFO";
        }
    }
} 
using Log_Parser_App.Interfaces;

namespace Log_Parser_App.Services.LevelDetection
{
    /// <summary>
    /// High-priority strategy that prevents false positive error/warning detection
    /// Handles patterns like "0 Error", "0 Warning", "0 Errors", "0 Warnings"
    /// SOLID: Single Responsibility - only handles false positive exclusion
    /// </summary>
    public class FalsePositiveExclusionStrategy : ILevelDetectionStrategy
    {
        public int Priority => 1; // Highest priority - runs first
        
        public string DetectLevel(string message, string rawLine)
        {
            // If this is a false positive, override to INFO regardless of other patterns
            if (IsZeroErrorOrWarningFalsePositive(message) || IsZeroErrorOrWarningFalsePositive(rawLine))
            {
                return "INFO";
            }
            
            // If not a false positive, let other strategies handle it
            return "CONTINUE"; // Special return value meaning "continue to next strategy"
        }
        
        /// <summary>
        /// Checks if a line contains "0 Error" or "0 Warning" pattern which should not be treated as error/warning
        /// Centralized logic that was previously duplicated across all parsers
        /// </summary>
        /// <param name="text">Text to check (message or raw line)</param>
        /// <returns>True if text contains "0 error", "0 errors", "0 warning", or "0 warnings" pattern</returns>
        private static bool IsZeroErrorOrWarningFalsePositive(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
                
            var lowerText = text.ToLowerInvariant();
            return lowerText.Contains("0 error") || lowerText.Contains("0 errors") ||
                   lowerText.Contains("0 warning") || lowerText.Contains("0 warnings");
        }
    }
} 
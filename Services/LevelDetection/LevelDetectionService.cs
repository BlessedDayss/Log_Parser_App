using System.Collections.Generic;
using System.Linq;
using Log_Parser_App.Interfaces;

namespace Log_Parser_App.Services.LevelDetection
{
    /// <summary>
    /// Centralized service for log level detection using Chain of Responsibility pattern
    /// SOLID: Single Responsibility - coordinates level detection strategies
    /// SOLID: Open/Closed - easily extensible with new strategies
    /// </summary>
    public class LevelDetectionService
    {
        private readonly List<ILevelDetectionStrategy> _strategies;
        
        public LevelDetectionService(IEnumerable<ILevelDetectionStrategy> strategies)
        {
            // Sort strategies by priority (lower number = higher priority)
            _strategies = strategies.OrderBy(s => s.Priority).ToList();
        }
        
        /// <summary>
        /// Detects log level by running through all strategies in priority order
        /// Stops at first strategy that returns a definitive result (not "CONTINUE")
        /// </summary>
        /// <param name="message">The log message content</param>
        /// <param name="rawLine">The original raw log line</param>
        /// <returns>The detected log level</returns>
        public string DetectLevel(string message, string rawLine)
        {
            foreach (var strategy in _strategies)
            {
                var result = strategy.DetectLevel(message, rawLine);
                
                // If strategy returns a definitive result (not "CONTINUE"), use it
                if (result != "CONTINUE")
                {
                    return result;
                }
            }
            
            // If no strategy provided a definitive result, default to INFO
            return "INFO";
        }
    }
} 
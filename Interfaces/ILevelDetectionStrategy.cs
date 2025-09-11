using Log_Parser_App.Models;

namespace Log_Parser_App.Interfaces
{
    /// <summary>
    /// Strategy interface for detecting log level from parsed log entry
    /// Separates level detection logic from format parsing logic (SOLID - Single Responsibility)
    /// </summary>
    public interface ILevelDetectionStrategy
    {
        /// <summary>
        /// Determines the log level based on the message content and context
        /// </summary>
        /// <param name="message">The log message content</param>
        /// <param name="rawLine">The original raw log line</param>
        /// <returns>The detected log level (INFO, WARNING, ERROR, etc.)</returns>
        string DetectLevel(string message, string rawLine);
        
        /// <summary>
        /// Gets the priority of this strategy (lower number = higher priority)
        /// Used for ordering multiple strategies
        /// </summary>
        int Priority { get; }
    }
} 
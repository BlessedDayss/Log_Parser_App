using System.Collections.Generic;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Interfaces;

/// <summary>
/// Factory interface for creating log type handlers following Factory pattern.
/// Provides centralized creation and registration of log type handlers.
/// </summary>
public interface ILogTypeHandlerFactory
{
    /// <summary>
    /// Create handler for specific log format type
    /// </summary>
    /// <param name="logType">Log format type to create handler for</param>
    /// <returns>Handler instance for the specified log type</returns>
    /// <exception cref="ArgumentException">Thrown when log type is not supported</exception>
    ILogTypeHandler CreateHandler(LogFormatType logType);
    
    /// <summary>
    /// Register new log type handler
    /// </summary>
    /// <param name="handler">Handler instance to register</param>
    /// <exception cref="ArgumentException">Thrown when handler for this log type is already registered</exception>
    void RegisterHandler(ILogTypeHandler handler);
    
    /// <summary>
    /// Unregister log type handler
    /// </summary>
    /// <param name="logType">Log type to unregister handler for</param>
    /// <returns>True if handler was unregistered successfully</returns>
    bool UnregisterHandler(LogFormatType logType);
    
    /// <summary>
    /// Check if handler for log type is available
    /// </summary>
    /// <param name="logType">Log type to check</param>
    /// <returns>True if handler is available for the log type</returns>
    bool IsHandlerAvailable(LogFormatType logType);
    
    /// <summary>
    /// Get all supported log format types
    /// </summary>
    /// <returns>Collection of supported log format types</returns>
    IEnumerable<LogFormatType> GetSupportedLogTypes();
    
    /// <summary>
    /// Get all registered handlers
    /// </summary>
    /// <returns>Collection of all registered handlers</returns>
    IEnumerable<ILogTypeHandler> GetAllHandlers();
    
    /// <summary>
    /// Auto-detect log type from file path/content
    /// </summary>
    /// <param name="filePath">Path to the file to analyze</param>
    /// <param name="fileName">Optional file name for additional context</param>
    /// <returns>Detected log format type or Standard if cannot be determined</returns>
    Task<LogFormatType> DetectLogType(string filePath, string? fileName = null);
    
    /// <summary>
    /// Process multiple log files in batch
    /// </summary>
    /// <param name="filePaths">Collection of file paths to process</param>
    /// <returns>Dictionary mapping file paths to their processing results</returns>
    Task<Dictionary<string, IEnumerable<LogEntry>>> ProcessBatchAsync(IEnumerable<string> filePaths);
    
    /// <summary>
    /// Validate file can be processed by any registered handler
    /// </summary>
    /// <param name="filePath">Path to the file to validate</param>
    /// <returns>True if file can be processed</returns>
    Task<bool> CanProcessFileAsync(string filePath);
    
    /// <summary>
    /// Get handler that can process the given file
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>Handler that can process the file, or null if none found</returns>
    Task<ILogTypeHandler?> GetHandlerForFileAsync(string filePath);
    
    /// <summary>
    /// Get performance statistics for all handlers
    /// </summary>
    /// <returns>Dictionary of handler performance metrics</returns>
    Dictionary<LogFormatType, Dictionary<string, double>> GetPerformanceStatistics();
} 

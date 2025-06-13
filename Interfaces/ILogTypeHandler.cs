using System.Collections.Generic;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Interfaces;

/// <summary>
/// Strategy interface for handling different log types following Strategy pattern.
/// Each log type handler implements specific parsing, formatting, and analysis logic.
/// </summary>
public interface ILogTypeHandler
{
    /// <summary>
    /// Log format type that this handler supports
    /// </summary>
    LogFormatType SupportedLogType { get; }
    
    /// <summary>
    /// Parse log file using type-specific parsing logic
    /// </summary>
    /// <param name="filePath">Path to the log file</param>
    /// <returns>Collection of parsed log entries</returns>
    Task<IEnumerable<LogEntry>> ParseLogFileAsync(string filePath);
    
    /// <summary>
    /// Validate if file can be handled by this log type handler
    /// </summary>
    /// <param name="filePath">Path to the file to validate</param>
    /// <returns>True if this handler can process the file</returns>
    Task<bool> CanHandleAsync(string filePath);
    
    /// <summary>
    /// Get processing options specific to this log type
    /// </summary>
    /// <returns>Processing configuration for this log type</returns>
    LogProcessingOptions GetProcessingOptions();
    
    /// <summary>
    /// Apply log type specific processing and enrichment
    /// </summary>
    /// <param name="entry">Log entry to process</param>
    /// <returns>Processed log entry</returns>
    Task<LogEntry> ProcessLogEntryAsync(LogEntry entry);
    
    /// <summary>
    /// Get validation rules specific to this log type
    /// </summary>
    /// <returns>Collection of validation rules</returns>
    IEnumerable<LogValidationRule> GetValidationRules();
    
    /// <summary>
    /// Extract metadata specific to this log type
    /// </summary>
    /// <param name="entries">Log entries to extract metadata from</param>
    /// <returns>Rich metadata object</returns>
    LogMetadata ExtractMetadata(IEnumerable<LogEntry> entries);
} 

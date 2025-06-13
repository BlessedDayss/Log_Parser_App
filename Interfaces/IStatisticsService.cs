using System.Collections.Generic;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Interfaces;

/// <summary>
/// Service interface for statistics calculation and management following SRP principle.
/// Handles log statistics, aggregation, and real-time updates.
/// </summary>
public interface IStatisticsService
{
    /// <summary>
    /// Calculate comprehensive statistics for log entries
    /// </summary>
    /// <param name="logEntries">Log entries to analyze</param>
    /// <returns>Calculated log statistics</returns>
    Task<LogStatistics> CalculateStatisticsAsync(IEnumerable<LogEntry> logEntries);
    
    /// <summary>
    /// Calculate real-time statistics update
    /// </summary>
    /// <param name="existingStats">Current statistics</param>
    /// <param name="newEntries">New log entries to incorporate</param>
    /// <returns>Updated statistics</returns>
    Task<LogStatistics> UpdateStatisticsAsync(LogStatistics existingStats, IEnumerable<LogEntry> newEntries);
    
    /// <summary>
    /// Calculate error rate percentage
    /// </summary>
    /// <param name="logEntries">Log entries to analyze</param>
    /// <returns>Error rate as percentage (0-100)</returns>
    Task<double> CalculateErrorRateAsync(IEnumerable<LogEntry> logEntries);
    
    /// <summary>
    /// Get hourly distribution of log entries
    /// </summary>
    /// <param name="logEntries">Log entries to analyze</param>
    /// <returns>Dictionary with hour (0-23) and count</returns>
    Task<Dictionary<int, int>> GetHourlyDistributionAsync(IEnumerable<LogEntry> logEntries);
    
    /// <summary>
    /// Get log level distribution
    /// </summary>
    /// <param name="logEntries">Log entries to analyze</param>
    /// <returns>Dictionary with log level and count</returns>
    Task<Dictionary<string, int>> GetLogLevelDistributionAsync(IEnumerable<LogEntry> logEntries);
    
    /// <summary>
    /// Calculate average processing time (for performance logs)
    /// </summary>
    /// <param name="logEntries">Log entries to analyze</param>
    /// <returns>Average processing time in milliseconds</returns>
    Task<double> CalculateAverageProcessingTimeAsync(IEnumerable<LogEntry> logEntries);
    
    /// <summary>
    /// Get top error messages by frequency
    /// </summary>
    /// <param name="logEntries">Log entries to analyze</param>
    /// <param name="topCount">Number of top errors to return</param>
    /// <returns>Dictionary with error message and frequency</returns>
    Task<Dictionary<string, int>> GetTopErrorsAsync(IEnumerable<LogEntry> logEntries, int topCount = 10);
    
    /// <summary>
    /// Export statistics to formatted string
    /// </summary>
    /// <param name="statistics">Statistics to export</param>
    /// <param name="format">Export format (JSON, CSV, etc.)</param>
    /// <returns>Formatted statistics string</returns>
    string ExportStatistics(LogStatistics statistics, string format = "JSON");
} 

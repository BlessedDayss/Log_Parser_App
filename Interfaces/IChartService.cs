using System.Collections.Generic;
using System.Threading.Tasks;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Log_Parser_App.Models;

namespace Log_Parser_App.Interfaces;

/// <summary>
/// Service interface for chart generation and management following ISP principle.
/// Handles chart calculations, series generation, and chart data preparation.
/// </summary>
public interface IChartService
{
    /// <summary>
    /// Calculate and generate chart series for log entries
    /// </summary>
    /// <param name="logEntries">Collection of log entries to analyze</param>
    /// <param name="logType">Type of log format for appropriate chart generation</param>
    /// <returns>Collection of chart series ready for display</returns>
    Task<IEnumerable<ISeries>> CalculateChartSeriesAsync(IEnumerable<LogEntry> logEntries, LogFormatType logType);
    
    /// <summary>
    /// Generate hourly statistics chart for time-based analysis
    /// </summary>
    /// <param name="logEntries">Log entries to analyze</param>
    /// <returns>Chart series showing hourly distribution</returns>
    Task<ISeries> GenerateHourlyStatsAsync(IEnumerable<LogEntry> logEntries);
    
    /// <summary>
    /// Generate error level distribution chart
    /// </summary>
    /// <param name="logEntries">Log entries to analyze</param>
    /// <returns>Chart series showing error level distribution</returns>
    Task<ISeries> GenerateErrorLevelChartAsync(IEnumerable<LogEntry> logEntries);
    
    /// <summary>
    /// Generate chart axes configuration for specific log type
    /// </summary>
    /// <param name="logType">Log format type</param>
    /// <returns>Configured axes for the chart</returns>
    IEnumerable<Axis> GenerateChartAxes(LogFormatType logType);
    
    /// <summary>
    /// Update chart configuration for different dashboard types
    /// </summary>
    /// <param name="isDashboardVisible">Whether dashboard mode is active</param>
    /// <param name="logType">Current log format type</param>
    void UpdateChartConfiguration(bool isDashboardVisible, LogFormatType logType);
} 

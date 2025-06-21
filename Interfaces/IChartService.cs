using System;
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
    /// Generate comprehensive chart data for statistics view
    /// </summary>
    /// <param name="logEntries">Log entries to analyze</param>
    /// <returns>Complete chart data including series and axes</returns>
    ChartDataResult GenerateCharts(IEnumerable<LogEntry> logEntries);
}

/// <summary>
/// Result model for chart generation containing all chart data
/// </summary>
public class ChartDataResult
{
    public ISeries[] LevelsOverTimeSeries { get; set; } = Array.Empty<ISeries>();
    public ISeries[] TopErrorsSeries { get; set; } = Array.Empty<ISeries>();
    public ISeries[] LogDistributionSeries { get; set; } = Array.Empty<ISeries>();
    public ISeries[] TimeHeatmapSeries { get; set; } = Array.Empty<ISeries>();
    public ISeries[] ErrorTrendSeries { get; set; } = Array.Empty<ISeries>();
    public ISeries[] SourcesDistributionSeries { get; set; } = Array.Empty<ISeries>();
    
    public Axis[] TimeAxis { get; set; } = Array.Empty<Axis>();
    public Axis[] CountAxis { get; set; } = Array.Empty<Axis>();
    public Axis[] DaysAxis { get; set; } = Array.Empty<Axis>();
    public Axis[] HoursAxis { get; set; } = Array.Empty<Axis>();
    public Axis[] SourceAxis { get; set; } = Array.Empty<Axis>();
    public Axis[] ErrorMessageAxis { get; set; } = Array.Empty<Axis>();
} 

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Log_Parser_App.Services;

/// <summary>
/// Chart service implementation handling chart generation and management for different log types.
/// Extracted from MainViewModel to follow SRP principle.
/// </summary>
public class ChartService : IChartService
{
    private readonly ILogger<ChartService> _logger;

    public ChartService(ILogger<ChartService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calculate and generate chart series for log entries
    /// </summary>
    public async Task<IEnumerable<ISeries>> CalculateChartSeriesAsync(IEnumerable<LogEntry> logEntries, LogFormatType logType)
    {
        return await Task.Run(() =>
        {
            var entries = logEntries.ToList();
            if (!entries.Any())
                return Enumerable.Empty<ISeries>();

            try
            {
                var result = CalculateStatisticsAndCharts(entries);
                return result.AllSeries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating chart series for log type {LogType}", logType);
                return Enumerable.Empty<ISeries>();
            }
        });
    }

    /// <summary>
    /// Generate hourly statistics chart for time-based analysis
    /// </summary>
    public async Task<ISeries> GenerateHourlyStatsAsync(IEnumerable<LogEntry> logEntries)
    {
        return await Task.Run(() =>
        {
            var entries = logEntries.ToList();
            var hourlyData = entries
                .GroupBy(e => e.Timestamp.Hour)
                .OrderBy(g => g.Key)
                .Select(g => new ObservablePoint(g.Key, g.Count()))
                .ToArray();

            return new LineSeries<ObservablePoint>
            {
                Values = hourlyData,
                Name = "Hourly Distribution",
                Stroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 2 },
                Fill = null,
                GeometryStroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.White),
                GeometrySize = 4
            };
        });
    }

    /// <summary>
    /// Generate error level distribution chart
    /// </summary>
    public async Task<ISeries> GenerateErrorLevelChartAsync(IEnumerable<LogEntry> logEntries)
    {
        return await Task.Run(() =>
        {
            var entries = logEntries.ToList();
            var levelData = entries
                .GroupBy(e => e.Level.ToUpperInvariant())
                .Select(g => new ObservablePoint(g.Key.GetHashCode(), g.Count()))
                .ToArray();

            return new PieSeries<ObservablePoint>
            {
                Values = levelData,
                Name = "Error Level Distribution"
            };
        });
    }

    /// <summary>
    /// Generate chart axes configuration for specific log type
    /// </summary>
    public IEnumerable<Axis> GenerateChartAxes(LogFormatType logType)
    {
        return logType switch
        {
            LogFormatType.IIS => GenerateIISAxes(),
            LogFormatType.Standard => GenerateStandardAxes(),
            LogFormatType.RabbitMQ => GenerateRabbitMQAxes(),
            _ => GenerateStandardAxes()
        };
    }



    /// <summary>
    /// Generate comprehensive chart data for statistics view
    /// </summary>
    public ChartDataResult GenerateCharts(IEnumerable<LogEntry> logEntries)
    {
        try
        {
            var entries = logEntries.ToList();
            if (!entries.Any())
            {
                return new ChartDataResult();
            }

            var result = CalculateStatisticsAndCharts(entries);
            
            return new ChartDataResult
            {
                LevelsOverTimeSeries = result.LevelsOverTimeSeries,
                TopErrorsSeries = result.TopErrorsSeries,
                LogDistributionSeries = result.LogDistributionSeries,
                TimeHeatmapSeries = result.TimeHeatmapSeries,
                ErrorTrendSeries = result.ErrorTrendSeries,
                SourcesDistributionSeries = result.SourcesDistributionSeries,
                TimeAxis = result.TimeAxis,
                CountAxis = result.CountAxis,
                DaysAxis = result.DaysAxis,
                HoursAxis = result.HoursAxis,
                SourceAxis = result.SourceAxis,
                ErrorMessageAxis = result.ErrorMessageAxis
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating charts");
            return new ChartDataResult();
        }
    }

    /// <summary>
    /// Main chart calculation method extracted from MainViewModel
    /// </summary>
    private ChartCalculationResult CalculateStatisticsAndCharts(List<LogEntry> logEntries)
    {
        var result = new ChartCalculationResult();
        
        if (!logEntries.Any())
            return result;

        try
        {
            // Calculate basic statistics
            var errorEntries = logEntries.Where(e => e.Level.Contains("Error", StringComparison.OrdinalIgnoreCase) 
                                                  || e.Level.Contains("Fatal", StringComparison.OrdinalIgnoreCase)
                                                  || e.Level.Contains("Critical", StringComparison.OrdinalIgnoreCase)).ToList();
            var warningEntries = logEntries.Where(e => e.Level.Contains("Warning", StringComparison.OrdinalIgnoreCase) 
                                                    || e.Level.Contains("Warn", StringComparison.OrdinalIgnoreCase)).ToList();
            var infoEntries = logEntries.Where(e => e.Level.Contains("Info", StringComparison.OrdinalIgnoreCase)).ToList();
            var otherEntries = logEntries.Except(errorEntries).Except(warningEntries).Except(infoEntries).ToList();

            var totalCount = logEntries.Count;
            result.ErrorCount = errorEntries.Count;
            result.WarningCount = warningEntries.Count;
            result.InfoCount = infoEntries.Count;
            result.OtherCount = otherEntries.Count;

            if (totalCount > 0)
            {
                result.ErrorPercent = (double)result.ErrorCount / totalCount * 100;
                result.WarningPercent = (double)result.WarningCount / totalCount * 100;
                result.InfoPercent = (double)result.InfoCount / totalCount * 100;
                result.OtherPercent = (double)result.OtherCount / totalCount * 100;
            }

            // Generate chart series
            result.LevelsOverTimeSeries = GenerateLevelsOverTimeSeries(logEntries);
            result.TopErrorsSeries = GenerateTopErrorsSeries(errorEntries);
            result.LogDistributionSeries = GenerateLogDistributionSeries(result);
            result.TimeHeatmapSeries = GenerateTimeHeatmapSeries(logEntries);
            result.ErrorTrendSeries = GenerateErrorTrendSeries(errorEntries);
            result.SourcesDistributionSeries = GenerateSourcesDistributionSeries(logEntries);

            // Generate axes
            var timeAxisData = GenerateTimeAxis(logEntries);
            result.TimeAxis = timeAxisData.axes;
            result.CountAxis = GenerateCountAxis();
            result.DaysAxis = GenerateDaysAxis();
            result.HoursAxis = GenerateHoursAxis();
            result.SourceAxis = GenerateSourceAxis(logEntries);
            result.ErrorMessageAxis = GenerateErrorMessageAxis(errorEntries);

            // Create LogStatistics
            result.LogStatistics = new LogStatistics
            {
                TotalCount = totalCount,
                ErrorCount = result.ErrorCount,
                WarningCount = result.WarningCount,
                InfoCount = result.InfoCount,
                OtherCount = result.OtherCount
            };

            // Combine all series
            result.AllSeries = new List<ISeries>()
                .Concat(result.LevelsOverTimeSeries)
                .Concat(result.TopErrorsSeries)
                .Concat(result.LogDistributionSeries)
                .Concat(result.TimeHeatmapSeries)
                .Concat(result.ErrorTrendSeries)
                .Concat(result.SourcesDistributionSeries)
                .ToArray();

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chart calculation");
            return result;
        }
    }

    private ISeries[] GenerateLevelsOverTimeSeries(List<LogEntry> logEntries)
    {
        var timeGroups = logEntries
            .GroupBy(e => new DateTime(e.Timestamp.Year, e.Timestamp.Month, e.Timestamp.Day, e.Timestamp.Hour, 0, 0))
            .OrderBy(g => g.Key)
            .ToList();

        var errorSeries = new LineSeries<ObservablePoint>
        {
            Values = timeGroups.Select(g => new ObservablePoint(g.Key.Ticks, g.Count(e => e.Level.Contains("Error", StringComparison.OrdinalIgnoreCase)))).ToArray(),
            Name = "Errors",
            Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 2 },
            Fill = null
        };

        var warningSeries = new LineSeries<ObservablePoint>
        {
            Values = timeGroups.Select(g => new ObservablePoint(g.Key.Ticks, g.Count(e => e.Level.Contains("Warning", StringComparison.OrdinalIgnoreCase)))).ToArray(),
            Name = "Warnings",
            Stroke = new SolidColorPaint(SKColors.Orange) { StrokeThickness = 2 },
            Fill = null
        };

        var infoSeries = new LineSeries<ObservablePoint>
        {
            Values = timeGroups.Select(g => new ObservablePoint(g.Key.Ticks, g.Count(e => e.Level.Contains("Info", StringComparison.OrdinalIgnoreCase)))).ToArray(),
            Name = "Info",
            Stroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 2 },
            Fill = null
        };

        return new ISeries[] { errorSeries, warningSeries, infoSeries };
    }

    private ISeries[] GenerateTopErrorsSeries(List<LogEntry> errorEntries)
    {
        var topErrors = errorEntries
            .GroupBy(e => TruncateMessage(e.Message, 50))
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => new ObservablePoint(g.Key.GetHashCode(), g.Count()))
            .ToArray();

        return new ISeries[]
        {
            new ColumnSeries<ObservablePoint>
            {
                Values = topErrors,
                Name = "Top Errors",
                Fill = new SolidColorPaint(SKColors.DarkRed)
            }
        };
    }

    private ISeries[] GenerateLogDistributionSeries(ChartCalculationResult result)
    {
        var distributionData = new List<ObservablePoint>
        {
            new(0, result.ErrorCount),
            new(1, result.WarningCount),
            new(2, result.InfoCount),
            new(3, result.OtherCount)
        };

        return new ISeries[]
        {
            new PieSeries<ObservablePoint>
            {
                Values = distributionData.ToArray(),
                Name = "Log Distribution"
            }
        };
    }

    private ISeries[] GenerateTimeHeatmapSeries(List<LogEntry> logEntries)
    {
        var heatmapData = logEntries
            .GroupBy(e => new { Day = (int)e.Timestamp.DayOfWeek, Hour = e.Timestamp.Hour })
            .Select(g => new ObservablePoint(g.Key.Day * 24 + g.Key.Hour, g.Count()))
            .ToArray();

        return new ISeries[]
        {
            new HeatSeries<ObservablePoint>
            {
                Values = heatmapData,
                Name = "Activity Heatmap"
            }
        };
    }

    private ISeries[] GenerateErrorTrendSeries(List<LogEntry> errorEntries)
    {
        var dailyErrors = errorEntries
            .GroupBy(e => e.Timestamp.Date)
            .OrderBy(g => g.Key)
            .Select(g => new ObservablePoint(g.Key.Ticks, g.Count()))
            .ToArray();

        return new ISeries[]
        {
            new LineSeries<ObservablePoint>
            {
                Values = dailyErrors,
                Name = "Error Trend",
                Stroke = new SolidColorPaint(SKColors.DarkRed) { StrokeThickness = 3 },
                Fill = new SolidColorPaint(SKColors.DarkRed.WithAlpha(50))
            }
        };
    }

    private ISeries[] GenerateSourcesDistributionSeries(List<LogEntry> logEntries)
    {
        var sourceData = logEntries
            .GroupBy(e => e.Source ?? "Unknown")
            .Select(g => new ObservablePoint(g.Key.GetHashCode(), g.Count()))
            .ToArray();

        return new ISeries[]
        {
            new PieSeries<ObservablePoint>
            {
                Values = sourceData,
                Name = "Sources Distribution"
            }
        };
    }

    private (Axis[] axes, int tickInterval, string timeFormat) GenerateTimeAxis(List<LogEntry> logEntries)
    {
        if (!logEntries.Any())
            return (new Axis[0], 1, "HH:mm");

        var minTimestamp = logEntries.Min(e => e.Timestamp);
        var maxTimestamp = logEntries.Max(e => e.Timestamp);
        var tickInterval = DetermineOptimalTimeInterval(minTimestamp, maxTimestamp);
        var timeFormat = DetermineTimeFormat(minTimestamp, maxTimestamp, tickInterval);

        var axis = new Axis
        {
            Name = "Time",
            Labeler = value => new DateTime((long)value).ToString(timeFormat, CultureInfo.InvariantCulture),
            UnitWidth = TimeSpan.FromMinutes(tickInterval).Ticks,
            MinStep = TimeSpan.FromMinutes(tickInterval).Ticks
        };

        return (new[] { axis }, tickInterval, timeFormat);
    }

    private Axis[] GenerateCountAxis()
    {
        return new[]
        {
            new Axis
            {
                Name = "Count",
                Labeler = value => value.ToString("N0")
            }
        };
    }

    private Axis[] GenerateDaysAxis()
    {
        return new[]
        {
            new Axis
            {
                Name = "Day of Week",
                Labeler = value => ((DayOfWeek)(int)value).ToString()
            }
        };
    }

    private Axis[] GenerateHoursAxis()
    {
        return new[]
        {
            new Axis
            {
                Name = "Hour",
                Labeler = value => $"{value:00}:00"
            }
        };
    }

    private Axis[] GenerateSourceAxis(List<LogEntry> logEntries)
    {
        var sources = logEntries.Select(e => e.Source ?? "Unknown").Distinct().ToArray();
        return new[]
        {
            new Axis
            {
                Name = "Source",
                Labels = sources
            }
        };
    }

    private Axis[] GenerateErrorMessageAxis(List<LogEntry> errorEntries)
    {
        var errorMessages = errorEntries
            .GroupBy(e => TruncateMessage(e.Message, 30))
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToArray();

        return new[]
        {
            new Axis
            {
                Name = "Error Message",
                Labels = errorMessages
            }
        };
    }

    private Axis[] GenerateIISAxes()
    {
        return new[]
        {
            new Axis { Name = "Time", Labeler = value => new DateTime((long)value).ToString("HH:mm", CultureInfo.InvariantCulture) },
            new Axis { Name = "Response Code", Labeler = value => value.ToString("N0") }
        };
    }

    private Axis[] GenerateStandardAxes()
    {
        return new[]
        {
            new Axis { Name = "Time", Labeler = value => new DateTime((long)value).ToString("HH:mm", CultureInfo.InvariantCulture) },
            new Axis { Name = "Count", Labeler = value => value.ToString("N0") }
        };
    }

    private Axis[] GenerateRabbitMQAxes()
    {
        return new[]
        {
            new Axis { Name = "Time", Labeler = value => new DateTime((long)value).ToString("HH:mm", CultureInfo.InvariantCulture) },
            new Axis { Name = "Queue Activity", Labeler = value => value.ToString("N0") }
        };
    }

    private string TruncateMessage(string message, int maxLength)
    {
        return message.Length <= maxLength ? message : message.Substring(0, maxLength) + "...";
    }

    private int DetermineOptimalTimeInterval(DateTime minTimestamp, DateTime maxTimestamp)
    {
        var timeSpan = maxTimestamp - minTimestamp;
        return timeSpan.TotalHours switch
        {
            <= 1 => 5,    // 5 minutes
            <= 6 => 30,   // 30 minutes
            <= 24 => 60,  // 1 hour
            <= 168 => 360, // 6 hours
            _ => 1440     // 24 hours
        };
    }

    private string DetermineTimeFormat(DateTime minTimestamp, DateTime maxTimestamp, int tickInterval)
    {
        var timeSpan = maxTimestamp - minTimestamp;
        return timeSpan.TotalDays > 1 ? "MM/dd HH:mm" : "HH:mm";
    }

    /// <summary>
    /// Result container for chart calculations
    /// </summary>
    private class ChartCalculationResult
    {
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public int InfoCount { get; set; }
        public int OtherCount { get; set; }
        public double ErrorPercent { get; set; }
        public double WarningPercent { get; set; }
        public double InfoPercent { get; set; }
        public double OtherPercent { get; set; }
        public ISeries[] LevelsOverTimeSeries { get; set; } = Array.Empty<ISeries>();
        public ISeries[] TopErrorsSeries { get; set; } = Array.Empty<ISeries>();
        public ISeries[] LogDistributionSeries { get; set; } = Array.Empty<ISeries>();
        public ISeries[] TimeHeatmapSeries { get; set; } = Array.Empty<ISeries>();
        public ISeries[] ErrorTrendSeries { get; set; } = Array.Empty<ISeries>();
        public ISeries[] SourcesDistributionSeries { get; set; } = Array.Empty<ISeries>();
        public ISeries[] AllSeries { get; set; } = Array.Empty<ISeries>();
        public LogStatistics LogStatistics { get; set; } = new();
        public Axis[] TimeAxis { get; set; } = Array.Empty<Axis>();
        public Axis[] CountAxis { get; set; } = Array.Empty<Axis>();
        public Axis[] DaysAxis { get; set; } = Array.Empty<Axis>();
        public Axis[] HoursAxis { get; set; } = Array.Empty<Axis>();
        public Axis[] SourceAxis { get; set; } = Array.Empty<Axis>();
        public Axis[] ErrorMessageAxis { get; set; } = Array.Empty<Axis>();
    }
} 
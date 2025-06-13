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
/// Service responsible for chart calculations and statistics
/// Extracted from MainViewModel to follow Single Responsibility Principle
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
                return Array.Empty<ISeries>();

            return logType switch
            {
                LogFormatType.IIS => GenerateIISChartSeries(entries),
                LogFormatType.Standard => GenerateStandardChartSeries(entries),
                LogFormatType.RabbitMQ => GenerateRabbitMQChartSeries(entries),
                _ => GenerateStandardChartSeries(entries)
            };
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

            return new ColumnSeries<ObservablePoint>
            {
                Values = hourlyData,
                Name = "Hourly Distribution",
                Fill = new SolidColorPaint(SKColors.Blue)
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
            var errorCount = entries.Count(e => e.Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase));
            var warningCount = entries.Count(e => e.Level.Equals("WARNING", StringComparison.OrdinalIgnoreCase) || 
                                                 e.Level.Equals("WARN", StringComparison.OrdinalIgnoreCase));
            var infoCount = entries.Count(e => e.Level.Equals("INFO", StringComparison.OrdinalIgnoreCase));
            var otherCount = entries.Count - errorCount - warningCount - infoCount;

            var data = new[]
            {
                new ObservableValue(errorCount),
                new ObservableValue(warningCount),
                new ObservableValue(infoCount),
                new ObservableValue(otherCount)
            };

            return new PieSeries<ObservableValue>
            {
                Values = data,
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
    /// Update chart configuration for different dashboard types
    /// </summary>
    public void UpdateChartConfiguration(bool isDashboardVisible, LogFormatType logType)
    {
        _logger.LogDebug("Updating chart configuration for dashboard visibility: {Visible}, log type: {LogType}", 
            isDashboardVisible, logType);
        // Configuration logic can be expanded based on requirements
    }

    /// <summary>
    /// Main chart calculation method extracted from MainViewModel
    /// </summary>
    private ChartCalculationResult CalculateStatisticsAndCharts(List<LogEntry> logEntries)
    {
        if (!logEntries.Any())
        {
            return CreateEmptyResult();
        }

        // Count log levels
        var errorCount = logEntries.Count(e => e.Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase));
        var warningCount = logEntries.Count(e => e.Level.Equals("WARNING", StringComparison.OrdinalIgnoreCase) || 
                                               e.Level.Equals("WARN", StringComparison.OrdinalIgnoreCase));
        var infoCount = logEntries.Count(e => e.Level.Equals("INFO", StringComparison.OrdinalIgnoreCase));
        var otherCount = logEntries.Count - errorCount - warningCount - infoCount;

        var totalCount = logEntries.Count;
        var errorPercent = totalCount > 0 ? (double)errorCount / totalCount * 100 : 0;
        var warningPercent = totalCount > 0 ? (double)warningCount / totalCount * 100 : 0;
        var infoPercent = totalCount > 0 ? (double)infoCount / totalCount * 100 : 0;
        var otherPercent = totalCount > 0 ? (double)otherCount / totalCount * 100 : 0;

        // Generate charts
        var levelsOverTimeSeries = CreateLevelsOverTimeSeries(logEntries);
        var topErrorsSeries = CreateTopErrorsSeries(logEntries);
        var logDistributionSeries = CreateLogDistributionSeries(errorCount, warningCount, infoCount, otherCount);
        var timeHeatmapSeries = CreateTimeHeatmapSeries(logEntries);
        var errorTrendSeries = CreateErrorTrendSeries(logEntries);
        var sourcesDistributionSeries = CreateSourcesDistributionSeries(logEntries);

        // Create axes
        var timeAxis = CreateTimeAxis(logEntries);
        var countAxis = CreateCountAxis();
        var daysAxis = CreateDaysAxis();
        var hoursAxis = CreateHoursAxis();
        var sourceAxis = CreateSourceAxis(logEntries);
        var errorMessageAxis = CreateErrorMessageAxis(logEntries);

        // Create statistics
        var logStatistics = new LogStatistics
        {
            TotalCount = totalCount,
            ErrorCount = errorCount,
            WarningCount = warningCount,
            InfoCount = infoCount,
            OtherCount = otherCount,
            ErrorPercentage = errorPercent,
            WarningPercentage = warningPercent,
            InfoPercentage = infoPercent,
            OtherPercentage = otherPercent,
            FirstTimestamp = logEntries.Min(e => e.Timestamp),
            LastTimestamp = logEntries.Max(e => e.Timestamp)
        };

        return new ChartCalculationResult
        {
            ErrorCount = errorCount,
            WarningCount = warningCount,
            InfoCount = infoCount,
            OtherCount = otherCount,
            ErrorPercent = errorPercent,
            WarningPercent = warningPercent,
            InfoPercent = infoPercent,
            OtherPercent = otherPercent,
            LevelsOverTimeSeries = levelsOverTimeSeries,
            TopErrorsSeries = topErrorsSeries,
            LogDistributionSeries = logDistributionSeries,
            TimeHeatmapSeries = timeHeatmapSeries,
            ErrorTrendSeries = errorTrendSeries,
            SourcesDistributionSeries = sourcesDistributionSeries,
            LogStatistics = logStatistics,
            TimeAxis = timeAxis,
            CountAxis = countAxis,
            DaysAxis = daysAxis,
            HoursAxis = hoursAxis,
            SourceAxis = sourceAxis,
            ErrorMessageAxis = errorMessageAxis
        };
    }

            #region Private Helper Methods

    private ISeries[] CreateLevelsOverTimeSeries(List<LogEntry> logEntries)
    {
        var minTimestamp = logEntries.Min(e => e.Timestamp);
        var maxTimestamp = logEntries.Max(e => e.Timestamp);
        var interval = DetermineOptimalTimeInterval(minTimestamp, maxTimestamp);

        var groupedData = logEntries
            .GroupBy(e => new DateTime(e.Timestamp.Year, e.Timestamp.Month, e.Timestamp.Day, 
                e.Timestamp.Hour - (e.Timestamp.Hour % interval), 0, 0))
            .OrderBy(g => g.Key)
            .ToList();

        var errorData = new List<DateTimePoint>();
        var warningData = new List<DateTimePoint>();
        var infoData = new List<DateTimePoint>();

        foreach (var group in groupedData)
        {
            var timestamp = group.Key;
            var errors = group.Count(e => e.Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase));
            var warnings = group.Count(e => e.Level.Equals("WARNING", StringComparison.OrdinalIgnoreCase) || 
                                            e.Level.Equals("WARN", StringComparison.OrdinalIgnoreCase));
            var infos = group.Count(e => e.Level.Equals("INFO", StringComparison.OrdinalIgnoreCase));

            errorData.Add(new DateTimePoint(timestamp, errors));
            warningData.Add(new DateTimePoint(timestamp, warnings));
            infoData.Add(new DateTimePoint(timestamp, infos));
        }

        return new ISeries[]
        {
            new LineSeries<DateTimePoint>
            {
                Values = errorData,
                Name = "Errors",
                Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 2 },
                Fill = null,
                GeometryStroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.Red),
                GeometrySize = 4
            },
            new LineSeries<DateTimePoint>
            {
                Values = warningData,
                Name = "Warnings",
                Stroke = new SolidColorPaint(SKColors.Orange) { StrokeThickness = 2 },
                Fill = null,
                GeometryStroke = new SolidColorPaint(SKColors.Orange) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.Orange),
                GeometrySize = 4
            },
            new LineSeries<DateTimePoint>
            {
                Values = infoData,
                Name = "Info",
                Stroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 2 },
                Fill = null,
                GeometryStroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.Green),
                GeometrySize = 4
            }
        };
    }

    private ISeries[] CreateTopErrorsSeries(List<LogEntry> logEntries)
    {
        var errorEntries = logEntries.Where(e => e.Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase));
        var topErrors = errorEntries
            .GroupBy(e => TruncateMessage(e.Message, 50))
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => new { Message = g.Key, Count = g.Count() })
            .ToList();

        if (!topErrors.Any())
        {
            return Array.Empty<ISeries>();
        }

        return new ISeries[]
        {
            new ColumnSeries<int>
            {
                Values = topErrors.Select(e => e.Count).ToArray(),
                Name = "Error Count",
                Fill = new SolidColorPaint(SKColors.Red)
            }
        };
    }

    private ISeries[] CreateLogDistributionSeries(int errorCount, int warningCount, int infoCount, int otherCount)
    {
        var data = new List<ObservableValue>
        {
            new(errorCount),
            new(warningCount), 
            new(infoCount),
            new(otherCount)
        };

        return new ISeries[]
        {
            new PieSeries<ObservableValue>
            {
                Values = new[] { data[0] },
                Name = "Errors",
                Fill = new SolidColorPaint(SKColors.Red)
            },
            new PieSeries<ObservableValue>
            {
                Values = new[] { data[1] },
                Name = "Warnings",
                Fill = new SolidColorPaint(SKColors.Orange)
            },
            new PieSeries<ObservableValue>
            {
                Values = new[] { data[2] },
                Name = "Info",
                Fill = new SolidColorPaint(SKColors.Green)
            },
            new PieSeries<ObservableValue>
            {
                Values = new[] { data[3] },
                Name = "Other",
                Fill = new SolidColorPaint(SKColors.Gray)
            }
        };
    }

    private ISeries[] CreateTimeHeatmapSeries(List<LogEntry> logEntries)
    {
        var heatmapData = new List<ObservablePoint>();

        for (int day = 0; day < 7; day++)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                var count = logEntries.Count(e => 
                    (int)e.Timestamp.DayOfWeek == day && 
                    e.Timestamp.Hour == hour);
                
                heatmapData.Add(new ObservablePoint(hour, day));
            }
        }

        return new ISeries[]
        {
            new HeatSeries<ObservablePoint>
            {
                Values = heatmapData,
                Name = "Time Heatmap"
            }
        };
    }

    private ISeries[] CreateErrorTrendSeries(List<LogEntry> logEntries)
    {
        var errorEntries = logEntries.Where(e => e.Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase));
        var trendData = errorEntries
            .GroupBy(e => e.Timestamp.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DateTimePoint(g.Key, g.Count()))
            .ToList();

        return new ISeries[]
        {
            new LineSeries<DateTimePoint>
            {
                Values = trendData,
                Name = "Error Trend",
                Stroke = new SolidColorPaint(SKColors.DarkRed) { StrokeThickness = 3 },
                Fill = new SolidColorPaint(SKColors.Red.WithAlpha(50)),
                GeometryStroke = new SolidColorPaint(SKColors.DarkRed) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.Red),
                GeometrySize = 6
            }
        };
    }

    private ISeries[] CreateSourcesDistributionSeries(List<LogEntry> logEntries)
    {
        var sources = logEntries
            .GroupBy(e => e.Source ?? "Unknown")
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => new { Source = g.Key, Count = g.Count() })
            .ToList();

        return new ISeries[]
        {
            new ColumnSeries<int>
            {
                Values = sources.Select(s => s.Count).ToArray(),
                Name = "Log Count by Source",
                Fill = new SolidColorPaint(SKColors.Blue)
            }
        };
    }

    

    private Axis[] CreateTimeAxis(List<LogEntry> logEntries)
    {
        var minTimestamp = logEntries.Min(e => e.Timestamp);
        var maxTimestamp = logEntries.Max(e => e.Timestamp);
        var interval = DetermineOptimalTimeInterval(minTimestamp, maxTimestamp);

        return new[]
        {
            new Axis
            {
                Name = "Time",
                Labeler = value => new DateTime((long)value).ToString("HH:mm"),
                UnitWidth = TimeSpan.FromHours(interval).Ticks,
                MinStep = TimeSpan.FromHours(interval).Ticks
            }
        };
    }

    private Axis[] CreateCountAxis()
    {
        return new[]
        {
            new Axis
            {
                Name = "Count",
                MinLimit = 0
            }
        };
    }

    private Axis[] CreateDaysAxis()
    {
        return new[]
        {
            new Axis
            {
                Name = "Day of Week",
                Labels = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" }
            }
        };
    }

    private Axis[] CreateHoursAxis()
    {
        return new[]
        {
            new Axis
            {
                Name = "Hour",
                MinLimit = 0,
                MaxLimit = 23,
                MinStep = 1
            }
        };
    }

    private Axis[] CreateSourceAxis(List<LogEntry> logEntries)
    {
        var sources = logEntries
            .GroupBy(e => e.Source ?? "Unknown")
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToArray();

        return new[]
        {
            new Axis
            {
                Name = "Source",
                Labels = sources
            }
        };
    }

    private Axis[] CreateErrorMessageAxis(List<LogEntry> logEntries)
    {
        var errorMessages = logEntries
            .Where(e => e.Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => TruncateMessage(e.Message, 50))
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

    private string TruncateMessage(string message, int maxLength)
    {
        if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
            return message ?? string.Empty;
        
        return message.Substring(0, maxLength) + "...";
    }

    private int DetermineOptimalTimeInterval(DateTime minTimestamp, DateTime maxTimestamp)
    {
        var duration = maxTimestamp - minTimestamp;
        
        if (duration.TotalDays <= 1)
            return 1; // 1 hour intervals
        else if (duration.TotalDays <= 7)
            return 6; // 6 hour intervals
        else if (duration.TotalDays <= 30)
            return 24; // 1 day intervals
        else
            return 168; // 1 week intervals
    }

    // Missing methods from original implementation
    private IEnumerable<ISeries> GenerateIISChartSeries(IEnumerable<LogEntry> entries)
    {
        var entriesList = entries.ToList();
        return new List<ISeries>
        {
            new ColumnSeries<ObservablePoint>
            {
                Values = entriesList.GroupBy(e => e.Timestamp.Hour)
                    .Select(g => new ObservablePoint(g.Key, g.Count()))
                    .ToArray(),
                Name = "IIS Hourly Distribution",
                Fill = new SolidColorPaint(SKColors.Orange)
            }
        };
    }

    private IEnumerable<ISeries> GenerateStandardChartSeries(IEnumerable<LogEntry> entries)
    {
        var entriesList = entries.ToList();
        return new List<ISeries>
        {
            new ColumnSeries<ObservablePoint>
            {
                Values = entriesList.GroupBy(e => e.Timestamp.Hour)
                    .Select(g => new ObservablePoint(g.Key, g.Count()))
                    .ToArray(),
                Name = "Standard Hourly Distribution",
                Fill = new SolidColorPaint(SKColors.Blue)
            }
        };
    }

    private IEnumerable<ISeries> GenerateRabbitMQChartSeries(IEnumerable<LogEntry> entries)
    {
        var entriesList = entries.ToList();
        return new List<ISeries>
        {
            new ColumnSeries<ObservablePoint>
            {
                Values = entriesList.GroupBy(e => e.Timestamp.Hour)
                    .Select(g => new ObservablePoint(g.Key, g.Count()))
                    .ToArray(),
                Name = "RabbitMQ Hourly Distribution",
                Fill = new SolidColorPaint(SKColors.Green)
            }
        };
    }

    private IEnumerable<Axis> GenerateIISAxes()
    {
        return new[]
        {
            new Axis
            {
                Name = "HTTP Status Codes",
                Position = 0 // Bottom
            }
        };
    }

    private IEnumerable<Axis> GenerateStandardAxes()
    {
        return new[]
        {
            new Axis
            {
                Name = "Log Levels",
                Position = 0 // Bottom
            }
        };
    }

    private IEnumerable<Axis> GenerateRabbitMQAxes()
    {
        return new[]
        {
            new Axis
            {
                Name = "Queue Operations",
                Position = 0 // Bottom
            }
        };
    }

    private ChartCalculationResult CreateEmptyResult()
    {
        return new ChartCalculationResult
        {
            ErrorCount = 0,
            WarningCount = 0,
            InfoCount = 0,
            OtherCount = 0,
            ErrorPercent = 0,
            WarningPercent = 0,
            InfoPercent = 0,
            OtherPercent = 0,
            LevelsOverTimeSeries = Array.Empty<ISeries>(),
            TopErrorsSeries = Array.Empty<ISeries>(),
            LogDistributionSeries = Array.Empty<ISeries>(),
            TimeHeatmapSeries = Array.Empty<ISeries>(),
            ErrorTrendSeries = Array.Empty<ISeries>(),
            SourcesDistributionSeries = Array.Empty<ISeries>(),
            LogStatistics = new LogStatistics(),
            TimeAxis = Array.Empty<Axis>(),
            CountAxis = Array.Empty<Axis>(),
            DaysAxis = Array.Empty<Axis>(),
            HoursAxis = Array.Empty<Axis>(),
            SourceAxis = Array.Empty<Axis>(),
            ErrorMessageAxis = Array.Empty<Axis>()
        };
    }

    public ISeries[] GenerateStandardLogSeries(LogStatistics stats)
    {
        return new ISeries[]
        {
            new ColumnSeries<double> { Values = new double[] { stats.TotalCount }, Name = "Total" },
            new ColumnSeries<double> { Values = new double[] { stats.ErrorCount }, Name = "Errors" },
            new ColumnSeries<double> { Values = new double[] { stats.WarningCount }, Name = "Warnings" },
            new ColumnSeries<double> { Values = new double[] { stats.InfoCount }, Name = "Info" },
            new ColumnSeries<double> { Values = new double[] { stats.OtherCount }, Name = "Other" }
        };
    }

    public Axis[] GenerateStandardLogAxes(LogStatistics stats)
    {
        return new[]
        {
            new Axis { Labels = new[] { stats.FirstTimestamp.ToString("HH:mm") } },
            new Axis { Labels = new[] { stats.LastTimestamp.ToString("HH:mm") } }
        };
    }

    #endregion

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
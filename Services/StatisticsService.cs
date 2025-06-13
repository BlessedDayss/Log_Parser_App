using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services
{
    /// <summary>
    /// Service responsible for log statistics calculation and management
    /// Implements statistics aggregation for different log types and formats
    /// </summary>
    public class StatisticsService : IStatisticsService
    {
        private readonly ILogger<StatisticsService> _logger;

        public StatisticsService(ILogger<StatisticsService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Calculate comprehensive statistics for log entries
        /// </summary>
        public async Task<LogStatistics> CalculateStatisticsAsync(IEnumerable<LogEntry> logEntries)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var entries = logEntries.ToList();
                    _logger.LogDebug($"Calculating statistics for {entries.Count} log entries");

                    if (!entries.Any())
                    {
                        return CreateEmptyStatistics();
                    }

                    var errorCount = entries.Count(e => e.Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase));
                    var warningCount = entries.Count(e => e.Level.Equals("WARNING", StringComparison.OrdinalIgnoreCase) || 
                                                         e.Level.Equals("WARN", StringComparison.OrdinalIgnoreCase));
                    var infoCount = entries.Count(e => e.Level.Equals("INFO", StringComparison.OrdinalIgnoreCase));
                    var debugCount = entries.Count(e => e.Level.Equals("DEBUG", StringComparison.OrdinalIgnoreCase));
                    var traceCount = entries.Count(e => e.Level.Equals("TRACE", StringComparison.OrdinalIgnoreCase));
                    var otherCount = entries.Count - errorCount - warningCount - infoCount - debugCount - traceCount;

                    var totalCount = entries.Count;
                    var errorPercent = totalCount > 0 ? (double)errorCount / totalCount * 100 : 0;
                    var warningPercent = totalCount > 0 ? (double)warningCount / totalCount * 100 : 0;
                    var infoPercent = totalCount > 0 ? (double)infoCount / totalCount * 100 : 0;
                    var debugPercent = totalCount > 0 ? (double)debugCount / totalCount * 100 : 0;
                    var tracePercent = totalCount > 0 ? (double)traceCount / totalCount * 100 : 0;
                    var otherPercent = totalCount > 0 ? (double)otherCount / totalCount * 100 : 0;

                    // Time range analysis
                    var firstLogTime = entries.Min(e => e.Timestamp);
                    var lastLogTime = entries.Max(e => e.Timestamp);
                    var timeSpan = lastLogTime - firstLogTime;

                    // Source analysis
                    var uniqueSources = entries
                        .Where(e => !string.IsNullOrEmpty(e.Source))
                        .Select(e => e.Source!)
                        .Distinct()
                        .Count();

                    // Peak activity analysis
                    var peakHour = entries
                        .GroupBy(e => e.Timestamp.Hour)
                        .OrderByDescending(g => g.Count())
                        .FirstOrDefault()?.Key ?? 0;

                    var peakDay = entries
                        .GroupBy(e => e.Timestamp.Date)
                        .OrderByDescending(g => g.Count())
                        .FirstOrDefault()?.Key ?? DateTime.MinValue;

                    var statistics = new LogStatistics
                    {
                        TotalEntries = totalCount,
                        ErrorEntries = errorCount,
                        WarningEntries = warningCount,
                        InfoEntries = infoCount,
                        DebugEntries = debugCount,
                        TraceEntries = traceCount,
                        OtherEntries = otherCount,
                        ErrorPercentage = errorPercent,
                        WarningPercentage = warningPercent,
                        InfoPercentage = infoPercent,
                        DebugPercentage = debugPercent,
                        TracePercentage = tracePercent,
                        OtherPercentage = otherPercent,
                        FirstLogTime = firstLogTime,
                        LastLogTime = lastLogTime,
                        TimeSpan = timeSpan,
                        UniqueSources = uniqueSources,
                        PeakHour = peakHour,
                        PeakDay = peakDay,
                        LogsPerHour = timeSpan.TotalHours > 0 ? totalCount / timeSpan.TotalHours : 0,
                        LogsPerMinute = timeSpan.TotalMinutes > 0 ? totalCount / timeSpan.TotalMinutes : 0
                    };

                    _logger.LogInformation($"Statistics calculated: {totalCount} total, {errorCount} errors, {warningCount} warnings");
                    return statistics;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calculating statistics");
                    return CreateEmptyStatistics();
                }
            });
        }

        /// <summary>
        /// Calculate real-time statistics update for new log entries
        /// </summary>
        public async Task<LogStatistics> UpdateStatisticsAsync(LogStatistics currentStats, IEnumerable<LogEntry> newEntries)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var newEntriesList = newEntries.ToList();
                    _logger.LogDebug($"Updating statistics with {newEntriesList.Count} new entries");

                    if (!newEntriesList.Any())
                    {
                        return currentStats;
                    }

                    // Count new entries by level
                    var newErrorCount = newEntriesList.Count(e => e.Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase));
                    var newWarningCount = newEntriesList.Count(e => e.Level.Equals("WARNING", StringComparison.OrdinalIgnoreCase) || 
                                                               e.Level.Equals("WARN", StringComparison.OrdinalIgnoreCase));
                    var newInfoCount = newEntriesList.Count(e => e.Level.Equals("INFO", StringComparison.OrdinalIgnoreCase));
                    var newDebugCount = newEntriesList.Count(e => e.Level.Equals("DEBUG", StringComparison.OrdinalIgnoreCase));
                    var newTraceCount = newEntriesList.Count(e => e.Level.Equals("TRACE", StringComparison.OrdinalIgnoreCase));
                    var newOtherCount = newEntriesList.Count - newErrorCount - newWarningCount - newInfoCount - newDebugCount - newTraceCount;

                    // Update totals
                    var updatedTotal = currentStats.TotalEntries + newEntriesList.Count;
                    var updatedErrorCount = currentStats.ErrorEntries + newErrorCount;
                    var updatedWarningCount = currentStats.WarningEntries + newWarningCount;
                    var updatedInfoCount = currentStats.InfoEntries + newInfoCount;
                    var updatedDebugCount = currentStats.DebugEntries + newDebugCount;
                    var updatedTraceCount = currentStats.TraceEntries + newTraceCount;
                    var updatedOtherCount = currentStats.OtherEntries + newOtherCount;

                    // Recalculate percentages
                    var errorPercent = updatedTotal > 0 ? (double)updatedErrorCount / updatedTotal * 100 : 0;
                    var warningPercent = updatedTotal > 0 ? (double)updatedWarningCount / updatedTotal * 100 : 0;
                    var infoPercent = updatedTotal > 0 ? (double)updatedInfoCount / updatedTotal * 100 : 0;
                    var debugPercent = updatedTotal > 0 ? (double)updatedDebugCount / updatedTotal * 100 : 0;
                    var tracePercent = updatedTotal > 0 ? (double)updatedTraceCount / updatedTotal * 100 : 0;
                    var otherPercent = updatedTotal > 0 ? (double)updatedOtherCount / updatedTotal * 100 : 0;

                    // Update time range if needed
                    var newFirstLogTime = newEntriesList.Any() ? 
                        new DateTime(Math.Min(currentStats.FirstLogTime.Ticks, newEntriesList.Min(e => e.Timestamp).Ticks)) :
                        currentStats.FirstLogTime;

                    var newLastLogTime = newEntriesList.Any() ?
                        new DateTime(Math.Max(currentStats.LastLogTime.Ticks, newEntriesList.Max(e => e.Timestamp).Ticks)) :
                        currentStats.LastLogTime;

                    var timeSpan = newLastLogTime - newFirstLogTime;

                    var updatedStats = new LogStatistics
                    {
                        TotalEntries = updatedTotal,
                        ErrorEntries = updatedErrorCount,
                        WarningEntries = updatedWarningCount,
                        InfoEntries = updatedInfoCount,
                        DebugEntries = updatedDebugCount,
                        TraceEntries = updatedTraceCount,
                        OtherEntries = updatedOtherCount,
                        ErrorPercentage = errorPercent,
                        WarningPercentage = warningPercent,
                        InfoPercentage = infoPercent,
                        DebugPercentage = debugPercent,
                        TracePercentage = tracePercent,
                        OtherPercentage = otherPercent,
                        FirstLogTime = newFirstLogTime,
                        LastLogTime = newLastLogTime,
                        TimeSpan = timeSpan,
                        UniqueSources = currentStats.UniqueSources, // Would need full recalculation for accuracy
                        PeakHour = currentStats.PeakHour, // Would need full recalculation for accuracy
                        PeakDay = currentStats.PeakDay, // Would need full recalculation for accuracy
                        LogsPerHour = timeSpan.TotalHours > 0 ? updatedTotal / timeSpan.TotalHours : 0,
                        LogsPerMinute = timeSpan.TotalMinutes > 0 ? updatedTotal / timeSpan.TotalMinutes : 0
                    };

                    _logger.LogDebug($"Statistics updated: total now {updatedTotal}, errors now {updatedErrorCount}");
                    return updatedStats;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating statistics");
                    return currentStats;
                }
            });
        }

        /// <summary>
        /// Get statistics for specific log level
        /// </summary>
        public async Task<LevelStatistics> GetLevelStatisticsAsync(IEnumerable<LogEntry> logEntries, string level)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var entries = logEntries.Where(e => e.Level.Equals(level, StringComparison.OrdinalIgnoreCase)).ToList();
                    _logger.LogDebug($"Calculating level statistics for {level}: {entries.Count} entries");

                    if (!entries.Any())
                    {
                        return CreateEmptyLevelStatistics(level);
                    }

                    // Hourly distribution
                    var hourlyDistribution = entries
                        .GroupBy(e => e.Timestamp.Hour)
                        .ToDictionary(g => g.Key, g => g.Count());

                    // Daily distribution
                    var dailyDistribution = entries
                        .GroupBy(e => e.Timestamp.Date)
                        .ToDictionary(g => g.Key, g => g.Count());

                    // Source distribution
                    var sourceDistribution = entries
                        .Where(e => !string.IsNullOrEmpty(e.Source))
                        .GroupBy(e => e.Source!)
                        .ToDictionary(g => g.Key, g => g.Count());

                    // Message patterns (simplified - top 10 most common message prefixes)
                    var messagePatterns = entries
                        .Select(e => e.Message.Length > 50 ? e.Message.Substring(0, 50) + "..." : e.Message)
                        .GroupBy(m => m)
                        .OrderByDescending(g => g.Count())
                        .Take(10)
                        .ToDictionary(g => g.Key, g => g.Count());

                    return new LevelStatistics
                    {
                        Level = level,
                        TotalCount = entries.Count,
                        FirstOccurrence = entries.Min(e => e.Timestamp),
                        LastOccurrence = entries.Max(e => e.Timestamp),
                        HourlyDistribution = hourlyDistribution,
                        DailyDistribution = dailyDistribution,
                        SourceDistribution = sourceDistribution,
                        MessagePatterns = messagePatterns,
                        AveragePerHour = GetAveragePerHour(entries),
                        PeakHour = hourlyDistribution.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key,
                        PeakDay = dailyDistribution.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error calculating level statistics for {level}");
                    return CreateEmptyLevelStatistics(level);
                }
            });
        }

        /// <summary>
        /// Get time-based statistics (hourly/daily aggregation)
        /// </summary>
        public async Task<TimeBasedStatistics> GetTimeBasedStatisticsAsync(IEnumerable<LogEntry> logEntries, TimeGrouping grouping)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var entries = logEntries.ToList();
                    _logger.LogDebug($"Calculating time-based statistics with {grouping} grouping for {entries.Count} entries");

                    if (!entries.Any())
                    {
                        return CreateEmptyTimeBasedStatistics(grouping);
                    }

                    Dictionary<DateTime, int> timeDistribution;
                    Dictionary<DateTime, Dictionary<string, int>> levelDistribution;

                    switch (grouping)
                    {
                        case TimeGrouping.Hourly:
                            timeDistribution = entries
                                .GroupBy(e => new DateTime(e.Timestamp.Year, e.Timestamp.Month, e.Timestamp.Day, e.Timestamp.Hour, 0, 0))
                                .ToDictionary(g => g.Key, g => g.Count());

                            levelDistribution = entries
                                .GroupBy(e => new DateTime(e.Timestamp.Year, e.Timestamp.Month, e.Timestamp.Day, e.Timestamp.Hour, 0, 0))
                                .ToDictionary(g => g.Key, g => g.GroupBy(e => e.Level).ToDictionary(lg => lg.Key, lg => lg.Count()));
                            break;

                        case TimeGrouping.Daily:
                            timeDistribution = entries
                                .GroupBy(e => e.Timestamp.Date)
                                .ToDictionary(g => g.Key, g => g.Count());

                            levelDistribution = entries
                                .GroupBy(e => e.Timestamp.Date)
                                .ToDictionary(g => g.Key, g => g.GroupBy(e => e.Level).ToDictionary(lg => lg.Key, lg => lg.Count()));
                            break;

                        case TimeGrouping.Weekly:
                            timeDistribution = entries
                                .GroupBy(e => GetWeekStart(e.Timestamp))
                                .ToDictionary(g => g.Key, g => g.Count());

                            levelDistribution = entries
                                .GroupBy(e => GetWeekStart(e.Timestamp))
                                .ToDictionary(g => g.Key, g => g.GroupBy(e => e.Level).ToDictionary(lg => lg.Key, lg => lg.Count()));
                            break;

                        default:
                            throw new ArgumentException($"Unsupported time grouping: {grouping}");
                    }

                    return new TimeBasedStatistics
                    {
                        Grouping = grouping,
                        TimeDistribution = timeDistribution,
                        LevelDistribution = levelDistribution,
                        PeakTime = timeDistribution.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key,
                        PeakCount = timeDistribution.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Value,
                        AveragePerPeriod = timeDistribution.Values.Any() ? timeDistribution.Values.Average() : 0,
                        TotalPeriods = timeDistribution.Count
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error calculating time-based statistics with {grouping} grouping");
                    return CreateEmptyTimeBasedStatistics(grouping);
                }
            });
        }

        /// <summary>
        /// Reset statistics (clear cached data)
        /// </summary>
        public async Task ResetStatisticsAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    _logger.LogDebug("Resetting statistics");
                    // Clear any cached statistics data if needed
                    _logger.LogInformation("Statistics reset completed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resetting statistics");
                    throw;
                }
            });
        }

        #region Private Helper Methods

        private LogStatistics CreateEmptyStatistics()
        {
            return new LogStatistics
            {
                TotalEntries = 0,
                ErrorEntries = 0,
                WarningEntries = 0,
                InfoEntries = 0,
                DebugEntries = 0,
                TraceEntries = 0,
                OtherEntries = 0,
                ErrorPercentage = 0,
                WarningPercentage = 0,
                InfoPercentage = 0,
                DebugPercentage = 0,
                TracePercentage = 0,
                OtherPercentage = 0,
                FirstLogTime = DateTime.MinValue,
                LastLogTime = DateTime.MinValue,
                TimeSpan = TimeSpan.Zero,
                UniqueSources = 0,
                PeakHour = 0,
                PeakDay = DateTime.MinValue,
                LogsPerHour = 0,
                LogsPerMinute = 0
            };
        }

        private LevelStatistics CreateEmptyLevelStatistics(string level)
        {
            return new LevelStatistics
            {
                Level = level,
                TotalCount = 0,
                FirstOccurrence = DateTime.MinValue,
                LastOccurrence = DateTime.MinValue,
                HourlyDistribution = new Dictionary<int, int>(),
                DailyDistribution = new Dictionary<DateTime, int>(),
                SourceDistribution = new Dictionary<string, int>(),
                MessagePatterns = new Dictionary<string, int>(),
                AveragePerHour = 0,
                PeakHour = 0,
                PeakDay = DateTime.MinValue
            };
        }

        private TimeBasedStatistics CreateEmptyTimeBasedStatistics(TimeGrouping grouping)
        {
            return new TimeBasedStatistics
            {
                Grouping = grouping,
                TimeDistribution = new Dictionary<DateTime, int>(),
                LevelDistribution = new Dictionary<DateTime, Dictionary<string, int>>(),
                PeakTime = DateTime.MinValue,
                PeakCount = 0,
                AveragePerPeriod = 0,
                TotalPeriods = 0
            };
        }

        private double GetAveragePerHour(List<LogEntry> entries)
        {
            if (!entries.Any())
                return 0;

            var timeSpan = entries.Max(e => e.Timestamp) - entries.Min(e => e.Timestamp);
            return timeSpan.TotalHours > 0 ? entries.Count / timeSpan.TotalHours : 0;
        }

        private DateTime GetWeekStart(DateTime date)
        {
            var diff = (int)date.DayOfWeek - (int)DayOfWeek.Monday;
            if (diff < 0)
                diff += 7;
            return date.AddDays(-diff).Date;
        }

        /// <summary>
        /// Calculate error rate percentage
        /// </summary>
        public async Task<double> CalculateErrorRateAsync(IEnumerable<LogEntry> logEntries)
        {
            var entriesList = logEntries.ToList();
            if (!entriesList.Any())
                return 0.0;

            var errorCount = entriesList.Count(e => e.Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase));
            return await Task.FromResult((double)errorCount / entriesList.Count * 100);
        }

        /// <summary>
        /// Get hourly distribution of log entries
        /// </summary>
        public async Task<Dictionary<int, int>> GetHourlyDistributionAsync(IEnumerable<LogEntry> logEntries)
        {
            return await Task.FromResult(logEntries
                .GroupBy(e => e.Timestamp.Hour)
                .ToDictionary(g => g.Key, g => g.Count()));
        }

        /// <summary>
        /// Get log level distribution
        /// </summary>
        public async Task<Dictionary<string, int>> GetLogLevelDistributionAsync(IEnumerable<LogEntry> logEntries)
        {
            return await Task.FromResult(logEntries
                .GroupBy(e => e.Level)
                .ToDictionary(g => g.Key, g => g.Count()));
        }

        /// <summary>
        /// Calculate average processing time (for performance logs)
        /// </summary>
        public async Task<double> CalculateAverageProcessingTimeAsync(IEnumerable<LogEntry> logEntries)
        {
            var processingTimes = logEntries
                .Where(e => !string.IsNullOrEmpty(e.RawData) && e.RawData.Contains("processing_time"))
                .Select(e => TryExtractProcessingTime(e.RawData))
                .Where(time => time.HasValue)
                .Select(time => time.Value)
                .ToList();

            return await Task.FromResult(processingTimes.Any() ? processingTimes.Average() : 0.0);
        }

        /// <summary>
        /// Get top error messages by frequency
        /// </summary>
        public async Task<Dictionary<string, int>> GetTopErrorsAsync(IEnumerable<LogEntry> logEntries, int topCount = 10)
        {
            return await Task.FromResult(logEntries
                .Where(e => e.Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase))
                .GroupBy(e => e.Message)
                .OrderByDescending(g => g.Count())
                .Take(topCount)
                .ToDictionary(g => g.Key, g => g.Count()));
        }

        /// <summary>
        /// Export statistics to formatted string
        /// </summary>
        public string ExportStatistics(LogStatistics statistics, string format = "JSON")
        {
            try
            {
                switch (format.ToUpperInvariant())
                {
                    case "JSON":
                        return System.Text.Json.JsonSerializer.Serialize(statistics, new System.Text.Json.JsonSerializerOptions 
                        { 
                            WriteIndented = true 
                        });
                        
                    case "CSV":
                        var csv = new System.Text.StringBuilder();
                        csv.AppendLine("Metric,Value");
                        csv.AppendLine($"Total Entries,{statistics.TotalEntries}");
                        csv.AppendLine($"Error Entries,{statistics.ErrorEntries}");
                        csv.AppendLine($"Warning Entries,{statistics.WarningEntries}");
                        csv.AppendLine($"Info Entries,{statistics.InfoEntries}");
                        csv.AppendLine($"Error Percentage,{statistics.ErrorPercentage:F2}%");
                        csv.AppendLine($"Time Span,{statistics.TimeSpan}");
                        return csv.ToString();
                        
                    case "TEXT":
                    case "TXT":
                        var txt = new System.Text.StringBuilder();
                        txt.AppendLine("=== LOG STATISTICS REPORT ===");
                        txt.AppendLine($"Total Entries: {statistics.TotalEntries:N0}");
                        txt.AppendLine($"Error Entries: {statistics.ErrorEntries:N0}");
                        txt.AppendLine($"Warning Entries: {statistics.WarningEntries:N0}");
                        txt.AppendLine($"Info Entries: {statistics.InfoEntries:N0}");
                        txt.AppendLine($"Error Percentage: {statistics.ErrorPercentage:F2}%");
                        txt.AppendLine($"Time Span: {statistics.TimeSpan}");
                        return txt.ToString();
                        
                    default:
                        return System.Text.Json.JsonSerializer.Serialize(statistics, new System.Text.Json.JsonSerializerOptions 
                        { 
                            WriteIndented = true 
                        });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting statistics to format: {Format}", format);
                return $"Error exporting statistics: {ex.Message}";
            }
        }

        private double? TryExtractProcessingTime(string additionalData)
        {
            try
            {
                // Try to extract processing time from additional data
                // Expected format: processing_time=123.45
                if (string.IsNullOrEmpty(additionalData))
                    return null;

                var match = System.Text.RegularExpressions.Regex.Match(additionalData, @"processing_time[=:]\s*(\d+\.?\d*)");
                if (match.Success && double.TryParse(match.Groups[1].Value, out var time))
                {
                    return time;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Statistics for a specific log level
    /// </summary>
    public class LevelStatistics
    {
        public string Level { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
        public Dictionary<int, int> HourlyDistribution { get; set; } = new();
        public Dictionary<DateTime, int> DailyDistribution { get; set; } = new();
        public Dictionary<string, int> SourceDistribution { get; set; } = new();
        public Dictionary<string, int> MessagePatterns { get; set; } = new();
        public double AveragePerHour { get; set; }
        public int PeakHour { get; set; }
        public DateTime PeakDay { get; set; }
    }

    /// <summary>
    /// Time-based statistics with different grouping options
    /// </summary>
    public class TimeBasedStatistics
    {
        public TimeGrouping Grouping { get; set; }
        public Dictionary<DateTime, int> TimeDistribution { get; set; } = new();
        public Dictionary<DateTime, Dictionary<string, int>> LevelDistribution { get; set; } = new();
        public DateTime PeakTime { get; set; }
        public int PeakCount { get; set; }
        public double AveragePerPeriod { get; set; }
        public int TotalPeriods { get; set; }
    }

    /// <summary>
    /// Time grouping options for statistics
    /// </summary>
    public enum TimeGrouping
    {
        Hourly,
        Daily,
        Weekly
    }

    #endregion
} 
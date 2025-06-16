using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Services.Dashboard
{
    /// <summary>
    /// Dashboard strategy for overview and general statistics
    /// </summary>
    public class OverviewDashboardStrategy : BaseDashboardStrategy
    {
        public override DashboardType DashboardType => DashboardType.Overview;
        public override string DisplayName => "Overview";
        public override string Description => "General log analysis and key metrics overview";
        public override string IconKey => "BarChartIcon";

        public override bool CanHandle(DashboardContext context)
        {
            return true; // Can always handle overview
        }

        public override int GetPriority(DashboardContext context)
        {
            // Default priority - good fallback option
            return 5;
        }

        public override async Task<DashboardData> LoadDashboardDataAsync(IReadOnlyList<LogEntry> logEntries)
        {
            UpdateLogEntries(logEntries);

            var dashboardData = new DashboardData
            {
                Title = DisplayName,
                Subtitle = "Comprehensive log analysis overview",
                LastUpdated = DateTime.UtcNow,
                IsLoading = false
            };

            // Add overview metrics
            dashboardData.Metrics = await CreateOverviewMetricsAsync();

            // Add charts
            dashboardData.Charts = await CreateOverviewChartsAsync();

            // Add summary tables
            dashboardData.Tables = await CreateOverviewTablesAsync();

            return dashboardData;
        }

        public override IReadOnlyList<ChartConfiguration> GetChartConfigurations()
        {
            return new List<ChartConfiguration>
            {
                new ChartConfiguration
                {
                    Id = "log-levels-pie",
                    Title = "Log Levels Distribution",
                    Type = ChartType.Pie,
                    Height = 300,
                    Options = new Dictionary<string, object>
                    {
                        { "responsive", true },
                        { "maintainAspectRatio", false },
                        { "plugins", new { legend = new { position = "right" } } }
                    }
                },
                new ChartConfiguration
                {
                    Id = "timeline-bar",
                    Title = "Log Entries Timeline",
                    Type = ChartType.Bar,
                    Height = 350,
                    Options = new Dictionary<string, object>
                    {
                        { "responsive", true },
                        { "maintainAspectRatio", false },
                        { "scales", new { y = new { beginAtZero = true } } }
                    }
                },
                new ChartConfiguration
                {
                    Id = "source-distribution",
                    Title = "Log Sources",
                    Type = ChartType.Doughnut,
                    Height = 280,
                    Options = new Dictionary<string, object>
                    {
                        { "responsive", true },
                        { "cutout", "50%" }
                    }
                }
            };
        }

        public override IReadOnlyList<DashboardMetric> GetMetrics()
        {
            var basicMetrics = CreateBasicMetrics(_logEntries);
            var overviewMetrics = CreateOverviewSpecificMetrics();
            
            return basicMetrics.Concat(overviewMetrics).ToList();
        }

        private Task<IList<DashboardMetric>> CreateOverviewMetricsAsync()
        {
            var metrics = new List<DashboardMetric>();

            // Add basic log metrics
            var basicMetrics = CreateBasicMetrics(_logEntries);
            metrics.AddRange(basicMetrics);

            // Add overview-specific metrics
            if (_logEntries?.Any() == true)
            {
                // Time range
                var sortedEntries = _logEntries.OrderBy(e => e.Timestamp).ToList();

                if (sortedEntries.Any())
                {
                    var timeSpan = sortedEntries.Last().Timestamp - sortedEntries.First().Timestamp;
                    metrics.Add(new DashboardMetric
                    {
                        Name = "Time Range",
                        Value = timeSpan,
                        DisplayValue = FormatTimeSpan(timeSpan),
                        Unit = "duration",
                        Type = MetricType.Info,
                        IconKey = "ClockIcon"
                    });
                }

                // Unique sources
                var uniqueSources = _logEntries.Where(e => !string.IsNullOrEmpty(e.Source))
                                              .Select(e => e.Source)
                                              .Distinct()
                                              .Count();

                metrics.Add(new DashboardMetric
                {
                    Name = "Unique Sources",
                    Value = uniqueSources,
                    DisplayValue = uniqueSources.ToString("N0"),
                    Unit = "sources",
                    Type = MetricType.Info,
                    IconKey = "LayersIcon"
                });

                // Error rate
                var errorCount = _logEntries.Count(e => e.Level?.ToLowerInvariant() == "error");
                var errorRate = _logEntries.Count > 0 ? (errorCount / (double)_logEntries.Count) * 100 : 0;

                metrics.Add(new DashboardMetric
                {
                    Name = "Error Rate",
                    Value = errorRate,
                    DisplayValue = $"{errorRate:F2}%",
                    Unit = "percentage",
                    Type = errorRate > 10 ? MetricType.Error : errorRate > 5 ? MetricType.Warning : MetricType.Success,
                    IconKey = "AlertCircleIcon"
                });
            }

            return Task.FromResult<IList<DashboardMetric>>(metrics);
        }

        private Task<IList<ChartData>> CreateOverviewChartsAsync()
        {
            var charts = new List<ChartData>();

            // Log levels pie chart
            var levelCounts = _logEntries
                .GroupBy(e => e.Level ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());

            var logLevelsChart = new ChartData
            {
                ConfigurationId = "log-levels-pie",
                Labels = levelCounts.Keys.ToList(),
                Series = new List<ChartSeries>
                {
                    new ChartSeries
                    {
                        Name = "Log Levels",
                        Data = levelCounts.Values.Cast<object>().ToList(),
                        Color = GetLevelColor("Info")
                    }
                }
            };
            charts.Add(logLevelsChart);

            // Timeline bar chart (entries per hour)
            var timelineData = CreateTimelineData();
            if (timelineData.Any())
            {
                var timelineChart = new ChartData
                {
                    ConfigurationId = "timeline-bar",
                    Labels = timelineData.Keys.ToList(),
                    Series = new List<ChartSeries>
                    {
                        new ChartSeries
                        {
                            Name = "Entries",
                            Data = timelineData.Values.Cast<object>().ToList(),
                            Color = GetLevelColor("Info")
                        }
                    }
                };
                charts.Add(timelineChart);
            }

            // Source distribution chart
            var sourceCounts = _logEntries
                .Where(e => !string.IsNullOrEmpty(e.Source))
                .GroupBy(e => e.Source ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());

            if (sourceCounts.Any())
            {
                var sourceChart = new ChartData
                {
                    ConfigurationId = "source-distribution",
                    Labels = sourceCounts.Keys.ToList(),
                    Series = new List<ChartSeries>
                    {
                        new ChartSeries
                        {
                            Name = "Sources",
                            Data = sourceCounts.Values.Cast<object>().ToList(),
                            Color = GetLevelColor("Performance")
                        }
                    }
                };
                charts.Add(sourceChart);
            }

            return Task.FromResult<IList<ChartData>>(charts);
        }

        private Task<IList<DataTable>> CreateOverviewTablesAsync()
        {
            var tables = new List<DataTable>();

            // Recent errors table
            var recentErrors = _logEntries
                .Where(e => e.Level?.ToLowerInvariant() == "error")
                .OrderByDescending(e => e.Timestamp)
                .Take(10)
                .ToList();

            if (recentErrors.Any())
            {
                var errorsTable = new DataTable
                {
                    Title = "Recent Errors",
                    Columns = new List<DataColumn>
                    {
                        new DataColumn { Key = "timestamp", Name = "Time", DataType = typeof(string), Width = "20%" },
                        new DataColumn { Key = "source", Name = "Source", DataType = typeof(string), Width = "15%" },
                        new DataColumn { Key = "message", Name = "Message", DataType = typeof(string), Width = "50%" },
                        new DataColumn { Key = "logger", Name = "Logger", DataType = typeof(string), Width = "15%" }
                    },
                    Rows = new List<Dictionary<string, object>>()
                };

                foreach (var error in recentErrors)
                {
                    errorsTable.Rows.Add(new Dictionary<string, object>
                    {
                        { "timestamp", error.Timestamp.ToString("HH:mm:ss") },
                        { "source", error.Source ?? "Unknown" },
                        { "message", TruncateMessage(error.Message ?? "No message", 80) },
                        { "logger", "N/A" }
                    });
                }

                tables.Add(errorsTable);
            }

            return Task.FromResult<IList<DataTable>>(tables);
        }

        private IList<DashboardMetric> CreateOverviewSpecificMetrics()
        {
            var metrics = new List<DashboardMetric>();

            // Processing rate (entries per minute)
            if (_logEntries?.Any() == true && _context != null)
            {
                var sortedEntries = _logEntries.OrderBy(e => e.Timestamp).ToList();

                if (sortedEntries.Count > 1)
                {
                    var timeSpan = sortedEntries.Last().Timestamp - sortedEntries.First().Timestamp;
                    var entriesPerMinute = timeSpan.TotalMinutes > 0 ? sortedEntries.Count / timeSpan.TotalMinutes : 0;

                    metrics.Add(new DashboardMetric
                    {
                        Name = "Processing Rate",
                        Value = entriesPerMinute,
                        DisplayValue = $"{entriesPerMinute:F1}/min",
                        Unit = "entries/min",
                        Type = MetricType.Performance,
                        IconKey = "ActivityIcon"
                    });
                }
            }

            return metrics;
        }

        private Dictionary<string, int> CreateTimelineData()
        {
            var timelineData = new Dictionary<string, int>();

            if (!_logEntries.Any()) return timelineData;

            var grouped = _logEntries
                .GroupBy(e => e.Timestamp.ToString("HH:mm"))
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                timelineData[group.Key] = group.Count();
            }

            return timelineData;
        }

        private static string GetLevelColor(string level)
        {
            return level?.ToLowerInvariant() switch
            {
                "error" => "#e74c3c",
                "warning" or "warn" => "#f39c12",
                "info" or "information" => "#3498db",
                "debug" => "#95a5a6",
                "performance" => "#9b59b6",
                _ => "#34495e"
            };
        }

        private static string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalDays >= 1)
                return $"{timeSpan.TotalDays:F1} days";
            if (timeSpan.TotalHours >= 1)
                return $"{timeSpan.TotalHours:F1} hours";
            if (timeSpan.TotalMinutes >= 1)
                return $"{timeSpan.TotalMinutes:F1} minutes";
            return $"{timeSpan.TotalSeconds:F1} seconds";
        }

        private static string TruncateMessage(string message, int maxLength)
        {
            if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
                return message ?? string.Empty;

            return message.Substring(0, maxLength - 3) + "...";
        }
    }
} 
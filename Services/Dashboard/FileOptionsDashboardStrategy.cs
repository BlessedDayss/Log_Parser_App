using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Services.Dashboard
{
    /// <summary>
    /// Dashboard strategy for file options and statistics
    /// </summary>
    public class FileOptionsDashboardStrategy : BaseDashboardStrategy
    {
        public override DashboardType DashboardType => DashboardType.FileOptions;
        public override string DisplayName => "File Options";
        public override string Description => "File statistics, format analysis, and loading options";
        public override string IconKey => "FolderIcon";

        public override bool CanHandle(DashboardContext context)
        {
            return context?.LoadedFiles?.Any() == true;
        }

        public override int GetPriority(DashboardContext context)
        {
            // High priority when files are loaded but no specific analysis is needed
            if (context?.LoadedFiles?.Any() == true && context.ErrorCount == 0)
                return 10;
            
            return base.GetPriority(context ?? new DashboardContext());
        }

        public override async Task<DashboardData> LoadDashboardDataAsync(IReadOnlyList<LogEntry> logEntries)
        {
            UpdateLogEntries(logEntries);

            var dashboardData = new DashboardData
            {
                Title = DisplayName,
                Subtitle = "File analysis and loading statistics",
                LastUpdated = DateTime.UtcNow,
                IsLoading = false
            };

            // Add file metrics
            dashboardData.Metrics = await CreateFileMetricsAsync();

            // Add charts
            dashboardData.Charts = await CreateFileChartsAsync();

            // Add file information table
            dashboardData.Tables = await CreateFileTablesAsync();

            return dashboardData;
        }

        public override IReadOnlyList<ChartConfiguration> GetChartConfigurations()
        {
            return new List<ChartConfiguration>
            {
                new ChartConfiguration
                {
                    Id = "file-size-chart",
                    Title = "File Size Distribution",
                    Type = ChartType.Pie,
                    Height = 300,
                    Options = new Dictionary<string, object>
                    {
                        { "responsive", true },
                        { "maintainAspectRatio", false }
                    }
                },
                new ChartConfiguration
                {
                    Id = "log-level-distribution",
                    Title = "Log Level Distribution",
                    Type = ChartType.Doughnut,
                    Height = 250,
                    Options = new Dictionary<string, object>
                    {
                        { "responsive", true },
                        { "cutout", "60%" }
                    }
                },
                new ChartConfiguration
                {
                    Id = "entries-timeline",
                    Title = "Entries Over Time",
                    Type = ChartType.Line,
                    Height = 300,
                    Options = new Dictionary<string, object>
                    {
                        { "responsive", true },
                        { "interaction", new { intersect = false } }
                    }
                }
            };
        }

        public override IReadOnlyList<DashboardMetric> GetMetrics()
        {
            var basicMetrics = CreateBasicMetrics(_logEntries);
            var fileMetrics = CreateFileSpecificMetrics();
            
            return basicMetrics.Concat(fileMetrics).ToList();
        }

        private Task<IList<DashboardMetric>> CreateFileMetricsAsync()
        {
            var metrics = new List<DashboardMetric>();

            // Add basic log metrics
            var basicMetrics = CreateBasicMetrics(_logEntries);
            metrics.AddRange(basicMetrics);

            // Add file-specific metrics
            if (_context?.LoadedFiles != null)
            {
                var totalFiles = _context.LoadedFiles.Count;
                var totalSizeBytes = 0L;

                foreach (var filePath in _context.LoadedFiles)
                {
                    if (File.Exists(filePath))
                    {
                        var fileInfo = new FileInfo(filePath);
                        totalSizeBytes += fileInfo.Length;
                    }
                }

                metrics.Add(new DashboardMetric
                {
                    Name = "Loaded Files",
                    Value = totalFiles,
                    DisplayValue = totalFiles.ToString("N0"),
                    Unit = "files",
                    Type = MetricType.Info,
                    IconKey = "FileIcon"
                });

                metrics.Add(new DashboardMetric
                {
                    Name = "Total Size",
                    Value = totalSizeBytes,
                    DisplayValue = FormatFileSize(totalSizeBytes),
                    Unit = "bytes",
                    Type = MetricType.Info,
                    IconKey = "HardDriveIcon"
                });

                // Average entries per file
                var avgEntriesPerFile = totalFiles > 0 ? _logEntries.Count / (double)totalFiles : 0;
                metrics.Add(new DashboardMetric
                {
                    Name = "Avg Entries/File",
                    Value = avgEntriesPerFile,
                    DisplayValue = avgEntriesPerFile.ToString("N1"),
                    Unit = "entries",
                    Type = MetricType.Performance,
                    IconKey = "TrendingUpIcon"
                });
            }

            return Task.FromResult<IList<DashboardMetric>>(metrics);
        }

        private Task<IList<ChartData>> CreateFileChartsAsync()
        {
            var charts = new List<ChartData>();

            // File size distribution chart
            if (_context?.LoadedFiles != null)
            {
                var fileSizeData = new ChartData
                {
                    ConfigurationId = "file-size-chart",
                    Labels = new List<string>(),
                    Series = new List<ChartSeries>()
                };

                var sizeBuckets = new Dictionary<string, long>();
                foreach (var filePath in _context.LoadedFiles)
                {
                    if (File.Exists(filePath))
                    {
                        var fileInfo = new FileInfo(filePath);
                        var sizeCategory = GetFileSizeCategory(fileInfo.Length);
                        sizeBuckets[sizeCategory] = sizeBuckets.GetValueOrDefault(sizeCategory, 0) + 1;
                    }
                }

                fileSizeData.Labels = sizeBuckets.Keys.ToList();
                fileSizeData.Series.Add(new ChartSeries
                {
                    Name = "Files",
                    Data = sizeBuckets.Values.Cast<object>().ToList(),
                    Color = "#3498db"
                });

                charts.Add(fileSizeData);
            }

            // Log level distribution chart
            var logLevelData = new ChartData
            {
                ConfigurationId = "log-level-distribution",
                Labels = new List<string>(),
                Series = new List<ChartSeries>()
            };

            var levelCounts = _logEntries
                .GroupBy(e => e.Level ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());

            logLevelData.Labels = levelCounts.Keys.ToList();
            logLevelData.Series.Add(new ChartSeries
            {
                Name = "Log Levels",
                Data = levelCounts.Values.Cast<object>().ToList(),
                Color = "#e74c3c"
            });

            charts.Add(logLevelData);

            return Task.FromResult<IList<ChartData>>(charts);
        }

        private Task<IList<DataTable>> CreateFileTablesAsync()
        {
            var tables = new List<DataTable>();

            if (_context?.LoadedFiles != null)
            {
                var fileTable = new DataTable
                {
                    Title = "Loaded Files Information",
                    Columns = new List<DataColumn>
                    {
                        new DataColumn { Key = "filename", Name = "File Name", DataType = typeof(string), Width = "30%" },
                        new DataColumn { Key = "size", Name = "Size", DataType = typeof(string), Width = "15%" },
                        new DataColumn { Key = "entries", Name = "Entries", DataType = typeof(int), Width = "15%" },
                        new DataColumn { Key = "errors", Name = "Errors", DataType = typeof(int), Width = "15%" },
                        new DataColumn { Key = "modified", Name = "Last Modified", DataType = typeof(string), Width = "25%" }
                    },
                    Rows = new List<Dictionary<string, object>>()
                };

                foreach (var filePath in _context.LoadedFiles)
                {
                    if (File.Exists(filePath))
                    {
                        var fileInfo = new FileInfo(filePath);
                        var entriesFromFile = _logEntries.Count(e => e.Source?.Contains(fileInfo.Name) == true);
                        var errorsFromFile = _logEntries.Count(e => e.Source?.Contains(fileInfo.Name) == true && 
                                                                     e.Level?.ToLowerInvariant() == "error");

                        fileTable.Rows.Add(new Dictionary<string, object>
                        {
                            { "filename", fileInfo.Name },
                            { "size", FormatFileSize(fileInfo.Length) },
                            { "entries", entriesFromFile },
                            { "errors", errorsFromFile },
                            { "modified", fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm") }
                        });
                    }
                }

                tables.Add(fileTable);
            }

            return Task.FromResult<IList<DataTable>>(tables);
        }

        private IList<DashboardMetric> CreateFileSpecificMetrics()
        {
            var metrics = new List<DashboardMetric>();

            // File format analysis
            if (_context?.LoadedFiles != null)
            {
                var formats = _context.LoadedFiles
                    .Select(f => Path.GetExtension(f)?.ToLowerInvariant() ?? "unknown")
                    .GroupBy(ext => ext)
                    .ToDictionary(g => g.Key, g => g.Count());

                var mostCommonFormat = formats.OrderByDescending(kvp => kvp.Value).FirstOrDefault();
                
                metrics.Add(new DashboardMetric
                {
                    Name = "Primary Format",
                    Value = mostCommonFormat.Key,
                    DisplayValue = mostCommonFormat.Key.ToUpperInvariant(),
                    Unit = "format",
                    Type = MetricType.Info,
                    IconKey = "FileTextIcon"
                });
            }

            return metrics;
        }

        private static string GetFileSizeCategory(long bytes)
        {
            return bytes switch
            {
                < 1024 => "< 1 KB",
                < 1024 * 1024 => "< 1 MB",
                < 10 * 1024 * 1024 => "< 10 MB",
                < 100 * 1024 * 1024 => "< 100 MB",
                _ => "> 100 MB"
            };
        }

        private static string FormatFileSize(long bytes)
        {
            return bytes switch
            {
                < 1024 => $"{bytes} B",
                < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
                < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
                _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB"
            };
        }
    }
} 
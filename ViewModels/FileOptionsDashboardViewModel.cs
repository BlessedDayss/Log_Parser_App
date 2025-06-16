using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Log_Parser_App.Models;
using Log_Parser_App.Services.Dashboard;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.ViewModels
{
    /// <summary>
    /// ViewModel for File Options Dashboard - provides file-based dashboard options and management
    /// </summary>
    public partial class FileOptionsDashboardViewModel : ViewModelBase
    {
        private readonly ILogger<FileOptionsDashboardViewModel> _logger;
        private readonly IDashboardTypeService _dashboardTypeService;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private DashboardData? _dashboardData;

        [ObservableProperty]
        private FileOptionType _selectedFileOption = FileOptionType.Overview;

        [ObservableProperty]
        private int _totalFilesLoaded;

        [ObservableProperty]
        private long _totalFileSize;

        [ObservableProperty]
        private string _formattedFileSize = "0 B";

        [ObservableProperty]
        private DateTime? _oldestFileDate;

        [ObservableProperty]
        private DateTime? _newestFileDate;

        [ObservableProperty]
        private string _fileTypeDistribution = string.Empty;

        public ObservableCollection<FileOptionItem> FileOptions { get; } = new();
        public ObservableCollection<FileMetric> FileMetrics { get; } = new();
        public ObservableCollection<FileAction> AvailableActions { get; } = new();

        public FileOptionsDashboardViewModel(
            ILogger<FileOptionsDashboardViewModel> logger,
            IDashboardTypeService dashboardTypeService)
        {
            _logger = logger;
            _dashboardTypeService = dashboardTypeService;

            InitializeFileOptions();
            InitializeFileActions();
        }

        /// <summary>
        /// Initializes the file options available in the dashboard
        /// </summary>
        private void InitializeFileOptions()
        {
            FileOptions.Clear();

            FileOptions.Add(new FileOptionItem
            {
                Type = FileOptionType.Overview,
                Title = "File Overview",
                Description = "General information about loaded files",
                IconKey = "FileMultiple",
                IsEnabled = true
            });

            FileOptions.Add(new FileOptionItem
            {
                Type = FileOptionType.LogTypeAnalysis,
                Title = "Log Type Analysis",
                Description = "Analyze and categorize different log file types",
                IconKey = "FileChart",
                IsEnabled = true
            });

            FileOptions.Add(new FileOptionItem
            {
                Type = FileOptionType.FileComparison,
                Title = "File Comparison",
                Description = "Compare metrics across multiple log files",
                IconKey = "FileCompare",
                IsEnabled = true
            });

            FileOptions.Add(new FileOptionItem
            {
                Type = FileOptionType.FilePerformance,
                Title = "File Performance",
                Description = "Analyze file parsing and processing performance",
                IconKey = "FileSpeed",
                IsEnabled = true
            });

            FileOptions.Add(new FileOptionItem
            {
                Type = FileOptionType.FileHealth,
                Title = "File Health Check",
                Description = "Check file integrity and parsing success rates",
                IconKey = "FileCheck",
                IsEnabled = true
            });

            FileOptions.Add(new FileOptionItem
            {
                Type = FileOptionType.BatchOperations,
                Title = "Batch Operations",
                Description = "Perform operations on multiple files simultaneously",
                IconKey = "FileBatch",
                IsEnabled = true
            });
        }

        /// <summary>
        /// Initializes available file actions
        /// </summary>
        private void InitializeFileActions()
        {
            AvailableActions.Clear();

            AvailableActions.Add(new FileAction
            {
                Id = "reload_all",
                Title = "Reload All Files",
                Description = "Reload all currently loaded files",
                IconKey = "Refresh",
                IsEnabled = true
            });

            AvailableActions.Add(new FileAction
            {
                Id = "export_summary",
                Title = "Export File Summary",
                Description = "Export file analysis summary to CSV",
                IconKey = "Export",
                IsEnabled = true
            });

            AvailableActions.Add(new FileAction
            {
                Id = "clear_cache",
                Title = "Clear File Cache",
                Description = "Clear cached file parsing results",
                IconKey = "Delete",
                IsEnabled = true
            });

            AvailableActions.Add(new FileAction
            {
                Id = "optimize_memory",
                Title = "Optimize Memory",
                Description = "Free up memory by optimizing file data storage",
                IconKey = "Memory",
                IsEnabled = true
            });
        }

        /// <summary>
        /// Loads dashboard data for the current file option
        /// </summary>
        [RelayCommand]
        private async Task LoadDashboardData()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading file options dashboard...";

                if (_dashboardTypeService?.CurrentStrategy == null)
                {
                    StatusMessage = "Dashboard service not available";
                    return;
                }

                // Get dashboard data from the current strategy
                var dashboardData = await _dashboardTypeService.CurrentStrategy.RefreshDataAsync();
                DashboardData = dashboardData;

                // Update file metrics
                await UpdateFileMetricsAsync();

                StatusMessage = "File options dashboard loaded successfully";
                _logger.LogInformation("File options dashboard data loaded for option: {FileOption}", SelectedFileOption);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading file options dashboard data");
                StatusMessage = $"Error loading dashboard: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Changes the selected file option
        /// </summary>
        [RelayCommand]
        private async Task ChangeFileOption(FileOptionType optionType)
        {
            try
            {
                SelectedFileOption = optionType;
                StatusMessage = $"Switched to {GetFileOptionDisplayName(optionType)}";

                // Reload dashboard data for the new option
                await LoadDashboardData();

                _logger.LogInformation("File option changed to: {FileOption}", optionType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing file option to {FileOption}", optionType);
                StatusMessage = $"Error changing file option: {ex.Message}";
            }
        }

        /// <summary>
        /// Executes a file action
        /// </summary>
        [RelayCommand]
        private async Task ExecuteFileAction(string actionId)
        {
            try
            {
                var action = AvailableActions.FirstOrDefault(a => a.Id == actionId);
                if (action == null)
                {
                    StatusMessage = "Action not found";
                    return;
                }

                if (!action.IsEnabled)
                {
                    StatusMessage = "Action is currently disabled";
                    return;
                }

                IsLoading = true;
                StatusMessage = $"Executing {action.Title}...";

                await ExecuteActionAsync(actionId);

                StatusMessage = $"{action.Title} completed successfully";
                _logger.LogInformation("File action executed: {ActionId}", actionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing file action: {ActionId}", actionId);
                StatusMessage = $"Error executing action: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Updates file metrics based on current data
        /// </summary>
        private async Task UpdateFileMetricsAsync()
        {
            try
            {
                FileMetrics.Clear();

                // Add basic file metrics
                FileMetrics.Add(new FileMetric
                {
                    Name = "Total Files",
                    Value = TotalFilesLoaded,
                    DisplayValue = TotalFilesLoaded.ToString("N0"),
                    Unit = "files",
                    IconKey = "File",
                    Type = MetricType.Info
                });

                FileMetrics.Add(new FileMetric
                {
                    Name = "Total Size",
                    Value = TotalFileSize,
                    DisplayValue = FormattedFileSize,
                    Unit = "",
                    IconKey = "HardDrive",
                    Type = MetricType.Info
                });

                if (OldestFileDate.HasValue)
                {
                    FileMetrics.Add(new FileMetric
                    {
                        Name = "Date Range",
                        Value = $"{OldestFileDate:yyyy-MM-dd} to {NewestFileDate:yyyy-MM-dd}",
                        DisplayValue = $"{(NewestFileDate - OldestFileDate)?.Days ?? 0} days",
                        Unit = "span",
                        IconKey = "Calendar",
                        Type = MetricType.Info
                    });
                }

                // Add performance metrics if available
                if (DashboardData?.Metrics != null)
                {
                    foreach (var metric in DashboardData.Metrics.Take(5)) // Limit to top 5 metrics
                    {
                        FileMetrics.Add(new FileMetric
                        {
                            Name = metric.Name,
                            Value = metric.Value,
                            DisplayValue = metric.DisplayValue,
                            Unit = metric.Unit,
                            IconKey = metric.IconKey,
                            Type = metric.Type
                        });
                    }
                }

                await Task.CompletedTask; // Placeholder for async operations
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating file metrics");
            }
        }

        /// <summary>
        /// Executes the specified action
        /// </summary>
        private async Task ExecuteActionAsync(string actionId)
        {
            switch (actionId)
            {
                case "reload_all":
                    await ReloadAllFilesAsync();
                    break;
                case "export_summary":
                    await ExportFileSummaryAsync();
                    break;
                case "clear_cache":
                    await ClearFileCacheAsync();
                    break;
                case "optimize_memory":
                    await OptimizeMemoryAsync();
                    break;
                default:
                    throw new ArgumentException($"Unknown action: {actionId}");
            }
        }

        /// <summary>
        /// Reloads all currently loaded files
        /// </summary>
        private async Task ReloadAllFilesAsync()
        {
            // Implementation would trigger file reload through main application
            await Task.Delay(1000); // Simulate operation
            await LoadDashboardData();
        }

        /// <summary>
        /// Exports file summary to CSV
        /// </summary>
        private async Task ExportFileSummaryAsync()
        {
            // Implementation would export file analysis data
            await Task.Delay(500); // Simulate operation
        }

        /// <summary>
        /// Clears file parsing cache
        /// </summary>
        private async Task ClearFileCacheAsync()
        {
            // Implementation would clear file cache
            await Task.Delay(300); // Simulate operation
        }

        /// <summary>
        /// Optimizes memory usage
        /// </summary>
        private async Task OptimizeMemoryAsync()
        {
            // Implementation would optimize memory
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(200); // Simulate operation
        }

        /// <summary>
        /// Gets display name for file option type
        /// </summary>
        public string GetFileOptionDisplayName(FileOptionType optionType)
        {
            var option = FileOptions.FirstOrDefault(o => o.Type == optionType);
            return option?.Title ?? optionType.ToString();
        }

        /// <summary>
        /// Gets description for file option type
        /// </summary>
        public string GetFileOptionDescription(FileOptionType optionType)
        {
            var option = FileOptions.FirstOrDefault(o => o.Type == optionType);
            return option?.Description ?? string.Empty;
        }

        /// <summary>
        /// Checks if a file option is enabled
        /// </summary>
        public bool IsFileOptionEnabled(FileOptionType optionType)
        {
            var option = FileOptions.FirstOrDefault(o => o.Type == optionType);
            return option?.IsEnabled ?? false;
        }

        /// <summary>
        /// Updates file size formatting
        /// </summary>
        partial void OnTotalFileSizeChanged(long value)
        {
            FormattedFileSize = FormatFileSize(value);
        }

        /// <summary>
        /// Formats file size in human-readable format
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
    }

    /// <summary>
    /// File option types available in the dashboard
    /// </summary>
    public enum FileOptionType
    {
        Overview,
        LogTypeAnalysis,
        FileComparison,
        FilePerformance,
        FileHealth,
        BatchOperations
    }

    /// <summary>
    /// File option item model
    /// </summary>
    public class FileOptionItem
    {
        public FileOptionType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconKey { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// File metric model
    /// </summary>
    public class FileMetric
    {
        public string Name { get; set; } = string.Empty;
        public object Value { get; set; } = string.Empty;
        public string DisplayValue { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string IconKey { get; set; } = string.Empty;
        public MetricType Type { get; set; } = MetricType.Info;
    }

    /// <summary>
    /// File action model
    /// </summary>
    public class FileAction
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconKey { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
    }
} 
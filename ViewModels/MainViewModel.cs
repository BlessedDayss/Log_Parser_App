using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Log_Parser_App.Models;
using Log_Parser_App.Services;
using Microsoft.Extensions.Logging;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Defaults;
using SkiaSharp;
using System.Collections.Generic;

namespace Log_Parser_App.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly ILogParserService _logParserService;
        private readonly ILogger<MainViewModel> _logger;
        private readonly IFileService _fileService;
        private readonly IErrorRecommendationService _errorRecommendationService;

        [ObservableProperty]
        private string _statusMessage = "Ready to work";

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isDarkTheme;

        [ObservableProperty]
        private TabItem? _selectedTab;

        [ObservableProperty]
        private ObservableCollection<TabItem> _tabs = new();

        [ObservableProperty]
        private int _errorCount;

        [ObservableProperty]
        private int _warningCount;

        [ObservableProperty]
        private int _infoCount;

        [ObservableProperty]
        private int _otherCount;

        [ObservableProperty]
        private double _errorPercent;

        [ObservableProperty]
        private double _warningPercent;

        [ObservableProperty]
        private double _infoPercent;

        [ObservableProperty]
        private double _otherPercent;

        [ObservableProperty]
        private LogStatistics _logStatistics = new();

        [ObservableProperty]
        private ISeries[] _levelsOverTimeSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private ISeries[] _topErrorsSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private ISeries[] _logDistributionSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private ISeries[] _timeHeatmapSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private ISeries[] _errorTrendSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private ISeries[] _sourcesDistributionSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private Axis[] _timeAxis = { new Axis { Name = "Time", Labels = new List<string>() } };

        [ObservableProperty]
        private Axis[] _countAxis = { new Axis { Name = "Count", MinLimit = 0 } };

        [ObservableProperty]
        private Axis[] _hoursAxis = { new Axis { Name = "Hours", Labels = new List<string>() } };

        [ObservableProperty]
        private Axis[] _errorMessageAxis = { new Axis { Name = "Error Message", Labels = new List<string>() } };

        [ObservableProperty]
        private Axis[] _sourceAxis = { new Axis { Name = "Source", Labels = new List<string>() } };

        public System.Windows.Input.ICommand? ExternalOpenFileCommand { get; set; }

        private string? _lastOpenedFilePath;
        public string? LastOpenedFilePath
        {
            get => _lastOpenedFilePath;
            set => SetProperty(ref _lastOpenedFilePath, value);
        }

        public MainViewModel(
            ILogParserService logParserService,
            ILogger<MainViewModel> logger,
            IFileService fileService,
            IErrorRecommendationService errorRecommendationService)
        {
            _logParserService = logParserService;
            _logger = logger;
            _fileService = fileService;
            _errorRecommendationService = errorRecommendationService;

            InitializeErrorRecommendationService();
            _logger.LogInformation("MainViewModel initialized");
        }

        private async void InitializeErrorRecommendationService()
        {
            try
            {
                await _errorRecommendationService.InitializeAsync();
                _logger.LogInformation("Error recommendation service initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize error recommendation service");
            }
        }

        [RelayCommand]
        private async Task LoadFilesOrFolder()
        {
            try
            {
                var paths = await _fileService.PickFilesOrFolderAsync();
                if (!paths.Any())
                {
                    _logger.LogInformation("No files or folders selected");
                    return;
                }

                IsLoading = true;
                StatusMessage = "Loading selected files...";

                foreach (var path in paths)
                {
                    if (Directory.Exists(path))
                    {
                        // Handle folder - find all log files in the folder
                        var logFiles = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                            .Where(f => f.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ||
                                      f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                                      f.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));

                        foreach (var logFile in logFiles)
                        {
                            await LoadSingleFile(logFile);
                        }
                    }
                    else
                    {
                        // Handle single file
                        await LoadSingleFile(path);
                    }
                }

                if (Tabs.Count > 0 && SelectedTab == null)
                {
                    SelectedTab = Tabs[0];
                }

                StatusMessage = $"Loaded {Tabs.Count} files";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading files");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadSingleFile(string filePath)
        {
            try
            {
                LastOpenedFilePath = filePath;
                var fileName = Path.GetFileName(filePath);

                var tab = new TabItem
                {
                    Header = fileName,
                    FilePath = filePath,
                    StatusMessage = $"Loading {fileName}..."
                };

                await Task.Run(async () =>
                {
                    var entries = await _logParserService.ParseLogFileAsync(filePath);
                    var logEntries = entries.ToList();

                    foreach (var entry in logEntries)
                    {
                        if (!string.IsNullOrEmpty(entry.Message))
                        {
                            var lines = entry.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            var regex = new System.Text.RegularExpressions.Regex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}");
                            string mainLine = lines.FirstOrDefault(l => !l.TrimStart().StartsWith("at ")) ?? lines[0];
                            var stackLines = lines.SkipWhile(l => !l.TrimStart().StartsWith("at ")).Where(l => l.TrimStart().StartsWith("at ")).ToList();
                            entry.Message = mainLine.Trim();
                            entry.StackTrace = stackLines.Count > 0 ? string.Join("\n", stackLines) : null;
                        }

                        entry.OpenFileCommand = ExternalOpenFileCommand;

                        // Properly process errors using ErrorRecommendationService
                        if (entry.Level == "ERROR" || entry.Level == "FAULT" || entry.Level == "CRITICAL")
                        {
                            // Get recommendations from the service
                            var recommendation = await Task.Run(() => _errorRecommendationService.AnalyzeError(entry.Message));

                            if (recommendation != null)
                            {
                                // Apply the recommendation to the log entry
                                entry.ErrorType = recommendation.ErrorType;
                                entry.ErrorDescription = recommendation.Description;
                                entry.ErrorRecommendations.Clear();
                                entry.ErrorRecommendations.AddRange(recommendation.Recommendations);

                                _logger.LogDebug("Applied error recommendation of type {ErrorType} to log entry", recommendation.ErrorType);
                            }
                            else
                            {
                                // Default error information if no recommendation found
                                entry.ErrorType = "UnknownError";
                                entry.ErrorDescription = "Unknown error type. No specific recommendations available.";
                                entry.ErrorRecommendations.Clear();
                                entry.ErrorRecommendations.Add("Check the application documentation for this error message.");
                                entry.ErrorRecommendations.Add("Search the error message online for more information.");
                                entry.ErrorRecommendations.Add("Contact support if the issue persists.");

                                _logger.LogDebug("No error recommendation found for: {ErrorMessage}", entry.Message);
                            }
                        }
                    }

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        foreach (var entry in logEntries)
                        {
                            tab.LogEntries.Add(entry);
                            tab.FilteredLogEntries.Add(entry);
                            if (entry.Level == "ERROR" || entry.Level == "FAULT" || entry.Level == "CRITICAL")
                            {
                                tab.ErrorLogEntries.Add(entry);
                            }
                        }

                        UpdateTabStatistics(tab);
                        tab.StatusMessage = $"Loaded {tab.LogEntries.Count} entries";
                    });
                });

                Tabs.Add(tab);
                _logger.LogInformation("Added new tab for file: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading file: {FilePath}", filePath);
                throw;
            }
        }

        private void UpdateTabStatistics(TabItem tab)
        {
            if (tab.LogEntries.Count == 0)
            {
                ResetTabStatistics(tab);
                return;
            }

            _logger.LogDebug("Updating statistics for tab {TabHeader}", tab.Header);

            tab.ErrorCount = tab.LogEntries.Count(e => e.Level == "ERROR");
            tab.WarningCount = tab.LogEntries.Count(e => e.Level == "WARNING");
            tab.InfoCount = tab.LogEntries.Count(e => e.Level == "INFO");
            tab.OtherCount = tab.LogEntries.Count(e => e.Level != "ERROR" && e.Level != "WARNING" && e.Level != "INFO");

            var total = tab.LogEntries.Count;
            tab.ErrorPercent = total > 0 ? Math.Round((double)tab.ErrorCount / total * 100, 1) : 0;
            tab.WarningPercent = total > 0 ? Math.Round((double)tab.WarningCount / total * 100, 1) : 0;
            tab.InfoPercent = total > 0 ? Math.Round((double)tab.InfoCount / total * 100, 1) : 0;
            tab.OtherPercent = total > 0 ? Math.Round((double)tab.OtherCount / total * 100, 1) : 0;

            UpdateTabCharts(tab);

            tab.TabStatistics = new LogStatistics
            {
                TotalCount = total,
                ErrorCount = tab.ErrorCount,
                WarningCount = tab.WarningCount,
                InfoCount = tab.InfoCount,
                OtherCount = tab.OtherCount,
                ErrorPercent = tab.ErrorPercent,
                WarningPercent = tab.WarningPercent,
                InfoPercent = tab.InfoPercent,
                OtherPercent = tab.OtherPercent
            };
        }

        private void ResetTabStatistics(TabItem tab)
        {
            tab.ErrorCount = 0;
            tab.WarningCount = 0;
            tab.InfoCount = 0;
            tab.OtherCount = 0;
            tab.ErrorPercent = 0;
            tab.WarningPercent = 0;
            tab.InfoPercent = 0;
            tab.OtherPercent = 0;

            tab.LevelsOverTimeSeries = Array.Empty<ISeries>();
            tab.TopErrorsSeries = Array.Empty<ISeries>();
            tab.LogDistributionSeries = Array.Empty<ISeries>();
            tab.TimeHeatmapSeries = Array.Empty<ISeries>();
            tab.ErrorTrendSeries = Array.Empty<ISeries>();
            tab.SourcesDistributionSeries = Array.Empty<ISeries>();
        }

        private void UpdateTabCharts(TabItem tab)
        {
            try
            {
                // Update all charts for the tab
                // (Implementation of chart updating logic - similar to existing UpdateCharts but for tab)
                UpdateLogDistributionChart(tab);
                UpdateTimeSeriesCharts(tab);
                UpdateTopErrorsChart(tab);
                UpdateSourcesDistributionChart(tab);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating charts for tab {TabHeader}", tab.Header);
            }
        }

        private void UpdateLogDistributionChart(TabItem tab)
        {
            tab.LogDistributionSeries = tab.ErrorCount + tab.WarningCount + tab.InfoCount + tab.OtherCount > 0
                ? new ISeries[]
                {
                    new PieSeries<double>
                    {
                        Values = new double[] { tab.ErrorCount },
                        Name = "Errors",
                        Fill = new SolidColorPaint(SKColors.OrangeRed),
                        InnerRadius = 50,
                        MaxRadialColumnWidth = 20,
                        DataLabelsSize = 14,
                        DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue} ({Math.Round(point.Coordinate.PrimaryValue / (point.StackedValue?.Total ?? 1) * 100)}%)",
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer
                    },
                    new PieSeries<double>
                    {
                        Values = new double[] { tab.WarningCount },
                        Name = "Warnings",
                        Fill = new SolidColorPaint(SKColors.Orange),
                        InnerRadius = 50,
                        MaxRadialColumnWidth = 20,
                        DataLabelsSize = 14,
                        DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue} ({Math.Round(point.Coordinate.PrimaryValue / (point.StackedValue?.Total ?? 1) * 100)}%)",
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer
                    },
                    new PieSeries<double>
                    {
                        Values = new double[] { tab.InfoCount },
                        Name = "Information",
                        Fill = new SolidColorPaint(SKColors.DodgerBlue),
                        InnerRadius = 50,
                        MaxRadialColumnWidth = 20,
                        DataLabelsSize = 14,
                        DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue} ({Math.Round(point.Coordinate.PrimaryValue / (point.StackedValue?.Total ?? 1) * 100)}%)",
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer
                    }
                }
                : Array.Empty<ISeries>();
        }

        private void UpdateTimeSeriesCharts(TabItem tab)
        {
            var timeGroups = tab.LogEntries
                .GroupBy(e => e.Timestamp.Hour)
                .OrderBy(g => g.Key)
                .ToList();

            var allHours = Enumerable.Range(0, 24).ToList();
            var baseDate = DateTime.Today;

            var errorsByHour = new List<DateTimePoint>();
            var warningsByHour = new List<DateTimePoint>();
            var infosByHour = new List<DateTimePoint>();
            var totalByHour = new List<DateTimePoint>();

            foreach (var hour in allHours)
            {
                var hourTime = baseDate.AddHours(hour);
                var group = timeGroups.FirstOrDefault(g => g.Key == hour);

                int errorCount = group?.Count(e => e.Level == "ERROR") ?? 0;
                int warningCount = group?.Count(e => e.Level == "WARNING") ?? 0;
                int infoCount = group?.Count(e => e.Level == "INFO") ?? 0;
                int totalCount = group?.Count() ?? 0;

                errorsByHour.Add(new DateTimePoint(hourTime, errorCount));
                warningsByHour.Add(new DateTimePoint(hourTime, warningCount));
                infosByHour.Add(new DateTimePoint(hourTime, infoCount));
                totalByHour.Add(new DateTimePoint(hourTime, totalCount));
            }

            tab.LevelsOverTimeSeries = new ISeries[]
            {
                new LineSeries<DateTimePoint>
                {
                    Values = totalByHour,
                    Name = "All Logs",
                    Stroke = new SolidColorPaint(SKColors.Gray, 3),
                    Fill = new SolidColorPaint(SKColors.Gray.WithAlpha(40)),
                    GeometryFill = new SolidColorPaint(SKColors.White),
                    GeometryStroke = new SolidColorPaint(SKColors.Gray, 2),
                    LineSmoothness = 0.5
                },
                new LineSeries<DateTimePoint>
                {
                    Values = errorsByHour,
                    Name = "Errors",
                    Stroke = new SolidColorPaint(SKColors.OrangeRed, 3),
                    Fill = new SolidColorPaint(SKColors.OrangeRed.WithAlpha(40)),
                    GeometryFill = new SolidColorPaint(SKColors.White),
                    GeometryStroke = new SolidColorPaint(SKColors.OrangeRed, 2),
                    LineSmoothness = 0.5
                }
            };

            var timeHeatData = totalByHour.Select(p => Convert.ToDouble(p.Value)).DefaultIfEmpty(0).ToArray();
            tab.TimeHeatmapSeries = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = timeHeatData,
                    Name = "Activity",
                    Fill = new LinearGradientPaint(
                        new[] { new SKColor(100, 149, 237, 100), new SKColor(65, 105, 225) },
                        new SKPoint(0, 0),
                        new SKPoint(0, 1)
                    ),
                    Stroke = null,
                    MaxBarWidth = double.MaxValue
                }
            };
        }

        private void UpdateTopErrorsChart(TabItem tab)
        {
            var topErrors = tab.LogEntries
                .Where(e => e.Level == "ERROR")
                .GroupBy(e => e.Message)
                .Select(g => new { Message = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToList();

            if (topErrors.Any())
            {
                var values = topErrors.Select(e => (double)e.Count).ToArray();
                var labels = topErrors.Select(e => TruncateMessage(e.Message, 40)).ToList();

                tab.TopErrorsSeries = new ISeries[]
                {
                    new ColumnSeries<double>
                    {
                        Values = values,
                        Name = "Count",
                        Fill = new LinearGradientPaint(
                            new[] { new SKColor(255, 0, 0, 200), new SKColor(255, 99, 71, 220) },
                            new SKPoint(0, 0),
                            new SKPoint(0, 1)
                        ),
                        Stroke = new SolidColorPaint(SKColors.OrangeRed.WithAlpha(220), 2),
                        MaxBarWidth = 50,
                        DataLabelsSize = 12,
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                        DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue}"
                    }
                };
            }
            else
            {
                tab.TopErrorsSeries = Array.Empty<ISeries>();
            }
        }

        private void UpdateSourcesDistributionChart(TabItem tab)
        {
            var sourcesData = tab.LogEntries
                .GroupBy(e => e.Source)
                .Select(g => new { Source = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(8)
                .ToList();

            if (sourcesData.Any())
            {
                var sourceValues = new List<ISeries>();
                var colors = new[]
                {
                    SKColors.DodgerBlue, SKColors.MediumSeaGreen, SKColors.Orange,
                    SKColors.Purple, SKColors.OrangeRed, SKColors.Gold,
                    SKColors.MediumVioletRed, SKColors.LightSlateGray
                };

                foreach (var source in sourcesData)
                {
                    sourceValues.Add(new ColumnSeries<double>
                    {
                        Values = new[] { source.Count },
                        Name = source.Source,
                        Fill = new SolidColorPaint(colors[sourceValues.Count % colors.Length]),
                        MaxBarWidth = 35
                    });
                }

                tab.SourcesDistributionSeries = sourceValues.ToArray();
            }
            else
            {
                tab.SourcesDistributionSeries = Array.Empty<ISeries>();
            }
        }

        private static string TruncateMessage(string message, int maxLength)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;
            return message.Length <= maxLength ? message : message.Substring(0, maxLength) + "...";
        }

        [RelayCommand]
        private void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
            _logger.LogInformation("Theme changed to: {Theme}", IsDarkTheme ? "Dark" : "Light");
        }

        [RelayCommand]
        private void OpenLogFile(LogEntry? entry)
        {
            if (entry == null)
                return;

            var filePath = LastOpenedFilePath;
            if (string.IsNullOrEmpty(filePath))
            {
                StatusMessage = "File path is empty. Cannot open file.";
                return;
            }

            try
            {
                var opened = false;
                string? error = null;

                if (OperatingSystem.IsMacOS())
                {
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "/usr/bin/open",
                            Arguments = $"\"{filePath}\"",
                            UseShellExecute = false
                        };
                        var proc = System.Diagnostics.Process.Start(psi);
                        if (proc != null) opened = true;
                    }
                    catch (Exception ex)
                    {
                        error = $"Can't open: {ex.Message}\n{ex.StackTrace}";
                        _logger.LogError(ex, "open failed");
                    }
                }
                else if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true });
                        if (proc != null) opened = true;
                    }
                    catch (Exception ex)
                    {
                        error = $"Cannot open: {ex.Message}\n{ex.StackTrace}";
                        _logger.LogError(ex, "Windows open failed");
                    }
                }
                else if (OperatingSystem.IsLinux())
                {
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "xdg-open",
                            Arguments = $"\"{filePath}\"",
                            UseShellExecute = false
                        };
                        var proc = System.Diagnostics.Process.Start(psi);
                        if (proc != null) opened = true;
                    }
                    catch (Exception ex)
                    {
                        error = $"Cannot open file with xdg-open: {ex.Message}\n{ex.StackTrace}";
                        _logger.LogError(ex, "Linux open failed");
                    }
                }
                else
                {
                    error = "Unknown OS, cannot open file";
                }

                if (!opened)
                {
                    StatusMessage = error ?? "Failed to open file (unknown error)";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open file {FilePath}", filePath);
                StatusMessage = $"Error opening file: {ex.Message}\n{ex.StackTrace}";
            }
        }

        [RelayCommand]
        private void CloseTab(TabItem tab)
        {
            if (tab == null) return;

            var index = Tabs.IndexOf(tab);
            Tabs.Remove(tab);

            if (Tabs.Count == 0)
            {
                SelectedTab = null;
                StatusMessage = "No files open";
                ResetAllStatistics();
            }
            else
            {
                // Select the next available tab
                if (index >= Tabs.Count)
                {
                    index = Tabs.Count - 1;
                }
                SelectedTab = Tabs[index];
                UpdateGlobalStatistics();
            }

            _logger.LogInformation("Closed tab for file: {FilePath}", tab.FilePath);
        }

        private void ResetAllStatistics()
        {
            ErrorCount = 0;
            WarningCount = 0;
            InfoCount = 0;
            OtherCount = 0;
            ErrorPercent = 0;
            WarningPercent = 0;
            InfoPercent = 0;
            OtherPercent = 0;

            LogStatistics = new LogStatistics();
            LevelsOverTimeSeries = Array.Empty<ISeries>();
            TopErrorsSeries = Array.Empty<ISeries>();
            LogDistributionSeries = Array.Empty<ISeries>();
            TimeHeatmapSeries = Array.Empty<ISeries>();
            ErrorTrendSeries = Array.Empty<ISeries>();
            SourcesDistributionSeries = Array.Empty<ISeries>();
        }

        private void UpdateGlobalStatistics()
        {
            if (SelectedTab == null || Tabs.Count == 0)
            {
                ResetAllStatistics();
                return;
            }

            var totalEntries = Tabs.Sum(t => t.LogEntries.Count);
            ErrorCount = Tabs.Sum(t => t.ErrorCount);
            WarningCount = Tabs.Sum(t => t.WarningCount);
            InfoCount = Tabs.Sum(t => t.InfoCount);
            OtherCount = Tabs.Sum(t => t.OtherCount);

            ErrorPercent = totalEntries > 0 ? Math.Round((double)ErrorCount / totalEntries * 100, 1) : 0;
            WarningPercent = totalEntries > 0 ? Math.Round((double)WarningCount / totalEntries * 100, 1) : 0;
            InfoPercent = totalEntries > 0 ? Math.Round((double)InfoCount / totalEntries * 100, 1) : 0;
            OtherPercent = totalEntries > 0 ? Math.Round((double)OtherCount / totalEntries * 100, 1) : 0;

            LogStatistics = new LogStatistics
            {
                TotalCount = totalEntries,
                ErrorCount = ErrorCount,
                WarningCount = WarningCount,
                InfoCount = InfoCount,
                OtherCount = OtherCount,
                ErrorPercent = ErrorPercent,
                WarningPercent = WarningPercent,
                InfoPercent = InfoPercent,
                OtherPercent = OtherPercent
            };

            // Update charts based on combined data from all tabs
            UpdateGlobalCharts();
        }

        private void UpdateGlobalCharts()
        {
            try
            {
                UpdateLogDistributionChart();
                UpdateTimeSeriesCharts();
                UpdateTopErrorsChart();
                UpdateSourcesDistributionChart();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating global charts");
            }
        }

        private void UpdateLogDistributionChart()
        {
            LogDistributionSeries = ErrorCount + WarningCount + InfoCount + OtherCount > 0
                ? new ISeries[]
                {
                    new PieSeries<double>
                    {
                        Values = new double[] { ErrorCount },
                        Name = "Errors",
                        Fill = new SolidColorPaint(SKColors.OrangeRed),
                        InnerRadius = 50,
                        MaxRadialColumnWidth = 20,
                        DataLabelsSize = 14,
                        DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue} ({Math.Round(point.Coordinate.PrimaryValue / (point.StackedValue?.Total ?? 1) * 100)}%)",
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer
                    },
                    new PieSeries<double>
                    {
                        Values = new double[] { WarningCount },
                        Name = "Warnings",
                        Fill = new SolidColorPaint(SKColors.Orange),
                        InnerRadius = 50,
                        MaxRadialColumnWidth = 20,
                        DataLabelsSize = 14,
                        DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue} ({Math.Round(point.Coordinate.PrimaryValue / (point.StackedValue?.Total ?? 1) * 100)}%)",
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer
                    },
                    new PieSeries<double>
                    {
                        Values = new double[] { InfoCount },
                        Name = "Information",
                        Fill = new SolidColorPaint(SKColors.DodgerBlue),
                        InnerRadius = 50,
                        MaxRadialColumnWidth = 20,
                        DataLabelsSize = 14,
                        DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue} ({Math.Round(point.Coordinate.PrimaryValue / (point.StackedValue?.Total ?? 1) * 100)}%)",
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer
                    }
                }
                : Array.Empty<ISeries>();
        }

        private void UpdateTimeSeriesCharts()
        {
            var allEntries = Tabs.SelectMany(t => t.LogEntries)
                .GroupBy(e => e.Timestamp.Hour)
                .OrderBy(g => g.Key)
                .ToList();

            var allHours = Enumerable.Range(0, 24).ToList();
            var baseDate = DateTime.Today;

            var errorsByHour = new List<DateTimePoint>();
            var warningsByHour = new List<DateTimePoint>();
            var infosByHour = new List<DateTimePoint>();
            var totalByHour = new List<DateTimePoint>();

            foreach (var hour in allHours)
            {
                var hourTime = baseDate.AddHours(hour);
                var group = allEntries.FirstOrDefault(g => g.Key == hour);

                int errorCount = group?.Count(e => e.Level == "ERROR") ?? 0;
                int warningCount = group?.Count(e => e.Level == "WARNING") ?? 0;
                int infoCount = group?.Count(e => e.Level == "INFO") ?? 0;
                int totalCount = group?.Count() ?? 0;

                errorsByHour.Add(new DateTimePoint(hourTime, errorCount));
                warningsByHour.Add(new DateTimePoint(hourTime, warningCount));
                infosByHour.Add(new DateTimePoint(hourTime, infoCount));
                totalByHour.Add(new DateTimePoint(hourTime, totalCount));
            }

            LevelsOverTimeSeries = new ISeries[]
            {
                new LineSeries<DateTimePoint>
                {
                    Values = totalByHour,
                    Name = "All Logs",
                    Stroke = new SolidColorPaint(SKColors.Gray, 3),
                    Fill = new SolidColorPaint(SKColors.Gray.WithAlpha(40)),
                    GeometryFill = new SolidColorPaint(SKColors.White),
                    GeometryStroke = new SolidColorPaint(SKColors.Gray, 2),
                    LineSmoothness = 0.5
                },
                new LineSeries<DateTimePoint>
                {
                    Values = errorsByHour,
                    Name = "Errors",
                    Stroke = new SolidColorPaint(SKColors.OrangeRed, 3),
                    Fill = new SolidColorPaint(SKColors.OrangeRed.WithAlpha(40)),
                    GeometryFill = new SolidColorPaint(SKColors.White),
                    GeometryStroke = new SolidColorPaint(SKColors.OrangeRed, 2),
                    LineSmoothness = 0.5
                }
            };

            var timeHeatData = totalByHour.Select(p => Convert.ToDouble(p.Value)).DefaultIfEmpty(0).ToArray();
            TimeHeatmapSeries = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = timeHeatData,
                    Name = "Activity",
                    Fill = new LinearGradientPaint(
                        new[] { new SKColor(100, 149, 237, 100), new SKColor(65, 105, 225) },
                        new SKPoint(0, 0),
                        new SKPoint(0, 1)
                    ),
                    Stroke = null,
                    MaxBarWidth = double.MaxValue
                }
            };
        }

        private void UpdateTopErrorsChart()
        {
            var topErrors = Tabs.SelectMany(t => t.LogEntries)
                .Where(e => e.Level == "ERROR")
                .GroupBy(e => e.Message)
                .Select(g => new { Message = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToList();

            if (topErrors.Any())
            {
                var values = topErrors.Select(e => (double)e.Count).ToArray();
                var labels = topErrors.Select(e => TruncateMessage(e.Message, 40)).ToList();

                TopErrorsSeries = new ISeries[]
                {
                    new ColumnSeries<double>
                    {
                        Values = values,
                        Name = "Count",
                        Fill = new LinearGradientPaint(
                            new[] { new SKColor(255, 0, 0, 200), new SKColor(255, 99, 71, 220) },
                            new SKPoint(0, 0),
                            new SKPoint(0, 1)
                        ),
                        Stroke = new SolidColorPaint(SKColors.OrangeRed.WithAlpha(220), 2),
                        MaxBarWidth = 50,
                        DataLabelsSize = 12,
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                        DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue}"
                    }
                };
            }
            else
            {
                TopErrorsSeries = Array.Empty<ISeries>();
            }
        }

        private void UpdateSourcesDistributionChart()
        {
            var sourcesData = Tabs.SelectMany(t => t.LogEntries)
                .GroupBy(e => e.Source)
                .Select(g => new { Source = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(8)
                .ToList();

            if (sourcesData.Any())
            {
                var sourceValues = new List<ISeries>();
                var colors = new[]
                {
                    SKColors.DodgerBlue, SKColors.MediumSeaGreen, SKColors.Orange,
                    SKColors.Purple, SKColors.OrangeRed, SKColors.Gold,
                    SKColors.MediumVioletRed, SKColors.LightSlateGray
                };

                foreach (var source in sourcesData)
                {
                    sourceValues.Add(new ColumnSeries<double>
                    {
                        Values = new[] { source.Count },
                        Name = source.Source,
                        Fill = new SolidColorPaint(colors[sourceValues.Count % colors.Length]),
                        MaxBarWidth = 35
                    });
                }

                SourcesDistributionSeries = sourceValues.ToArray();
            }
            else
            {
                SourcesDistributionSeries = Array.Empty<ISeries>();
            }
        }

        partial void OnSelectedTabChanged(TabItem? oldValue, TabItem? newValue)
        {
            if (newValue != null)
            {
                StatusMessage = $"Selected file: {newValue.FilePath}";
                LastOpenedFilePath = newValue.FilePath;
            }
            UpdateGlobalStatistics();
        }

        partial void OnIsLoadingChanged(bool value)
        {
            if (!value && SelectedTab != null)
            {
                UpdateGlobalStatistics();
            }
        }
    }
}
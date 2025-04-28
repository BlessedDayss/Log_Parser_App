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
        private string _filePath = string.Empty;
        
        [ObservableProperty]
        private string _fileStatus = "No file selected";
        
        [ObservableProperty]
        private bool _isDarkTheme;
        
        [ObservableProperty]
        private int _selectedTabIndex = 0;
        
        [ObservableProperty]
        private ObservableCollection<LogEntry> _logEntries = new();
        
        [ObservableProperty]
        private ObservableCollection<LogEntry> _filteredLogEntries = new();
        
        [ObservableProperty]
        private ObservableCollection<LogEntry> _errorLogEntries = new();
        
        [ObservableProperty]
        private LogStatistics _logStatistics = new();
        
        [ObservableProperty]
        private bool _isDashboardVisible = false;
        
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
        private PackageLogEntry? _selectedPackageEntry;
        
        [ObservableProperty]
        private LogEntry? _selectedLogEntry;
        
        // LiveCharts - Графики
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
        private Axis[] _daysAxis = { new Axis { Name = "Days", Labels = new List<string>() } };
        
        [ObservableProperty]
        private Axis[] _hoursAxis = { new Axis { Name = "Hours", Labels = new List<string>() { "00:00", "04:00", "08:00", "12:00", "16:00", "20:00", "24:00" } } };
        
        [ObservableProperty]
        private Axis[] _sourceAxis = { new Axis { Name = "Source", LabelsRotation = 15, Labels = new List<string>() } };

        [ObservableProperty]
        private Axis[] _errorMessageAxis = { new Axis { LabelsRotation = 15, Name = "Error Message"} };
        
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
        private async Task LoadFile()
        {
            try
            {
                var file = await _fileService.PickLogFileAsync();
                if (file == null) return;
                LastOpenedFilePath = file;
                
                StatusMessage = $"Opening {Path.GetFileName(file)}...";
                IsLoading = true;
                FileStatus = Path.GetFileName(file);
                
                // Switch to dashboard in UI thread
                IsDashboardVisible = true;
                
                // Execute parsing in a separate thread
                await Task.Run((Func<Task?>)(async () =>
                {
                    // Clear collections only through UI thread
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        LogEntries.Clear();
                    });

                    var entries = await _logParserService.ParseLogFileAsync(file);
                    
                    
                    var logEntries = entries as LogEntry[] ?? entries.ToArray();
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
                    }
                    
                    foreach (var entry in logEntries)
                    {
                        entry.OpenFileCommand = ExternalOpenFileCommand;
                    }
                    
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _logger.LogInformation("Processing {Count} log entries", logEntries.Count());
                        
                        LogEntries.Clear(); // Очищаем основную коллекцию
                        
                        foreach (var entry in logEntries)
                        {
                            LogEntries.Add(entry);

                            // --- Логика рекомендаций для ошибок (остается здесь, т.к. использует ErrorRecommendationService) ---
                            if (entry.Level == "ERROR") // Проверяем финальный уровень ПОСЛЕ парсинга
                            {
                                _logger.LogDebug("Processing recommendations for ERROR entry: '{Message}'", entry.Message);
                                var recommendation = _errorRecommendationService.AnalyzeError(entry.Message);
                                if (recommendation != null)
                                {
                                    entry.ErrorType = recommendation.ErrorType;
                                    entry.ErrorDescription = recommendation.Description;
                                    entry.ErrorRecommendations.Clear();
                                    entry.ErrorRecommendations.AddRange(recommendation.Recommendations);
                                }
                                else
                                {
                                    entry.ErrorType = "UnknownError";
                                    entry.ErrorDescription = "Unknown error. Recommendations not found.";
                                    entry.ErrorRecommendations.Clear(); // Очищаем на случай, если были старые
                                    entry.ErrorRecommendations.Add("Check error log for additional information.");
                                    entry.ErrorRecommendations.Add("Contact documentation or support.");
                                    _logger.LogWarning("No recommendation found for error message: {Message}", entry.Message);
                                }
                                _logger.LogDebug("Finished recommendations processing for entry. HasRecommendations: {HasRecommendations}", entry.HasRecommendations);
                            }
                            // --- Конец логики рекомендаций ---
                        }
                        
                        OnPropertyChanged(nameof(LogEntries)); // Уведомляем об обновлении основной коллекции
                        
                        _logger.LogInformation("Added {Count} entries to main collection", LogEntries.Count);
 
                        // Фильтрованные записи обновляются после основного списка
                        FilteredLogEntries.Clear();
                        foreach (var entry in LogEntries)
                        {
                            FilteredLogEntries.Add(entry);
                        }
                        OnPropertyChanged(nameof(FilteredLogEntries)); // Уведомляем об обновлении фильтрованных записей
                    });
                }));
                
                await Dispatcher.UIThread.InvokeAsync(() => {
                    UpdateErrorLogEntries(); // Обновляем коллекцию ошибок ПОСЛЕ добавления всех записей
                    UpdateLogStatistics(); // Обновляем статистику ПОСЛЕ добавления всех записей
                    StatusMessage = $"Loaded {LogEntries.Count} log entries";
                    SelectedTabIndex = 0;
                });
                
                _logger.LogInformation("Loaded {Count} log entries", LogEntries.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading log file");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        [RelayCommand]
        private void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
            _logger.LogInformation("Theme changed to: {Theme}", IsDarkTheme ? "Dark" : "Light");
        }
        
        private void UpdateErrorLogEntries()
        {
            ErrorLogEntries.Clear();
            _logger.LogDebug("Updating ErrorLogEntries. Checking {Count} entries in LogEntries.", LogEntries.Count);
            var errors = LogEntries.Where(e => e.Level == "ERROR").ToList();
            foreach (var errorEntry in errors)
            {
                _logger.LogTrace("Adding entry to ErrorLogEntries (Line {LineNumber}): Level={Level}", errorEntry.LineNumber, errorEntry.Level);
                ErrorLogEntries.Add(errorEntry);
            }
            _logger.LogInformation("Updated ErrorLogEntries collection with {Count} entries", ErrorLogEntries.Count);
            OnPropertyChanged(nameof(ErrorLogEntries)); // Уведомляем UI об изменениях
        }
        
        private void UpdateLogStatistics()
        {
            if (LogEntries.Count == 0)
            {
                ErrorCount = 0;
                WarningCount = 0;
                InfoCount = 0;
                OtherCount = 0;
                ErrorPercent = 0;
                WarningPercent = 0;
                InfoPercent = 0;
                OtherPercent = 0;
                // Сбрасываем графики
                LevelsOverTimeSeries = Array.Empty<ISeries>();
                TopErrorsSeries = Array.Empty<ISeries>();
                LogDistributionSeries = Array.Empty<ISeries>();
                TimeHeatmapSeries = Array.Empty<ISeries>();
                ErrorTrendSeries = Array.Empty<ISeries>();
                SourcesDistributionSeries = Array.Empty<ISeries>();
                return;
            }
            
            _logger.LogDebug("Updating LogStatistics. Current LogEntries count: {Count}", LogEntries.Count);
            
            ErrorCount = LogEntries.Count(e => e.Level == "ERROR");
            WarningCount = LogEntries.Count(e => e.Level == "WARNING");
            InfoCount = LogEntries.Count(e => e.Level == "INFO");
            OtherCount = LogEntries.Count(e => e.Level != "ERROR" && e.Level != "WARNING" && e.Level != "INFO");
            
            var total = LogEntries.Count;
            ErrorPercent = total > 0 ? Math.Round((double)ErrorCount / total * 100, 1) : 0;
            WarningPercent = total > 0 ? Math.Round((double)WarningCount / total * 100, 1) : 0;
            InfoPercent = total > 0 ? Math.Round((double)InfoCount / total * 100, 1) : 0;
            OtherPercent = total > 0 ? Math.Round((double)OtherCount / total * 100, 1) : 0;
            
            _logger.LogDebug("Calculated counts - Error: {ErrorCount}, Warning: {WarningCount}, Info: {InfoCount}, Other: {OtherCount}", 
                ErrorCount, WarningCount, InfoCount, OtherCount);

            UpdateCharts();
            
            // Обновляем статистику в классе LogStatistics (для будущего использования)
            LogStatistics = new LogStatistics
            {
                TotalCount = total,
                ErrorCount = ErrorCount,
                WarningCount = WarningCount,
                InfoCount = InfoCount,
                OtherCount = OtherCount,
                ErrorPercent = ErrorPercent,
                WarningPercent = WarningPercent,
                InfoPercent = InfoPercent,
                OtherPercent = OtherPercent
            };
        }
        
        private void UpdateCharts()
        {
            try
            {
                // 1. График распределения типов логов (пончик)
                LogDistributionSeries = ErrorCount + WarningCount + InfoCount + OtherCount > 0 
                ? new ISeries[]
                {
                    new PieSeries<double>
                    {
                        Values = new double[] { ErrorCount },
                        Name = "Ошибки",
                        Fill = new SolidColorPaint(SKColors.OrangeRed),
                        InnerRadius = 50,
                        MaxRadialColumnWidth = 20,
                        DataLabelsSize = 14,
                        DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue} ({Math.Round((point.StackedValue?.Total ?? 0) == 0 ? 0 : point.Coordinate.PrimaryValue / (point.StackedValue?.Total ?? 1) * 100)}%)",
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer
                    },
                    new PieSeries<double>
                    {
                        Values = new double[] { WarningCount },
                        Name = "Предупреждения",
                        Fill = new SolidColorPaint(SKColors.Orange),
                        InnerRadius = 50,
                        MaxRadialColumnWidth = 20,
                        DataLabelsSize = 14,
                        DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue} ({Math.Round((point.StackedValue?.Total ?? 0) == 0 ? 0 : point.Coordinate.PrimaryValue / (point.StackedValue?.Total ?? 1) * 100)}%)",
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer
                    },
                    new PieSeries<double>
                    {
                        Values = new double[] { InfoCount },
                        Name = "Информация",
                        Fill = new SolidColorPaint(SKColors.DodgerBlue),
                        InnerRadius = 50,
                        MaxRadialColumnWidth = 20,
                        DataLabelsSize = 14,
                        DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue} ({Math.Round((point.StackedValue?.Total ?? 0) == 0 ? 0 : point.Coordinate.PrimaryValue / (point.StackedValue?.Total ?? 1) * 100)}%)",
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer
                    },
                    new PieSeries<double>
                    {
                        Values = new double[] { OtherCount },
                        Name = "Другие",
                        Fill = new SolidColorPaint(SKColors.Gray),
                        InnerRadius = 50,
                        MaxRadialColumnWidth = 20,
                        DataLabelsSize = 14,
                        DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue} ({Math.Round((point.StackedValue?.Total ?? 0) == 0 ? 0 : point.Coordinate.PrimaryValue / (point.StackedValue?.Total ?? 1) * 100)}%)",
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer
                    }
                }
                : Array.Empty<ISeries>();
                
                // 2. Временная динамика логов по часам - (линейный график)
                // Сгруппируем логи по часам для удобства анализа
                var timeGroups = LogEntries
                    .GroupBy(e => e.Timestamp.Hour)
                    .OrderBy(g => g.Key)
                    .ToList();
                
                // Создаем полный список часов от 0 до 23 для равномерного отображения
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
                
                LevelsOverTimeSeries = new ISeries[]
                {
                    new LineSeries<DateTimePoint>
                    {
                        Values = totalByHour,
                        Name = "Все логи",
                        Stroke = new SolidColorPaint(SKColors.Gray, 3),
                        Fill = new SolidColorPaint(SKColors.Gray.WithAlpha(40)),
                        GeometryFill = new SolidColorPaint(SKColors.White),
                        GeometryStroke = new SolidColorPaint(SKColors.Gray, 2),
                        LineSmoothness = 0.5
                    },
                    new LineSeries<DateTimePoint>
                    {
                        Values = errorsByHour,
                        Name = "Ошибки",
                        Stroke = new SolidColorPaint(SKColors.OrangeRed, 3),
                        Fill = new SolidColorPaint(SKColors.OrangeRed.WithAlpha(40)),
                        GeometryFill = new SolidColorPaint(SKColors.White),
                        GeometryStroke = new SolidColorPaint(SKColors.OrangeRed, 2),
                        LineSmoothness = 0.5
                    },
                    new LineSeries<DateTimePoint>
                    {
                        Values = warningsByHour,
                        Name = "Предупреждения",
                        Stroke = new SolidColorPaint(SKColors.Orange, 3),
                        Fill = new SolidColorPaint(SKColors.Orange.WithAlpha(40)),
                        GeometryFill = new SolidColorPaint(SKColors.White),
                        GeometryStroke = new SolidColorPaint(SKColors.Orange, 2),
                        LineSmoothness = 0.5
                    },
                    new LineSeries<DateTimePoint>
                    {
                        Values = infosByHour,
                        Name = "Информация",
                        Stroke = new SolidColorPaint(SKColors.DodgerBlue, 3),
                        Fill = new SolidColorPaint(SKColors.DodgerBlue.WithAlpha(40)),
                        GeometryFill = new SolidColorPaint(SKColors.White),
                        GeometryStroke = new SolidColorPaint(SKColors.DodgerBlue, 2),
                        LineSmoothness = 0.5
                    }
                };
                
                // 3. Heatmap распределения логов по времени
                int maxValue = totalByHour.Any() ? totalByHour.Select(p => (int)p.Value).Max() : 0;
                
                var timeHeatData = totalByHour.Select(p => p.Value).ToArray();
                
                TimeHeatmapSeries = new ISeries[]
                {
                    new ColumnSeries<double>
                    {
                        Values = timeHeatData,
                        Name = "Активность",
                        Fill = new LinearGradientPaint(
                            new[] { new SKColor(100, 149, 237, 100), new SKColor(65, 105, 225) },
                            new SKPoint(0, 0),
                            new SKPoint(0, 1)
                        ),
                        Stroke = null,
                        MaxBarWidth = double.MaxValue
                    }
                };
                
                // 4. Тренд ошибок по часам
                var errorTrend = errorsByHour.Select(p => p.Value).ToArray();
                
                ErrorTrendSeries = errorTrend.Any(v => v > 0) ? new ISeries[]
                {
                    new LineSeries<double>
                    {
                        Values = errorTrend,
                        Name = "Тренд ошибок",
                        Stroke = new SolidColorPaint(SKColors.OrangeRed, 3),
                        Fill = new SolidColorPaint(SKColors.OrangeRed.WithAlpha(40)),
                        GeometryFill = new SolidColorPaint(SKColors.White),
                        GeometryStroke = new SolidColorPaint(SKColors.OrangeRed, 2),
                        LineSmoothness = 0.8
                    }
                } : Array.Empty<ISeries>();
                
                // 5. ТОП ошибок (гистограмма)
                var topErrors = LogEntries
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
                            Name = "Количество",
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
                    
                    ErrorMessageAxis[0].Labels = labels;
                    ErrorMessageAxis[0].TextSize = 11;
                }
                else
                {
                    TopErrorsSeries = Array.Empty<ISeries>();
                    ErrorMessageAxis[0].Labels = new List<string>();
                }
                
                // 6. Распределение логов по источникам
                var sourcesData = LogEntries
                    .GroupBy(e => e.Source)
                    .Select(g => new { Source = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(8)
                    .ToList();
                
                if (sourcesData.Any())
                {
                    var sourceValues = new List<ISeries>();
                    var colors = new SKColor[] 
                    { 
                        SKColors.DodgerBlue, SKColors.MediumSeaGreen, SKColors.Orange, 
                        SKColors.Purple, SKColors.OrangeRed, SKColors.Gold, 
                        SKColors.MediumVioletRed, SKColors.LightSlateGray 
                    };
                    
                    // Создаем серии для каждого типа лога (Error, Warning, Info)
                    for (int i = 0; i < sourcesData.Count; i++)
                    {
                        var source = sourcesData[i].Source;
                        var sourceEntries = LogEntries.Where(e => e.Source == source).ToList();
                        
                        var errorCount = sourceEntries.Count(e => e.Level == "ERROR");
                        var warningCount = sourceEntries.Count(e => e.Level == "WARNING");
                        var infoCount = sourceEntries.Count(e => e.Level == "INFO");
                        var otherCount = sourceEntries.Count - errorCount - warningCount - infoCount;
                        
                        sourceValues.Add(new StackedColumnSeries<double>
                        {
                            Values = new double[] { errorCount, warningCount, infoCount, otherCount },
                            Name = source,
                            Stroke = null,
                            DataLabelsSize = 10,
                            DataLabelsPaint = new SolidColorPaint(SKColors.White),
                            Fill = new SolidColorPaint(colors[i % colors.Length])
                        });
                    }
                    
                    SourcesDistributionSeries = sourceValues.ToArray();
                    SourceAxis[0].Labels = sourcesData.Select(x => x.Source).ToList();
                }
                else
                {
                    SourcesDistributionSeries = Array.Empty<ISeries>();
                    SourceAxis[0].Labels = new List<string>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating charts");
            }
        }
        
        private string TruncateMessage(string message, int maxLength)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;
            return message.Length <= maxLength ? message : message.Substring(0, maxLength) + "...";
        }

        [RelayCommand]
        private void ShowPackageErrorDetails(PackageLogEntry? entry)
        {
            if (entry == null)
                return;
                
            SelectedPackageEntry = entry;
            _logger.LogInformation("Selected package error entry: {PackageId}", entry.PackageId);
        }

        [RelayCommand]
        private static void ApplyFilters()
        {
            // Implementation of ApplyFilters method
            // This method should filter LogEntries and result in FilteredLogEntries
            // ... existing code ...
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
                        error = $"Не удалось открыть файл через xdg-open: {ex.Message}\n{ex.StackTrace}";
                        _logger.LogError(ex, "Linux open failed");
                    }
                }
                else
                {
                    error = "Неизвестная ОС, не могу открыть файл";
                }
                if (!opened)
                {
                    StatusMessage = error ?? "Не удалось открыть файл (неизвестная ошибка)";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open file {FilePath}", filePath);
                StatusMessage = $"Ошибка открытия файла: {ex.Message}\n{ex.StackTrace}";
            }
        }
    }
} 
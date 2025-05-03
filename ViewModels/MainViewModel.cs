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
        private Axis[] _errorMessageAxis = { new Axis { LabelsRotation = 15, Name = "Error Message" } };

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

            // Проверяем аргументы командной строки для автоматической загрузки файла
            CheckCommandLineArgs();

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

        private void CheckCommandLineArgs()
        {
            // Получаем аргументы командной строки из Program
            var args = Program.StartupArgs;
            
            if (args.Length > 0 && !string.IsNullOrEmpty(args[0]))
            {
                var filePath = args[0];
                
                // Если файл существует, загружаем его с небольшой задержкой
                // чтобы UI успел инициализироваться
                if (System.IO.File.Exists(filePath))
                {
                    _logger.LogInformation("Запланирована загрузка файла из аргументов командной строки: {FilePath}", filePath);
                    LastOpenedFilePath = filePath;
                    
                    // Делаем небольшую задержку перед загрузкой файла
                    Task.Delay(500).ContinueWith(async _ => 
                    {
                        await Dispatcher.UIThread.InvokeAsync(async () => 
                        {
                            await LoadFileAsync(filePath);
                        });
                    });
                }
            }
        }

        private async Task LoadFileAsync(string filePath)
        {
            try
            {
                StatusMessage = $"Opening {Path.GetFileName(filePath)}...";
                IsLoading = true;
                FileStatus = Path.GetFileName(filePath);

                // Переключаемся на дашборд
                IsDashboardVisible = true;

                _logger.LogInformation("PERF: Начало загрузки файла {FilePath}", filePath);
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // Очищаем коллекции только через UI поток
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    LogEntries.Clear();
                    FilteredLogEntries.Clear();
                    ErrorLogEntries.Clear();
                }, DispatcherPriority.Background);

                // Выполняем парсинг полностью отдельно от UI-потока
                var entries = await Task.Run(async () => {
                    try 
                    {
                        _logger.LogDebug("PERF: Начало парсинга файла {FilePath}", filePath);
                        var parseStopwatch = System.Diagnostics.Stopwatch.StartNew();
                        var logEntries = await _logParserService.ParseLogFileAsync(filePath);
                        _logger.LogDebug("PERF: Парсинг файла завершен за {ElapsedMs}ms", parseStopwatch.ElapsedMilliseconds);
                        return logEntries;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при парсинге файла {FilePath}", filePath);
                        throw;
                    }
                });

                var logEntries = entries as LogEntry[] ?? entries.ToArray();
                _logger.LogDebug("PERF: Начало предварительной обработки {Count} записей", logEntries.Length);

                var processedEntries = await Task.Run(() => {
                    // Преобразуем записи в потоке пула потоков
                    List<LogEntry> processed = new List<LogEntry>(logEntries.Length);
                    foreach (var entry in logEntries)
                    {
                        try
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
                            processed.Add(entry);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Ошибка при обработке записи {LineNumber}", entry.LineNumber);
                        }
                    }
                    return processed;
                });

                _logger.LogDebug("PERF: Начало обработки рекомендаций для ошибок");
                
                // Добавляем рекомендации для ошибок параллельно с загрузкой UI
                var errorsWithRecommendations = await Task.Run(() => {
                    var errorEntries = processedEntries.Where(e => e.Level == "ERROR").ToList();
                    Parallel.ForEach(errorEntries, entry => {
                        try
                        {
                            _logger.LogTrace("Обработка рекомендаций для ошибки: '{Message}'", entry.Message);
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
                                entry.ErrorRecommendations.Clear();
                                entry.ErrorRecommendations.Add("Check error log for additional information.");
                                entry.ErrorRecommendations.Add("Contact documentation or support.");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Ошибка при обработке рекомендаций для записи {LineNumber}", entry.LineNumber);
                        }
                    });
                    return errorEntries;
                });

                // Загружаем записи в UI пакетами для предотвращения блокировки
                const int batchSize = 1000;
                for (int i = 0; i < processedEntries.Count; i += batchSize)
                {
                    var batch = processedEntries.Skip(i).Take(batchSize).ToList();
                    
                    await Dispatcher.UIThread.InvokeAsync(() => {
                        foreach (var entry in batch)
                        {
                            LogEntries.Add(entry);
                            FilteredLogEntries.Add(entry);
                        }
                        
                        // Обновляем статус для пользователя
                        if (i + batchSize < processedEntries.Count)
                        {
                            StatusMessage = $"Loading entries... ({i + batch.Count}/{processedEntries.Count})";
                        }
                    }, DispatcherPriority.Background);
                    
                    // Делаем небольшую паузу, чтобы UI мог перерисоваться
                    await Task.Delay(10);
                }

                // Обновляем UI и статистику после завершения загрузки
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateErrorLogEntries();
                    UpdateLogStatistics();
                    
                    _logger.LogDebug("PERF: Завершение загрузки данных в UI");
                    _logger.LogInformation("Загружено {Count} записей логов за {ElapsedMs}ms", LogEntries.Count, sw.ElapsedMilliseconds);
                    
                    StatusMessage = $"Loaded {LogEntries.Count} log entries";
                    SelectedTabIndex = 0;
                    IsLoading = false;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка загрузки файла логов");
                
                await Dispatcher.UIThread.InvokeAsync(() => {
                    StatusMessage = $"Error: {ex.Message}";
                    IsLoading = false;
                });
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
                
                // Используем наш оптимизированный метод загрузки файла
                await LoadFileAsync(file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка выбора и загрузки файла логов");
                StatusMessage = $"Error: {ex.Message}";
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
                // 1. Log Type Distribution chart (Pie chart)
                LogDistributionSeries = ErrorCount + WarningCount + InfoCount + OtherCount > 0
                ? new ISeries[]
                {
                    new PieSeries<double>
                    {
                        Values = new double[] { ErrorCount },
                        Name = "Errors",
                        Fill = new SolidColorPaint(SKColors.Crimson),
                        InnerRadius = 60,
                        MaxRadialColumnWidth = 25,
                        // Using default font size to avoid crash
                        DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue} ({Math.Round(point.Coordinate.PrimaryValue / (point.StackedValue?.Total ?? 1) * 100)}%)",
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle
                    },
                    new PieSeries<double>
                    {
                        Values = new double[] { WarningCount },
                        Name = "Warnings",
                        Fill = new SolidColorPaint(SKColors.Orange),
                        InnerRadius = 60,
                        MaxRadialColumnWidth = 25,
                        // Using default font size to avoid crash
                        DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue} ({Math.Round(point.Coordinate.PrimaryValue / (point.StackedValue?.Total ?? 1) * 100)}%)",
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle
                    },
                    new PieSeries<double>
                    {
                        Values = new double[] { InfoCount },
                        Name = "Info",
                        Fill = new SolidColorPaint(SKColors.DodgerBlue),
                        InnerRadius = 60,
                        MaxRadialColumnWidth = 25,
                        // Using default font size to avoid crash
                        DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue} ({Math.Round(point.Coordinate.PrimaryValue / (point.StackedValue?.Total ?? 1) * 100)}%)",
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle
                    },
                    new PieSeries<double>
                    {
                        Values = new double[] { OtherCount },
                        Name = "Others",
                        Fill = new SolidColorPaint(SKColors.SlateGray),
                        InnerRadius = 60,
                        MaxRadialColumnWidth = 25,
                        // Using default font size to avoid crash
                        DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue} ({Math.Round(point.Coordinate.PrimaryValue / (point.StackedValue?.Total ?? 1) * 100)}%)",
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle
                    }
                }
                : Array.Empty<ISeries>();

                // 2. Logs By Hour - (Line chart)
                // Группируем логи не по часам, а по фактической дате и времени для более точного отображения
                var actualTimeGroups = LogEntries
                    .GroupBy(e => new { Date = e.Timestamp.Date, Hour = e.Timestamp.Hour, Minute = e.Timestamp.Minute / 10 * 10 })
                    .OrderBy(g => g.Key.Date)
                    .ThenBy(g => g.Key.Hour)
                    .ThenBy(g => g.Key.Minute)
                    .ToList();

                // Находим минимальное и максимальное время в логах
                var minTimestamp = LogEntries.Any() ? LogEntries.Min(e => e.Timestamp) : DateTime.Now.AddHours(-1);
                var maxTimestamp = LogEntries.Any() ? LogEntries.Max(e => e.Timestamp) : DateTime.Now;

                // Если диапазон слишком мал (меньше часа), расширяем его для лучшей визуализации
                if ((maxTimestamp - minTimestamp).TotalMinutes < 60)
                {
                    maxTimestamp = minTimestamp.AddHours(1);
                }

                // Создаем список времен для отображения на оси
                var timePoints = new List<DateTime>();
                var tickInterval = DetermineOptimalTimeInterval(minTimestamp, maxTimestamp);
                var currentTime = new DateTime(minTimestamp.Year, minTimestamp.Month, minTimestamp.Day,
                                              minTimestamp.Hour, minTimestamp.Minute / tickInterval * tickInterval, 0);

                while (currentTime <= maxTimestamp.AddMinutes(tickInterval))
                {
                    timePoints.Add(currentTime);
                    currentTime = currentTime.AddMinutes(tickInterval);
                }

                // Создаем точки данных на основе фактического времени
                var errorsByTime = new List<DateTimePoint>();
                var warningsByTime = new List<DateTimePoint>();
                var infosByTime = new List<DateTimePoint>();
                var totalByTime = new List<DateTimePoint>();

                // Создаем точки для каждого интервала времени
                foreach (var time in timePoints)
                {
                    var endTime = time.AddMinutes(tickInterval);
                    var entries = LogEntries.Where(e => e.Timestamp >= time && e.Timestamp < endTime).ToList();

                    var errorCount = entries.Count(e => e.Level == "ERROR");
                    var warningCount = entries.Count(e => e.Level == "WARNING");
                    var infoCount = entries.Count(e => e.Level == "INFO");
                    var totalCount = entries.Count;

                    errorsByTime.Add(new DateTimePoint(time, errorCount));
                    warningsByTime.Add(new DateTimePoint(time, warningCount));
                    infosByTime.Add(new DateTimePoint(time, infoCount));
                    totalByTime.Add(new DateTimePoint(time, totalCount));
                }

                // Форматируем метки времени для оси X
                List<string> timeLabels = new List<string>();
                string format = DetermineTimeFormat(minTimestamp, maxTimestamp, tickInterval);

                // Настраиваем ось времени с динамическими метками
                TimeAxis[0] = new Axis
                {
                    Name = "Время",
                    NamePaint = new SolidColorPaint(SKColors.LightGray),
                    LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                    TextSize = 12,
                    Labeler = value =>
                    {
                        try
                        {
                            return new DateTime((long)value).ToString(format);
                        }
                        catch
                        {
                            return string.Empty;
                        }
                    },
                    UnitWidth = TimeSpan.FromMinutes(tickInterval).Ticks,
                    MinStep = TimeSpan.FromMinutes(tickInterval).Ticks,
                    SeparatorsPaint = new SolidColorPaint(SKColors.LightSlateGray) { StrokeThickness = 0.5f },
                    ShowSeparatorLines = true
                };

                // Улучшенные линейные серии с более информативной визуализацией
                LevelsOverTimeSeries = new ISeries[]
                {
                    new LineSeries<DateTimePoint>
                    {
                        Values = totalByTime,
                        Name = "Все логи",
                        Stroke = new SolidColorPaint(SKColors.LightGray, 2),
                        Fill = new SolidColorPaint(SKColors.Gray.WithAlpha(40)),
                        GeometryFill = new SolidColorPaint(SKColors.White),
                        GeometryStroke = new SolidColorPaint(SKColors.Gray, 2),
                        GeometrySize = 8,
                        LineSmoothness = 0.2,
                    },
                    new LineSeries<DateTimePoint>
                    {
                        Values = errorsByTime,
                        Name = "Ошибки",
                        Stroke = new SolidColorPaint(SKColors.Crimson, 3),
                        Fill = new SolidColorPaint(SKColors.Crimson.WithAlpha(40)),
                        GeometryFill = new SolidColorPaint(SKColors.White),
                        GeometryStroke = new SolidColorPaint(SKColors.Crimson, 2),
                        GeometrySize = 10,
                        LineSmoothness = 0.2,
                    },
                    new LineSeries<DateTimePoint>
                    {
                        Values = warningsByTime,
                        Name = "Предупреждения",
                        Stroke = new SolidColorPaint(SKColors.Orange, 3),
                        Fill = new SolidColorPaint(SKColors.Orange.WithAlpha(40)),
                        GeometryFill = new SolidColorPaint(SKColors.White),
                        GeometryStroke = new SolidColorPaint(SKColors.Orange, 2),
                        GeometrySize = 10,
                        LineSmoothness = 0.2,
                    },
                    new LineSeries<DateTimePoint>
                    {
                        Values = infosByTime,
                        Name = "Информация",
                        Stroke = new SolidColorPaint(SKColors.DodgerBlue, 3),
                        Fill = new SolidColorPaint(SKColors.DodgerBlue.WithAlpha(40)),
                        GeometryFill = new SolidColorPaint(SKColors.White),
                        GeometryStroke = new SolidColorPaint(SKColors.DodgerBlue, 2),
                        GeometrySize = 10,
                        LineSmoothness = 0.2,
                    }
                };

                // 3. Activity Heat Map
                int maxValue = totalByTime.Any() ? totalByTime.Select(p => (int)p.Value).Max() : 0;

                var timeHeatData = totalByTime.Select(p => p.Value).ToArray();

                TimeHeatmapSeries = new ISeries[]
                {
                    new ColumnSeries<double>
                    {
                        Values = timeHeatData,
                        Name = "Activity",
                        Fill = new LinearGradientPaint(
                            new[] {
                                new SKColor(65, 105, 225, 80), // Light blue
                                new SKColor(65, 105, 225, 140),
                                new SKColor(65, 105, 225, 200),
                                new SKColor(65, 105, 225, 255)  // Royal blue
                            },
                            new SKPoint(0, 1),
                            new SKPoint(0, 0)
                        ),
                        Stroke = null,
                        MaxBarWidth = double.MaxValue,
                        IgnoresBarPosition = true
                    }
                };

                // 4. Error Trend by Hour
                var errorTrend = errorsByTime.Select(p => p.Value).ToArray();

                ErrorTrendSeries = errorTrend.Any(v => v > 0) ? new ISeries[]
                {
                    new LineSeries<double>
                    {
                        Values = errorTrend,
                        Name = "Error Trend",
                        Stroke = new SolidColorPaint(SKColors.Crimson, 3),
                        Fill = new SolidColorPaint(SKColors.Crimson.WithAlpha(40)),
                        GeometryFill = new SolidColorPaint(SKColors.White),
                        GeometryStroke = new SolidColorPaint(SKColors.Crimson, 2),
                        LineSmoothness = 0.8
                    }
                } : Array.Empty<ISeries>();

                // 5. Top Errors Chart
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
                    var labels = topErrors.Select(e => TruncateMessage(e.Message, 30)).ToList();

                    TopErrorsSeries = new ISeries[]
                    {
                        new ColumnSeries<double>
                        {
                            Values = values,
                            Name = "Count",
                            Fill = new LinearGradientPaint(
                                new[] {
                                    new SKColor(220, 53, 69, 190),  // Bootstrap danger lighter
                                    new SKColor(220, 53, 69, 230)   // Bootstrap danger
                                },
                                new SKPoint(0, 0),
                                new SKPoint(0, 1)
                            ),
                            Stroke = new SolidColorPaint(SKColors.Crimson.WithAlpha(220), 2),
                            MaxBarWidth = 50,
                            DataLabelsPaint = new SolidColorPaint(SKColors.White),
                            DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                            DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue}"
                        }
                    };

                    ErrorMessageAxis[0].Labels = labels;
                    ErrorMessageAxis[0].Name = "Error Messages";
                    // Safely set text size for axis
                    try
                    {
                        ErrorMessageAxis[0].TextSize = 11;
                        ErrorMessageAxis[0].LabelsRotation = 25;
                    }
                    catch
                    {
                        _logger.LogWarning("Could not set text size for error message axis");
                    }
                }
                else
                {
                    TopErrorsSeries = Array.Empty<ISeries>();
                    ErrorMessageAxis[0].Labels = new List<string>();
                    ErrorMessageAxis[0].Name = "Error Messages";
                }

                // 6. Source Distribution
                var sourcesData = LogEntries
                    .GroupBy(e => e.Source)
                    .Where(g => !string.IsNullOrEmpty(g.Key)) // Filter out empty sources
                    .Select(g => new { Source = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(8)
                    .ToList();

                if (sourcesData.Any())
                {
                    var sourceValues = new List<ISeries>();
                    var colors = new SKColor[]
                    {
                        SKColors.CornflowerBlue, SKColors.MediumSeaGreen, SKColors.Orange,
                        SKColors.MediumPurple, SKColors.Crimson, SKColors.Gold,
                        SKColors.MediumVioletRed, SKColors.SlateBlue
                    };

                    // Create series for each log type (Error, Warning, Info)
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
                            DataLabelsPaint = new SolidColorPaint(SKColors.White),
                            Fill = new SolidColorPaint(colors[i % colors.Length]),
                            Padding = 10
                        });
                    }

                    SourcesDistributionSeries = sourceValues.ToArray();
                    SourceAxis[0].Labels = sourcesData.Select(x => x.Source).ToList();
                    SourceAxis[0].LabelsRotation = 25;
                    SourceAxis[0].Name = "Log Sources";
                    SourceAxis[0].SeparatorsPaint = new SolidColorPaint(SKColors.LightGray.WithAlpha(100));
                    SourceAxis[0].ShowSeparatorLines = true;
                }
                else
                {
                    SourcesDistributionSeries = Array.Empty<ISeries>();
                    SourceAxis[0].Labels = new List<string>();
                    SourceAxis[0].Name = "Log Sources";
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

        private int DetermineOptimalTimeInterval(DateTime minTimestamp, DateTime maxTimestamp)
        {
            var totalMinutes = (maxTimestamp - minTimestamp).TotalMinutes;
            if (totalMinutes <= 60) return 10; // 10-минутные интервалы для периода менее часа
            if (totalMinutes <= 180) return 30; // 30-минутные интервалы для периода менее 3 часов
            if (totalMinutes <= 720) return 60; // 1-часовые интервалы для периода менее 12 часов
            return 120; // 2-часовые интервалы для более длительных периодов
        }

        private string DetermineTimeFormat(DateTime minTimestamp, DateTime maxTimestamp, int tickInterval)
        {
            if ((maxTimestamp - minTimestamp).TotalDays > 1)
                return "dd.MM HH:mm"; // Включаем день для многодневных логов
            return "HH:mm"; // Только время для однодневных логов
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
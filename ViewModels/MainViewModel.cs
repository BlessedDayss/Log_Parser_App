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
using System.Threading; // Corrected
using Log_Parser_App.Models.Interfaces;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;

namespace Log_Parser_App.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly ILogParserService _logParserService;
        private readonly ILogger<MainViewModel> _logger;
        private readonly IFileService _fileService;
        private readonly IErrorRecommendationService _errorRecommendationService;
        private readonly IFilePickerService _filePickerService;
        private readonly IIISLogParserService _iisLogParserService;

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
        private bool _isMultiFileModeActive = false;

        [ObservableProperty]
        private int _selectedTabIndex = 0;

        private ObservableCollection<TabViewModel> _fileTabs = new();
        public ObservableCollection<TabViewModel> FileTabs
        {
            get => _fileTabs;
            set { _fileTabs = value; OnPropertyChanged(); }
        }

        public ObservableCollection<LogEntry> AllErrorLogEntries { get; } = new();

        private TabViewModel? _selectedTab;
        public TabViewModel? SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (_selectedTab != null)
                {
                    _selectedTab.PropertyChanged -= SelectedTab_PropertyChanged;
                }

                if (SetProperty(ref _selectedTab, value))
                {
                    OnPropertyChanged(nameof(IsCurrentTabIIS)); 
                    if (_selectedTab != null)
                    {
                        _selectedTab.PropertyChanged += SelectedTab_PropertyChanged;
                        FilePath = _selectedTab.FilePath;
                        FileStatus = _selectedTab.Title;
                        // UpdateLogStatistics will be called due to PropertyChanged event if counts change, 
                        // or immediately if the tab type dictates a full refresh.
                    }
                    else
                    {
                        FilePath = string.Empty;
                        FileStatus = "No file selected";
                        IsDashboardVisible = !FileTabs.Any();
                    }
                    UpdateLogStatistics(); // Call this to refresh stats when tab selection changes
                }
            }
        }

        private void SelectedTab_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (SelectedTab != null && SelectedTab.IsThisTabIIS)
            {
                if (e.PropertyName == nameof(TabViewModel.IIS_TotalCount) ||
                    e.PropertyName == nameof(TabViewModel.IIS_ErrorCount) ||
                    e.PropertyName == nameof(TabViewModel.IIS_InfoCount) ||
                    e.PropertyName == nameof(TabViewModel.IIS_RedirectCount))
                {
                    UpdateLogStatistics();
                }
            }
            // For standard logs, existing mechanisms (e.g., ApplyFiltersCommand directly calling UpdateLogStatistics) should handle updates.
            // Or, if TabViewModel for standard logs also exposed aggregated counts via PropertyChanged, we could listen here too.
        }

        // Property to indicate if the current tab is an IIS log
        public bool IsCurrentTabIIS => SelectedTab?.LogType == LogFormatType.IIS;

        private List<LogEntry> _logEntries = new();
        public List<LogEntry> LogEntries
        {
            get => _logEntries;
            set { _logEntries = value; OnPropertyChanged(); }
        }

        private List<LogEntry> _filteredLogEntries = new();
        public List<LogEntry> FilteredLogEntries
        {
            get => _filteredLogEntries;
            set { _filteredLogEntries = value; OnPropertyChanged(); }
        }

        private List<LogEntry> _errorLogEntries = new();
        public List<LogEntry> ErrorLogEntries
        {
            get => _errorLogEntries;
            set { _errorLogEntries = value; OnPropertyChanged(); }
        }

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

        // Filter Criteria Properties
        public ObservableCollection<FilterCriterion> FilterCriteria { get; } = new();

        // Example: Define available fields and operators at the MainViewModel level
        // These would be used to populate the FilterCriterion instances
        private readonly List<string> _masterAvailableFields = new List<string> { "Timestamp", "Level", "Message", "Source", "RawData", "CorrelationId", "ErrorType" };
        public Dictionary<string, List<string>> OperatorsByFieldType { get; } = new()
        {
            { "Timestamp", new List<string> { "Equals", "Before", "After", "Between" } },
            { "Level", new List<string> { "Equals", "Not Equals" } },
            { "Message", new List<string> { "Contains", "Equals", "StartsWith", "EndsWith", "Regex Not Contains" } },
            { "Source", new List<string> { "Equals", "Contains" } },
            { "RawData", new List<string> { "Contains", "Regex" } },
            { "CorrelationId", new List<string> { "Equals" } },
            { "ErrorType", new List<string> { "Equals", "Contains" } }
        };
        public Dictionary<string, ObservableCollection<string>> AvailableValuesByField { get; } = new()
        {
            { "Level", new ObservableCollection<string> { "ERROR", "INFO", "WARNING" } }
            // Other fields might not have predefined values, or they could be populated dynamically
        };

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
            IErrorRecommendationService errorRecommendationService,
            IFilePickerService filePickerService,
            IIISLogParserService iisLogParserService)
        {
            _logParserService = logParserService;
            _logger = logger;
            _fileService = fileService;
            _errorRecommendationService = errorRecommendationService;
            _filePickerService = filePickerService;
            _iisLogParserService = iisLogParserService;

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
                IsDashboardVisible = true;
                _logger.LogInformation("PERF: Начало загрузки файла {FilePath}", filePath);
                var sw = System.Diagnostics.Stopwatch.StartNew();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    LogEntries.Clear();
                    FilteredLogEntries.Clear();
                    ErrorLogEntries.Clear();
                }, DispatcherPriority.Background);

                // Выполняем парсинг полностью отдельно от UI-потока
                var entries = await Task.Run(async () =>
                {
                    try
                    {
                        _logger.LogDebug("PERF: Начало парсинга файла {FilePath}", filePath);
                        var parseStopwatch = System.Diagnostics.Stopwatch.StartNew();
                        
                        var entriesList = new List<LogEntry>();
                        await foreach (var entryValue in _logParserService.ParseLogFileAsync(filePath, CancellationToken.None))
                        {
                            entriesList.Add(entryValue);
                        }
                        var logEntriesResult = entriesList; // Use this variable below

                        _logger.LogDebug("PERF: Парсинг файла завершен за {ElapsedMs}ms", parseStopwatch.ElapsedMilliseconds);
                        return logEntriesResult;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при парсинге файла {FilePath}", filePath);
                        throw;
                    }
                });

                // var logEntries = entries as LogEntry[] ?? entries.ToArray(); // Old line
                var loadedLogEntries = entries; // 'entries' is now the List<LogEntry> from Task.Run

                _logger.LogDebug("PERF: Начало предварительной обработки {Count} записей", loadedLogEntries.Count);

                var processedEntries = await Task.Run(() =>
                {
                    List<LogEntry> processed = new List<LogEntry>(loadedLogEntries.Count);
                    foreach (var entry in loadedLogEntries) // Use loadedLogEntries here
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

                await Task.Run(() =>
                {
                    var errorEntries = processedEntries.Where(e => e.Level.Equals("Error", StringComparison.OrdinalIgnoreCase)).ToList();
                    Parallel.ForEach(errorEntries, entry =>
                    {
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
                });

                // Обновляем UI и статистику после полной загрузки
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    LogEntries = processedEntries.ToList(); // Ensure this is a new list if processedEntries is reused
                    FilteredLogEntries = processedEntries.ToList(); // Ensure this is a new list
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
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
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
                var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
                var files = await _filePickerService.PickFilesAsync(mainWindow);
                if (files == null || !files.Any()) return;
                await LoadFilesAsync(files);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка загрузки файлов логов");
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusMessage = $"Error: {ex.Message}";
                    IsLoading = false;
                });
            }
        }

        private async Task LoadFilesAsync(IEnumerable<string> files)
        {
            StatusMessage = $"Opening {files.Count()} files...";
            IsLoading = true;
            FileStatus = $"{files.Count()} files";
            IsDashboardVisible = true;
            
            foreach (var file in files)
            {
                // Check if the file is already opened
                if (FileTabs.Any(tab => tab.FilePath == file))
                {
                    var existingTab = FileTabs.First(tab => tab.FilePath == file);
                    SelectedTab = existingTab;
                    continue;
                }
                
                await LoadFileToTab(file);
            }
            
            UpdateMultiFileModeStatus();
            _logger.LogInformation("[LoadFilesAsync] After UpdateMultiFileModeStatus. IsMultiFileModeActive: {IsActive}", IsMultiFileModeActive);
            UpdateAllErrorLogEntries();
            IsLoading = false;
            StatusMessage = $"Finished processing {files.Count()} files. {FileTabs.Count(t => files.Contains(t.FilePath))} new tab(s) added.";
            if (FileTabs.Any() && SelectedTab == null)
            {
                SelectedTab = FileTabs.LastOrDefault(t => files.Contains(t.FilePath)) ?? FileTabs.LastOrDefault();
            }
        }
        
        private async Task LoadFileToTab(string filePath)
        {
            try
            {
                var entriesList = new List<LogEntry>();
                await foreach (var entryValue in _logParserService.ParseLogFileAsync(filePath, CancellationToken.None))
                {
                    entriesList.Add(entryValue);
                }
                // var logEntriesArr = entries as LogEntry[] ?? entries.ToArray(); // Old logic
                var logEntriesArr = entriesList.ToArray(); // New logic based on collected list

                var processedEntries = await Task.Run(() =>
                {
                    var processed = new List<LogEntry>(logEntriesArr.Length);
                    foreach (var entry in logEntriesArr) // Iterate over the array collected from streaming
                    {
                        entry.OpenFileCommand = ExternalOpenFileCommand;
                        processed.Add(entry);
                    }
                    return processed;
                });
                
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var title = Path.GetFileName(filePath);
                    var newTab = new TabViewModel(filePath, title, processedEntries.ToList()); // Use ToList() for safety if processedEntries is modified elsewhere
                    FileTabs.Add(newTab);
                    SelectedTab = newTab;
                    StatusMessage = $"Loaded {processedEntries.Count} log entries from {title}";
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading file: {filePath}");
                StatusMessage = $"Error loading {Path.GetFileName(filePath)}: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task LoadDirectory()
        {
            try
            {
                var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
                var dir = await _filePickerService.PickDirectoryAsync(mainWindow);
                if (string.IsNullOrEmpty(dir)) return;
                await LoadDirectoryAsync(dir);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading directory");
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusMessage = $"Error: {ex.Message}";
                    IsLoading = false;
                });
            }
        }
        
        private async Task LoadDirectoryAsync(string dir)
        {
            _logger.LogInformation("Attempting to load directory: {DirectoryPath}", dir);
            StatusMessage = $"Opening directory {Path.GetFileName(dir)}...";
            IsLoading = true;
            FileStatus = $"Dir: {Path.GetFileName(dir)}";
            IsDashboardVisible = true;

            try
            {
                var files = Directory.EnumerateFiles(dir, "*.txt", SearchOption.TopDirectoryOnly).Take(10).ToList();

                if (!files.Any())
                {
                    _logger.LogWarning("No '*.txt' files found in directory: {DirectoryPath}", dir);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        StatusMessage = $"No '*.txt' files found in directory {Path.GetFileName(dir)}.";
                        IsLoading = false;
                    });
                    UpdateMultiFileModeStatus();
                    _logger.LogInformation("[LoadDirectoryAsync - No files found or error block] After UpdateMultiFileModeStatus. IsMultiFileModeActive: {IsActive}", IsMultiFileModeActive);
                    UpdateAllErrorLogEntries();
                    return;
                }

                _logger.LogInformation("Found {FileCount} '*.txt' files in {DirectoryPath}. Loading up to 10.", files.Count, dir);
                
                foreach (var file in files)
                {
                    if (FileTabs.Any(tab => tab.FilePath == file))
                    {
                        _logger.LogInformation("File {FilePath} is already open. Selecting existing tab.", file);
                        var existingTab = FileTabs.First(tab => tab.FilePath == file);
                        await Dispatcher.UIThread.InvokeAsync(() => SelectedTab = existingTab);
                        continue;
                    }
                    
                    _logger.LogInformation("Loading file {FilePath} from directory {DirectoryPath}", file, dir);
                    await LoadFileToTab(file);
                }
                
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var successfullyLoadedTabsCount = files.Count(f => FileTabs.Any(t => t.FilePath == f));
                    StatusMessage = $"Finished loading files from directory {Path.GetFileName(dir)}. Loaded {successfullyLoadedTabsCount} new tab(s).";
                    IsLoading = false; 
                    
                    var lastAddedTabFromDir = FileTabs.LastOrDefault(t => files.Contains(t.FilePath));
                    if (lastAddedTabFromDir != null)
                    {
                        SelectedTab = lastAddedTabFromDir;
                    }
                    else if (!FileTabs.Any() && SelectedTab == null)
                    {
                        FileStatus = "No file selected";
                        IsDashboardVisible = false;
                    }
                    UpdateMultiFileModeStatus();
                    _logger.LogInformation("[LoadDirectoryAsync - No files found or error block] After UpdateMultiFileModeStatus. IsMultiFileModeActive: {IsActive}", IsMultiFileModeActive);
                    UpdateAllErrorLogEntries();
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied to directory: {DirectoryPath}", dir);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusMessage = $"Error: Access denied to {Path.GetFileName(dir)}.";
                    IsLoading = false;
                    UpdateMultiFileModeStatus();
                    _logger.LogInformation("[LoadDirectoryAsync - UnauthorizedAccessException] After UpdateMultiFileModeStatus. IsMultiFileModeActive: {IsActive}", IsMultiFileModeActive);
                    UpdateAllErrorLogEntries();
                });
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogError(ex, "Directory not found: {DirectoryPath}", dir);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusMessage = $"Error: Directory {Path.GetFileName(dir)} not found.";
                    IsLoading = false;
                    UpdateMultiFileModeStatus();
                    _logger.LogInformation("[LoadDirectoryAsync - DirectoryNotFoundException] After UpdateMultiFileModeStatus. IsMultiFileModeActive: {IsActive}", IsMultiFileModeActive);
                    UpdateAllErrorLogEntries();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing directory {DirectoryPath}", dir);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusMessage = $"Error processing directory {Path.GetFileName(dir)}: {ex.Message}";
                    IsLoading = false;
                    UpdateMultiFileModeStatus();
                    _logger.LogInformation("[LoadDirectoryAsync - Exception] After UpdateMultiFileModeStatus. IsMultiFileModeActive: {IsActive}", IsMultiFileModeActive);
                    UpdateAllErrorLogEntries();
                });
            }
        }
        
        [RelayCommand]
        private void CloseTab(TabViewModel tab)
        {
            if (tab == null) return;
            
            var tabIndex = FileTabs.IndexOf(tab);
            FileTabs.Remove(tab);
            
            if (SelectedTab == tab)
            {
                SelectedTab = FileTabs.Count > tabIndex ? FileTabs[tabIndex] : FileTabs.LastOrDefault();
            }

            if (SelectedTab == null && FileTabs.Any())
            {
                 SelectedTab = FileTabs.FirstOrDefault();
            }
            else if (!FileTabs.Any())
            {
                 SelectedTab = null;
            }

            if (SelectedTab != null) {
                foreach (var t in FileTabs)
                {
                    t.IsSelected = (t == SelectedTab);
                }
            }
            
            UpdateMultiFileModeStatus();
            _logger.LogInformation("[CloseTab] After UpdateMultiFileModeStatus. IsMultiFileModeActive: {IsActive}", IsMultiFileModeActive);
            UpdateAllErrorLogEntries();
            _logger.LogInformation("Closed tab: {TabTitle}. Remaining tabs: {Count}", tab.Title, FileTabs.Count);
        }

        [RelayCommand]
        private void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
            _logger.LogInformation("Theme changed to: {Theme}", IsDarkTheme ? "Dark" : "Light");
        }

        private void UpdateErrorLogEntries()
        {
            var errors = LogEntries.Where(e => e.Level.Equals("Error", StringComparison.OrdinalIgnoreCase)).ToList();
            ErrorLogEntries = errors;
            _logger.LogInformation("Updated ErrorLogEntries collection with {Count} entries", ErrorLogEntries.Count);
            OnPropertyChanged(nameof(ErrorLogEntries));
        }

        private void UpdateLogStatistics()
        {
            if (SelectedTab == null)
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
                ClearAllCharts();
                return;
            }

            if (SelectedTab.IsThisTabStandard)
            {
                var entriesToAnalyze = SelectedTab.LogEntries; // Or FilteredLogEntries if standard filters are applied at TabViewModel level
                if (entriesToAnalyze == null || !entriesToAnalyze.Any())
                {
                    // Same clearing logic as if SelectedTab was null
                    ErrorCount = 0; WarningCount = 0; InfoCount = 0; OtherCount = 0;
                    ErrorPercent = 0; WarningPercent = 0; InfoPercent = 0; OtherPercent = 0;
                    LogStatistics = new LogStatistics();
                    ClearAllCharts();
                    return;
                }

                // Existing logic for standard logs
                var stats = CalculateStatisticsAndCharts(entriesToAnalyze);
                ErrorCount = stats.ErrorCount;
                WarningCount = stats.WarningCount;
                InfoCount = stats.InfoCount;
                OtherCount = stats.OtherCount;
                ErrorPercent = stats.ErrorPercent;
                WarningPercent = stats.WarningPercent;
                InfoPercent = stats.InfoPercent;
                OtherPercent = stats.OtherPercent;
                LogStatistics = stats.LogStatistics;
                LevelsOverTimeSeries = stats.LevelsOverTimeSeries;
                TopErrorsSeries = stats.TopErrorsSeries;
                LogDistributionSeries = stats.LogDistributionSeries;
                TimeHeatmapSeries = stats.TimeHeatmapSeries;
                ErrorTrendSeries = stats.ErrorTrendSeries;
                SourcesDistributionSeries = stats.SourcesDistributionSeries;
                TimeAxis = stats.TimeAxis;
                CountAxis = stats.CountAxis;
                DaysAxis = stats.DaysAxis;
                HoursAxis = stats.HoursAxis;
                SourceAxis = stats.SourceAxis;
                ErrorMessageAxis = stats.ErrorMessageAxis;
            }
            else if (SelectedTab.IsThisTabIIS)
            {
                ErrorCount = SelectedTab.IIS_ErrorCount;
                InfoCount = SelectedTab.IIS_InfoCount;
                WarningCount = 0; // No warnings for IIS logs for now
                OtherCount = SelectedTab.IIS_RedirectCount; // Redirects as "Other"

                int totalIISEntries = SelectedTab.IIS_TotalCount;
                if (totalIISEntries > 0)
                {
                    ErrorPercent = (double)ErrorCount / totalIISEntries * 100;
                    InfoPercent = (double)InfoCount / totalIISEntries * 100;
                    WarningPercent = 0;
                    OtherPercent = (double)OtherCount / totalIISEntries * 100;
                }
                else
                {
                    ErrorPercent = 0; InfoPercent = 0; WarningPercent = 0; OtherPercent = 0;
                }

                // Clear or reset standard log statistics and charts
                LogStatistics = new LogStatistics(); // Reset to default or create an IIS specific one later
                ClearAllCharts();
            }
            else
            {
                 // Should not happen if SelectedTab is not null, but as a fallback:
                ErrorCount = 0; WarningCount = 0; InfoCount = 0; OtherCount = 0;
                ErrorPercent = 0; WarningPercent = 0; InfoPercent = 0; OtherPercent = 0;
                LogStatistics = new LogStatistics();
                ClearAllCharts();
            }
        }

        private void ClearAllCharts()
        {
            LevelsOverTimeSeries = Array.Empty<ISeries>();
            TopErrorsSeries = Array.Empty<ISeries>();
            LogDistributionSeries = Array.Empty<ISeries>();
            TimeHeatmapSeries = Array.Empty<ISeries>();
            ErrorTrendSeries = Array.Empty<ISeries>();
            SourcesDistributionSeries = Array.Empty<ISeries>();
            // Reset Axes if necessary, or they might adjust automatically with empty series
        }

        private (int ErrorCount, int WarningCount, int InfoCount, int OtherCount, double ErrorPercent, double WarningPercent, double InfoPercent, double OtherPercent, ISeries[] LevelsOverTimeSeries, ISeries[] TopErrorsSeries, ISeries[] LogDistributionSeries, ISeries[] TimeHeatmapSeries, ISeries[] ErrorTrendSeries, ISeries[] SourcesDistributionSeries, LogStatistics LogStatistics, Axis[] TimeAxis, Axis[] CountAxis, Axis[] DaysAxis, Axis[] HoursAxis, Axis[] SourceAxis, Axis[] ErrorMessageAxis) CalculateStatisticsAndCharts(List<LogEntry> logEntries)
        {
            int errorCount = logEntries.Count(e => e.Level.Equals("Error", StringComparison.OrdinalIgnoreCase));
            int warningCount = logEntries.Count(e => e.Level == "WARNING");
            int infoCount = logEntries.Count(e => e.Level == "INFO");
            int otherCount = logEntries.Count - errorCount - warningCount - infoCount;
            int total = logEntries.Count;
            double errorPercent = total > 0 ? Math.Round((double)errorCount / total * 100, 1) : 0;
            double warningPercent = total > 0 ? Math.Round((double)warningCount / total * 100, 1) : 0;
            double infoPercent = total > 0 ? Math.Round((double)infoCount / total * 100, 1) : 0;
            double otherPercent = total > 0 ? Math.Round((double)otherCount / total * 100, 1) : 0;
            var logStats = new LogStatistics
            {
                TotalCount = total,
                ErrorCount = errorCount,
                WarningCount = warningCount,
                InfoCount = infoCount,
                OtherCount = otherCount,
                ErrorPercent = errorPercent,
                WarningPercent = warningPercent,
                InfoPercent = infoPercent,
                OtherPercent = otherPercent
            };
            // 1. Log Type Distribution chart (Pie chart)
            var logDistributionSeries = new ISeries[]
            {
                new PieSeries<double>
                {
                    Values = new double[] { errorCount },
                    Name = "Errors",
                    Fill = new SolidColorPaint(SKColors.Crimson),
                    InnerRadius = 50,
                    MaxRadialColumnWidth = 50,
                    DataLabelsSize = 16,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = point => errorCount > 0 && total > 0 ? $"Errors: {point.Coordinate.PrimaryValue}\n({Math.Round(point.Coordinate.PrimaryValue / total * 100)}%)" : "",
                    IsVisible = total > 0
                },
                new PieSeries<double>
                {
                    Values = new double[] { warningCount },
                    Name = "Warnings",
                    Fill = new SolidColorPaint(SKColors.DarkOrange),
                    InnerRadius = 50,
                    MaxRadialColumnWidth = 50,
                    DataLabelsSize = 16,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = point => warningCount > 0 && total > 0 ? $"Warnings: {point.Coordinate.PrimaryValue}\n({Math.Round(point.Coordinate.PrimaryValue / total * 100)}%)" : "",
                    IsVisible = total > 0
                },
                new PieSeries<double>
                {
                    Values = new double[] { infoCount },
                    Name = "Info",
                    Fill = new SolidColorPaint(SKColors.RoyalBlue),
                    InnerRadius = 50,
                    MaxRadialColumnWidth = 50,
                    DataLabelsSize = 16,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = point => infoCount > 0 && total > 0 ? $"Info: {point.Coordinate.PrimaryValue}\n({Math.Round(point.Coordinate.PrimaryValue / total * 100)}%)" : "",
                    IsVisible = total > 0
                },
                new PieSeries<double>
                {
                    Values = new double[] { otherCount },
                    Name = "Others",
                    Fill = new SolidColorPaint(SKColors.DarkGray),
                    InnerRadius = 50,
                    MaxRadialColumnWidth = 50,
                    DataLabelsSize = 16,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = point => otherCount > 0 && total > 0 ? $"Others: {point.Coordinate.PrimaryValue}\n({Math.Round(point.Coordinate.PrimaryValue / total * 100)}%)" : "",
                    IsVisible = total > 0
                }
            };
            // 2. Logs By Hour - (Line chart)
            var actualTimeGroups = logEntries
                .GroupBy(e => new { Date = e.Timestamp.Date, Hour = e.Timestamp.Hour, Minute = e.Timestamp.Minute / 10 * 10 })
                .OrderBy(g => g.Key.Date)
                .ThenBy(g => g.Key.Hour)
                .ThenBy(g => g.Key.Minute)
                .ToList();
            var minTimestamp = logEntries.Any() ? logEntries.Min(e => e.Timestamp) : DateTime.Now.AddHours(-1);
            var maxTimestamp = logEntries.Any() ? logEntries.Max(e => e.Timestamp) : DateTime.Now;
            if ((maxTimestamp - minTimestamp).TotalMinutes < 60)
            {
                maxTimestamp = minTimestamp.AddHours(1);
            }
            var timePoints = new List<DateTime>();
            var tickInterval = DetermineOptimalTimeInterval(minTimestamp, maxTimestamp);
            var currentTime = new DateTime(minTimestamp.Year, minTimestamp.Month, minTimestamp.Day,
                                          minTimestamp.Hour, minTimestamp.Minute / tickInterval * tickInterval, 0);
            while (currentTime <= maxTimestamp.AddMinutes(tickInterval))
            {
                timePoints.Add(currentTime);
                currentTime = currentTime.AddMinutes(tickInterval);
            }
            var errorsByTime = new List<DateTimePoint>();
            var warningsByTime = new List<DateTimePoint>();
            var infosByTime = new List<DateTimePoint>();
            var totalByTime = new List<DateTimePoint>();
            foreach (var time in timePoints)
            {
                var endTime = time.AddMinutes(tickInterval);
                var entries = logEntries.Where(e => e.Timestamp >= time && e.Timestamp < endTime).ToList();
                var errorC = entries.Count(e => e.Level.Equals("Error", StringComparison.OrdinalIgnoreCase));
                var warningC = entries.Count(e => e.Level == "WARNING");
                var infoC = entries.Count(e => e.Level == "INFO");
                var totalC = entries.Count;
                errorsByTime.Add(new DateTimePoint(time, errorC));
                warningsByTime.Add(new DateTimePoint(time, warningC));
                infosByTime.Add(new DateTimePoint(time, infoC));
                totalByTime.Add(new DateTimePoint(time, totalC));
            }
            List<string> timeLabels = new List<string>();
            string format = DetermineTimeFormat(minTimestamp, maxTimestamp, tickInterval);
            var timeAxis = new[]
            {
                new Axis
                {
                    Name = "Время",
                    NamePaint = new SolidColorPaint(SKColors.LightGray),
                    LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                    TextSize = 12,
                    Labeler = value =>
                    {
                        try { return new DateTime((long)value).ToString(format); } catch { return string.Empty; }
                    },
                    UnitWidth = TimeSpan.FromMinutes(tickInterval).Ticks,
                    MinStep = TimeSpan.FromMinutes(tickInterval).Ticks,
                    SeparatorsPaint = new SolidColorPaint(SKColors.LightSlateGray) { StrokeThickness = 0.5f },
                    ShowSeparatorLines = true
                }
            };
            var countAxis = new[] { new Axis { Name = "Count", MinLimit = 0 } };
            var daysAxis = new[] { new Axis { Name = "Days", Labels = new List<string>() } };
            var hoursAxis = new[] { new Axis { Name = "Hours", Labels = new List<string>() { "00:00", "04:00", "08:00", "12:00", "16:00", "20:00", "24:00" } } };
            var sourceAxis = new[] { new Axis { Name = "Source", LabelsRotation = 15, Labels = new List<string>() } };
            var errorMessageAxis = new[] { new Axis { LabelsRotation = 15, Name = "Error Message" } };
            var levelsOverTimeSeries = new ISeries[]
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
            int maxValue = totalByTime.Any() ? totalByTime.Select(p => (int)p.Value).Max() : 0;
            var timeHeatData = totalByTime.Select(p => p.Value).ToArray();
            var timeHeatmapSeries = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = timeHeatData,
                    Name = "Activity",
                    Fill = new LinearGradientPaint(
                        new[] {
                            new SKColor(65, 105, 225, 80),
                            new SKColor(65, 105, 225, 140),
                            new SKColor(65, 105, 225, 200),
                            new SKColor(65, 105, 225, 255)
                        },
                        new SKPoint(0, 1),
                        new SKPoint(0, 0)
                    ),
                    Stroke = null,
                    MaxBarWidth = double.MaxValue,
                    IgnoresBarPosition = true
                }
            };
            var errorTrend = errorsByTime.Select(p => p.Value).ToArray();
            var errorTrendSeries = errorTrend.Any(v => v > 0) ? new ISeries[]
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
            var topErrors = logEntries
                .Where(e => e.Level.Equals("Error", StringComparison.OrdinalIgnoreCase))
                .GroupBy(e => e.Message)
                .Select(g => new { Message = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToList();
            if (topErrors.Any())
            {
                var values = topErrors.Select(e => (double)e.Count).ToArray();
                var labels = topErrors.Select(e => TruncateMessage(e.Message, 30)).ToList();
                errorMessageAxis[0].Labels = labels;
                errorMessageAxis[0].Name = "Error Messages";
                try
                {
                    errorMessageAxis[0].TextSize = 11;
                    errorMessageAxis[0].LabelsRotation = 25;
                }
                catch { }
                var topErrorsSeries = new ISeries[]
                {
                    new ColumnSeries<double>
                    {
                        Values = values,
                        Name = "Count",
                        Fill = new LinearGradientPaint(
                            new[] {
                                new SKColor(220, 53, 69, 190),
                                new SKColor(220, 53, 69, 230)
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
                return (errorCount, warningCount, infoCount, otherCount, errorPercent, warningPercent, infoPercent, otherPercent, levelsOverTimeSeries, topErrorsSeries, logDistributionSeries, timeHeatmapSeries, errorTrendSeries, Array.Empty<ISeries>(), logStats, timeAxis, countAxis, daysAxis, hoursAxis, sourceAxis, errorMessageAxis);
            }
            else
            {
                errorMessageAxis[0].Labels = new List<string>();
                errorMessageAxis[0].Name = "Error Messages";
                return (errorCount, warningCount, infoCount, otherCount, errorPercent, warningPercent, infoPercent, otherPercent, levelsOverTimeSeries, Array.Empty<ISeries>(), logDistributionSeries, timeHeatmapSeries, errorTrendSeries, Array.Empty<ISeries>(), logStats, timeAxis, countAxis, daysAxis, hoursAxis, sourceAxis, errorMessageAxis);
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
        private async Task ApplyFilters()
        {
            if (LogEntries.Count == 0)
            {
                StatusMessage = "No log entries to filter";
                return;
            }
            if (FilterCriteria.Count == 0 || FilterCriteria.Any(c => string.IsNullOrWhiteSpace(c.SelectedField) || string.IsNullOrWhiteSpace(c.SelectedOperator) || c.Value == null))
            {
                StatusMessage = "Please configure all filter criteria completely";
                // Optionally, if no filters are defined, show all logs
                if (FilterCriteria.Count == 0)
                {
                    FilteredLogEntries = LogEntries.ToList();
                     _logger.LogDebug("No filters defined, showing all {Count} log entries.", LogEntries.Count);
                    OnPropertyChanged(nameof(FilteredLogEntries));
                    StatusMessage = "Displaying all log entries.";
                }
                return;
            }
            StatusMessage = "Applying filters...";
            IsLoading = true;
            try
            {
                var entriesToFilter = LogEntries;
                await Task.Run(() =>
                {
                    IEnumerable<LogEntry> currentlyFiltered = entriesToFilter;
                    foreach (var criterion in FilterCriteria)
                    {
                        if (string.IsNullOrWhiteSpace(criterion.SelectedField) ||
                            string.IsNullOrWhiteSpace(criterion.SelectedOperator) ||
                            criterion.Value == null) // Allow empty string for value, but not null if operator needs it
                        {
                            _logger.LogWarning("Skipping incomplete filter criterion: Field='{Field}', Operator='{Operator}', Value='{Value}'",
                                criterion.SelectedField, criterion.SelectedOperator, criterion.Value);
                            continue;
                        }
                        currentlyFiltered = ApplySingleFilterCriterion(currentlyFiltered, criterion);
                    }
                    var filteredEntriesList = currentlyFiltered.ToList();
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        FilteredLogEntries = filteredEntriesList;
                        StatusMessage = $"Filters applied. Displaying {FilteredLogEntries.Count} entries.";
                        _logger.LogInformation("Filters applied. Displaying {Count} entries.", FilteredLogEntries.Count);
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying filters");
                StatusMessage = $"Error applying filters: {ex.Message}";
            }
            finally
            {
                Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
            }
        }

        [RelayCommand]
        private async Task ResetFilters()
        {
            if (LogEntries.Count == 0)
            {
                StatusMessage = "No log entries to reset filters on.";
                return;
            }
            StatusMessage = "Resetting filters...";
            IsLoading = true;
            try
            {
                await Task.Run(() =>
                {
                    var allEntries = LogEntries.ToList();
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        FilterCriteria.Clear(); // Also clear the criteria themselves
                        FilteredLogEntries = allEntries;
                        StatusMessage = $"Filters reset. Displaying {FilteredLogEntries.Count} entries.";
                         _logger.LogInformation("Filters reset. Displaying {Count} log entries.", FilteredLogEntries.Count);
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting filters");
                StatusMessage = $"Error resetting filters: {ex.Message}";
            }
            finally
            {
                 Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
            }
        }

        private IEnumerable<LogEntry> ApplySingleFilterCriterion(IEnumerable<LogEntry> entries, FilterCriterion criterion)
        {
            _logger.LogDebug("ApplySingleFilterCriterion called. Field: {Field}, Operator: {Operator}, Value: '{Value}'. Initial entry count: {InitialCount}", 
                             criterion.SelectedField, criterion.SelectedOperator, criterion.Value, entries.Count());

            if (string.IsNullOrWhiteSpace(criterion.SelectedField) || 
                string.IsNullOrWhiteSpace(criterion.SelectedOperator) ||
                criterion.Value == null) 
            {
                _logger.LogWarning("Incomplete criterion. Field: {Field}, Op: {Op}, Val: {Val}. Returning original entries.", 
                                 criterion.SelectedField, criterion.SelectedOperator, criterion.Value);
                return entries;
            }

            Func<LogEntry, string?> getEntryValue;
            switch (criterion.SelectedField)
            {
                case "Timestamp":
                    getEntryValue = e => e.Timestamp.ToString("o");
                    _logger.LogTrace("Using Timestamp field. Formatted entry value example: {ExampleVal}", entries.FirstOrDefault()?.Timestamp.ToString("o"));
                    break;
                case "Level":
                    getEntryValue = e => e.Level;
                    _logger.LogTrace("Using Level field. Entry value example: {ExampleVal}", entries.FirstOrDefault()?.Level);
                    break;
                case "Message":
                    getEntryValue = e => e.Message;
                    _logger.LogTrace("Using Message field.");
                    break;
                case "Source":
                    getEntryValue = e => e.Source;
                     _logger.LogTrace("Using Source field.");
                    break;
                case "RawData":
                    getEntryValue = e => e.RawData;
                    _logger.LogTrace("Using RawData field.");
                    break;
                case "CorrelationId":
                    getEntryValue = e => e.CorrelationId;
                    _logger.LogTrace("Using CorrelationId field.");
                    break;
                case "ErrorType":
                    getEntryValue = e => e.ErrorType;
                    _logger.LogTrace("Using ErrorType field.");
                    break;
                default:
                    _logger.LogWarning("Unsupported field for filtering: {Field}. Returning original entries.", criterion.SelectedField);
                    return entries;
            }

            var filterValue = criterion.Value;
            _logger.LogTrace("Filter value to compare: '{FilterVal}'", filterValue);

            IEnumerable<LogEntry> result = entries; // Default to original if no specific operator matches

            if (criterion.SelectedField == "Timestamp" && DateTime.TryParse(filterValue, out var dateValue))
            {
                _logger.LogTrace("Timestamp field with parsable date: {DateVal}", dateValue);
                result = criterion.SelectedOperator switch
                {
                    "Equals" => entries.Where(e => e.Timestamp.Date == dateValue.Date),
                    "Before" => entries.Where(e => e.Timestamp < dateValue),
                    "After" => entries.Where(e => e.Timestamp > dateValue),
                    _ => entries 
                };
                _logger.LogDebug("Timestamp operator '{Operator}' applied. Result count: {Count}", criterion.SelectedOperator, result.Count());
                return result;
            }
            
            _logger.LogTrace("Applying general string-based operator: {Operator}", criterion.SelectedOperator);
            result = criterion.SelectedOperator switch
            {
                "Equals" => entries.Where(e => {
                    var entryVal = getEntryValue(e) ?? string.Empty;
                    bool comparisonResult = entryVal.Equals(filterValue, StringComparison.OrdinalIgnoreCase);
                    // _logger.LogTrace("Comparing (Equals): '{EntryVal}' with '{FilterVal}' -> {Result}", entryVal, filterValue, comparisonResult); // Can be too verbose
                    return comparisonResult;
                }),
                "Not Equals" => entries.Where(e => !(getEntryValue(e) ?? string.Empty).Equals(filterValue, StringComparison.OrdinalIgnoreCase)),
                "Contains" => entries.Where(e => (getEntryValue(e) ?? string.Empty).Contains(filterValue, StringComparison.OrdinalIgnoreCase)),
                "StartsWith" => entries.Where(e => (getEntryValue(e) ?? string.Empty).StartsWith(filterValue, StringComparison.OrdinalIgnoreCase)),
                "EndsWith" => entries.Where(e => (getEntryValue(e) ?? string.Empty).EndsWith(filterValue, StringComparison.OrdinalIgnoreCase)),
                "Regex Not Contains" => entries.Where(e => {
                    var entryVal = getEntryValue(e) ?? string.Empty;
                    try { return !System.Text.RegularExpressions.Regex.IsMatch(entryVal, filterValue, System.Text.RegularExpressions.RegexOptions.IgnoreCase); }
                    catch (ArgumentException ex) { _logger.LogWarning(ex, "Invalid regex pattern for Not Contains: {Pattern}", filterValue); return true; }
                }),
                "Regex" => entries.Where(e => { 
                    var entryVal = getEntryValue(e) ?? string.Empty;
                    try { return System.Text.RegularExpressions.Regex.IsMatch(entryVal, filterValue, System.Text.RegularExpressions.RegexOptions.IgnoreCase); }
                    catch (ArgumentException ex) { _logger.LogWarning(ex, "Invalid regex pattern for Regex: {Pattern}", filterValue); return false; }
                }),
                _ => entries
            };
            _logger.LogDebug("Operator '{Operator}' for field '{Field}' applied. Result count: {Count}", criterion.SelectedOperator, criterion.SelectedField, result.Count());
            return result;
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

        [RelayCommand]
        private async Task LoadIISLogFile()
        {
            try
            {
                var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
                if (mainWindow == null) return;

                var files = await _filePickerService.PickFilesAsync(mainWindow);
                if (files == null || !files.Any())
                {
                    _logger.LogInformation("IIS Log file selection cancelled.");
                    return;
                }

                IsLoading = true;
                StatusMessage = $"Loading {files.Count()} IIS log file(s)...";

                foreach (var filePath in files)
                {
                    if (string.IsNullOrEmpty(filePath))
                        continue;

                    if (FileTabs.Any(tab => tab.FilePath == filePath))
                    {
                        _logger.LogInformation("File {FilePath} is already open. Selecting existing tab.", filePath);
                        SelectedTab = FileTabs.First(tab => tab.FilePath == filePath);
                        continue;
                    }

                    var tabTitle = Path.GetFileName(filePath) + " (IIS)";
                    
                    List<IISLogEntry> parsedEntries = new List<IISLogEntry>();
                    try
                    {
                        await foreach (var entry in _iisLogParserService.ParseLogFileAsync(filePath, CancellationToken.None))
                        {
                            parsedEntries.Add(entry);
                        }
                        
                        _logger.LogInformation("Successfully parsed {Count} IIS log entries from {FilePath}", parsedEntries.Count, filePath);
                        
                        if (parsedEntries.Any())
                        {
                            var newTab = new TabViewModel(filePath, tabTitle, parsedEntries);
                            FileTabs.Add(newTab);
                            SelectedTab = newTab;
                            StatusMessage = $"Loaded {parsedEntries.Count} IIS log entries from {Path.GetFileName(filePath)}.";
                        }
                        else
                        {
                            StatusMessage = $"No IIS log entries found or parsed in {Path.GetFileName(filePath)}.";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing IIS log file {FilePath}", filePath);
                        StatusMessage = $"Error parsing {Path.GetFileName(filePath)}: {ex.Message}";
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in LoadIISLogFile command.");
                StatusMessage = $"An unexpected error occurred: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                UpdateMultiFileModeStatus();
                UpdateAllErrorLogEntries();
            }
        }

        [RelayCommand]
        private async Task ShowFilePickerContextMenu()
        {
            try
            {
                var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
                var result = await _filePickerService.ShowFilePickerContextMenuAsync(mainWindow);
                if (result.Files != null && result.Files.Any())
                {
                    await LoadFilesAsync(result.Files);
                }
                else if (!string.IsNullOrEmpty(result.Directory))
                {
                    await LoadDirectoryAsync(result.Directory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing file picker context menu");
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private void SelectTab(TabViewModel tab)
        {
            if (tab == null) return;
            
            foreach (var t in FileTabs)
            {
                t.IsSelected = (t == tab);
            }
            
            SelectedTab = tab;
        }

        [RelayCommand]
        private void ToggleDashboardVisibility()
        {
            IsDashboardVisible = !IsDashboardVisible;
            _logger.LogInformation("Видимость дашборда изменена на: {Visibility}", IsDashboardVisible);
        }

        private void UpdateMultiFileModeStatus()
        {
            var previousState = IsMultiFileModeActive;
            IsMultiFileModeActive = FileTabs.Count > 1;
            _logger.LogInformation("UpdateMultiFileModeStatus executed. FileTabs.Count: {Count}. IsMultiFileModeActive changed from {Previous} to {Current}", 
                                 FileTabs.Count, previousState, IsMultiFileModeActive);
        }

        private void UpdateAllErrorLogEntries()
        {
            AllErrorLogEntries.Clear();
            if (IsMultiFileModeActive)
            {
                _logger.LogDebug("[UpdateAllErrorLogEntries] Starting to collect errors. FileTabs.Count: {FileTabsCount}", FileTabs.Count);
                var allErrors = new List<LogEntry>();
                foreach (var tab in FileTabs)
                {
                    _logger.LogDebug("[UpdateAllErrorLogEntries] Processing tab: '{TabTitle}'. Total entries in this tab: {LogEntriesCount}", tab.Title, tab.LogEntries.Count);
                    var tabErrors = tab.LogEntries.Where(e => e.Level.Equals("Error", StringComparison.OrdinalIgnoreCase)).ToList();
                    _logger.LogDebug("[UpdateAllErrorLogEntries] Found {ErrorCount} errors in tab: '{TabTitle}'", tabErrors.Count, tab.Title);
                    foreach (var error in tabErrors)
                    {
                        error.SourceTabTitle = tab.Title; // Set the source tab title
                        allErrors.Add(error);
                    }
                }
                _logger.LogDebug("[UpdateAllErrorLogEntries] Total errors collected from all tabs: {TotalErrorCount}", allErrors.Count);
                allErrors.Sort((x, y) => x.Timestamp.CompareTo(y.Timestamp));
                foreach (var error in allErrors)
                {
                    AllErrorLogEntries.Add(error);
                }
            }
            _logger.LogInformation("AllErrorLogEntries updated. Count: {Count}. Active: {IsMultiFileModeActive}", AllErrorLogEntries.Count, IsMultiFileModeActive);
            OnPropertyChanged(nameof(AllErrorLogEntries)); // Notify UI about changes to this collection
        }

        [RelayCommand]
        private void AddFilterCriterion()
        {
            var newCriterion = new FilterCriterion
            {
                ParentViewModel = SelectedTab,
                AvailableFields = new ObservableCollection<string>(_masterAvailableFields) 
                // AvailableOperators will be populated by FilterCriterion based on SelectedField and ParentViewModel.OperatorsByFieldType
                // AvailableValues will be populated by FilterCriterion based on SelectedField and ParentViewModel.AvailableValuesByField
            };
            if (newCriterion.AvailableFields.Any())
            {
                newCriterion.SelectedField = newCriterion.AvailableFields.First(); // Auto-select first field
            }
            
            if (SelectedTab != null)
            {
                SelectedTab.FilterCriteria.Add(newCriterion);
                _logger.LogDebug("Added new filter criterion to selected tab. Total criteria: {Count}", SelectedTab.FilterCriteria.Count);
            }
            else
            {
                FilterCriteria.Add(newCriterion);
                _logger.LogDebug("Added new filter criterion to main view (no tab selected). Total criteria: {Count}", FilterCriteria.Count);
            }
        }

        [RelayCommand]
        private async Task RemoveFilterCriterion(FilterCriterion? criterion)
        {
            if (criterion != null)
            {
                if (SelectedTab != null && criterion.ParentViewModel == SelectedTab)
                {
                    SelectedTab.FilterCriteria.Remove(criterion);
                    SelectedTab.ApplyFiltersCommand?.Execute(null); // Re-apply filters after removing one
                    _logger.LogDebug("Removed filter criterion from selected tab. Total criteria: {Count}", SelectedTab.FilterCriteria.Count);
                }
                else
                {
                    FilterCriteria.Remove(criterion);
                    await ApplyFilters(); // Re-apply filters after removing one
                    _logger.LogDebug("Removed filter criterion from main view. Total criteria: {Count}", FilterCriteria.Count);
                }
            }
            else
            {
                _logger.LogWarning("Attempted to remove a null filter criterion.");
            }
        }
    }
}
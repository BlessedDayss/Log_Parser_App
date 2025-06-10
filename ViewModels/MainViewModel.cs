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
using System.Text.RegularExpressions;

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
        private bool _isIISDashboardVisible = false;

        [ObservableProperty]
        private bool _isStandardDashboardVisible = false;

        [ObservableProperty]
        private bool _isStartScreenVisible = true;

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
            // Используем новую логику из Program.cs
            var startupFilePath = Program.StartupFilePath;

            if (!string.IsNullOrEmpty(startupFilePath))
            {
                _logger.LogInformation("Loading file from command line arguments: {FilePath}", startupFilePath);
                LastOpenedFilePath = startupFilePath;

                // Делаем небольшую задержку перед загрузкой файла
                Task.Delay(500).ContinueWith(async _ =>
                {
                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        try
                        {
                            // Всегда используем стандартный парсер для всех файлов
                            _logger.LogInformation("Loading standard log file: {FilePath}", startupFilePath);
                            await LoadFileAsync(startupFilePath);
                            
                            // Check which tab was selected after loading
                            if (SelectedTab != null)
                            {
                                _logger.LogInformation("After loading file {FilePath}, selected tab LogType: {LogType}, IsThisTabIIS: {IsIIS}, IsThisTabStandard: {IsStandard}",
                                    startupFilePath, SelectedTab.LogType, SelectedTab.IsThisTabIIS, SelectedTab.IsThisTabStandard);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to load startup file: {FilePath}", startupFilePath);
                            StatusMessage = $"Error loading startup file: {ex.Message}";
                        }
                    });
                });
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

                // Объявляем переменные для подсчета в правильной области видимости
                int failedEntriesCount = 0;
                int processedEntriesCount = 0;
                
                // Выполняем парсинг полностью отдельно от UI-потока
                var entries = await Task.Run(async () =>
                {
                    try
                    {
                        _logger.LogDebug("PERF: Начало парсинга файла {FilePath}", filePath);
                        var parseStopwatch = System.Diagnostics.Stopwatch.StartNew();
                        
                        var entriesList = new List<LogEntry>();
                        
                        try
                        {
                            await foreach (var entryValue in _logParserService.ParseLogFileAsync(filePath, CancellationToken.None))
                            {
                                try
                                {
                                    entriesList.Add(entryValue);
                                    processedEntriesCount++;
                                }
                                catch (Exception entryEx)
                                {
                                    failedEntriesCount++;
                                    _logger.LogWarning(entryEx, "Failed to process individual log entry from line {LineNumber}, continuing with next entry. Failed entries so far: {FailedCount}", 
                                        entryValue?.LineNumber ?? -1, failedEntriesCount);
                                    // Continue processing without breaking the loop
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Exception occurred during log parsing enumeration for file {FilePath}. Successfully processed {ProcessedCount} entries, failed {FailedCount} entries before enumeration exception.", 
                                filePath, processedEntriesCount, failedEntriesCount);
                            // Continue with the entries we've already collected
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
                                string mainLine = lines.FirstOrDefault(l => !l.TrimStart().StartsWith("at ")) ?? (lines.Length > 0 ? lines[0] : string.Empty);
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
                                if (recommendation.Recommendations != null)
                                {
                                    entry.ErrorRecommendations.AddRange(recommendation.Recommendations);
                                }
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
                    // Create a new tab instead of just setting LogEntries
                    var title = Path.GetFileName(filePath);
                    var newTab = new TabViewModel(filePath, title, processedEntries.ToList());
                    
                    // Add debug logging to check tab type
                    _logger.LogInformation("Created new tab in LoadFileAsync for file {FilePath}. LogType: {LogType}, IsThisTabIIS: {IsIIS}, IsThisTabStandard: {IsStandard}", 
                        filePath, newTab.LogType, newTab.IsThisTabIIS, newTab.IsThisTabStandard);
                    
                    // Clear existing tabs and add the new one
                    FileTabs.Clear();
                    FileTabs.Add(newTab);
                    SelectedTab = newTab;
                    
                    // Also update the old LogEntries for compatibility
                    LogEntries = processedEntries.ToList(); 
                    FilteredLogEntries = processedEntries.ToList(); 
                    UpdateErrorLogEntries();
                    UpdateLogStatistics();
                    _logger.LogDebug("PERF: Завершение загрузки данных в UI");
                    var totalAttemptedEntries = processedEntriesCount + failedEntriesCount;
                    var successRate = totalAttemptedEntries > 0 ? Math.Round((double)processedEntriesCount / totalAttemptedEntries * 100, 1) : 100.0;
                    
                    _logger.LogInformation("Загружено {ProcessedCount} из {TotalCount} записей логов за {ElapsedMs}ms (успешность: {SuccessRate}%). Ошибок парсинга: {FailedCount}", 
                        LogEntries.Count, totalAttemptedEntries, sw.ElapsedMilliseconds, successRate, failedEntriesCount);
                    
                    StatusMessage = failedEntriesCount > 0 
                        ? $"Loaded {LogEntries.Count} log entries ({successRate}% success rate, {failedEntriesCount} parsing errors)"
                        : $"Loaded {LogEntries.Count} log entries (100% success rate)";
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
                if (files != null && files.Any())
                {
                await LoadFilesAsync(files);
                    IsStartScreenVisible = false;
                    IsStandardDashboardVisible = true;
                    IsIISDashboardVisible = false;
                }
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
                int failedEntriesCount = 0;
                int processedEntriesCount = 0;
                
                try
                {
                    await foreach (var entryValue in _logParserService.ParseLogFileAsync(filePath, CancellationToken.None))
                    {
                        try
                        {
                            entriesList.Add(entryValue);
                            processedEntriesCount++;
                        }
                        catch (Exception entryEx)
                        {
                            failedEntriesCount++;
                            _logger.LogWarning(entryEx, "Failed to process individual log entry from line {LineNumber} in tab {FilePath}, continuing with next entry. Failed entries so far: {FailedCount}", 
                                entryValue?.LineNumber ?? -1, filePath, failedEntriesCount);
                            // Continue processing without breaking the loop
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception occurred during log parsing enumeration for tab file {FilePath}. Successfully processed {ProcessedCount} entries, failed {FailedCount} entries before enumeration exception.", 
                        filePath, processedEntriesCount, failedEntriesCount);
                    // Continue with the entries we've already collected
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
                    
                    // Add debug logging to check tab type
                    _logger.LogInformation("Created new tab for file {FilePath}. LogType: {LogType}, IsThisTabIIS: {IsIIS}, IsThisTabStandard: {IsStandard}", 
                        filePath, newTab.LogType, newTab.IsThisTabIIS, newTab.IsThisTabStandard);
                    
                    FileTabs.Add(newTab);
                    SelectedTab = newTab;
                    var totalAttemptedEntries = processedEntriesCount + failedEntriesCount;
                    var successRate = totalAttemptedEntries > 0 ? Math.Round((double)processedEntriesCount / totalAttemptedEntries * 100, 1) : 100.0;
                    
                    StatusMessage = failedEntriesCount > 0 
                        ? $"Loaded {processedEntries.Count} log entries from {title} ({successRate}% success rate, {failedEntriesCount} parsing errors)"
                        : $"Loaded {processedEntries.Count} log entries from {title} (100% success rate)";
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
                IsIISDashboardVisible = false;
                IsStandardDashboardVisible = false;
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
                    IsIISDashboardVisible = false;
                    IsStandardDashboardVisible = true;
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
                
                IsIISDashboardVisible = false;
                IsStandardDashboardVisible = true;
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
                    ErrorPercent = Math.Round((double)ErrorCount / totalIISEntries * 100, 1);
                    InfoPercent = Math.Round((double)InfoCount / totalIISEntries * 100, 1);
                    WarningPercent = 0;
                    OtherPercent = Math.Round((double)OtherCount / totalIISEntries * 100, 1);
                    
                    LogStatistics = new LogStatistics
                    {
                        TotalCount = totalIISEntries,
                        ErrorCount = ErrorCount,
                        InfoCount = InfoCount,
                        WarningCount = 0,
                        OtherCount = OtherCount,
                        ErrorPercent = ErrorPercent,
                        InfoPercent = InfoPercent,
                        WarningPercent = 0,
                        OtherPercent = OtherPercent
                    };
                    
                    // Generate IIS-specific charts
                    CalculateIISCharts();
                }
                else
                {
                    ErrorPercent = 0; InfoPercent = 0; WarningPercent = 0; OtherPercent = 0;
                    LogStatistics = new LogStatistics();
                    ClearAllCharts();
                }

                IsIISDashboardVisible = true;
                IsStandardDashboardVisible = false;
            }
            else
            {
                 // Should not happen if SelectedTab is not null, but as a fallback:
                ErrorCount = 0; WarningCount = 0; InfoCount = 0; OtherCount = 0;
                ErrorPercent = 0; WarningPercent = 0; InfoPercent = 0; OtherPercent = 0;
                LogStatistics = new LogStatistics();
                ClearAllCharts();
                IsIISDashboardVisible = false;
                IsStandardDashboardVisible = false;
            }
        }

        private void CalculateIISCharts()
        {
            if (SelectedTab == null || !SelectedTab.IsThisTabIIS || SelectedTab.FilteredIISLogEntries.Count == 0)
            {
                ClearAllCharts();
                return;
            }
            
            var iisEntries = SelectedTab.FilteredIISLogEntries.ToList();
            
            // 1. Log Type Distribution chart (Pie chart) для IIS логов
            LogDistributionSeries = new ISeries[]
            {
                new PieSeries<double>
                {
                    Values = new double[] { ErrorCount },
                    Name = "Errors (4xx-5xx)",
                    Fill = new SolidColorPaint(SKColors.Crimson),
                    InnerRadius = 50,
                    MaxRadialColumnWidth = 50,
                    DataLabelsSize = 16,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = point => ErrorCount > 0 ? $"Errors: {ErrorCount}\n({ErrorPercent:0.0}%)" : "",
                    IsVisible = ErrorCount > 0
                },
                new PieSeries<double>
                {
                    Values = new double[] { InfoCount },
                    Name = "Success (2xx)",
                    Fill = new SolidColorPaint(SKColors.RoyalBlue),
                    InnerRadius = 50,
                    MaxRadialColumnWidth = 50,
                    DataLabelsSize = 16,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = point => InfoCount > 0 ? $"Success: {InfoCount}\n({InfoPercent:0.0}%)" : "",
                    IsVisible = InfoCount > 0
                },
                new PieSeries<double>
                {
                    Values = new double[] { OtherCount },
                    Name = "Redirects (3xx)",
                    Fill = new SolidColorPaint(SKColors.DarkGray),
                    InnerRadius = 50,
                    MaxRadialColumnWidth = 50,
                    DataLabelsSize = 16,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = point => OtherCount > 0 ? $"Redirects: {OtherCount}\n({OtherPercent:0.0}%)" : "",
                    IsVisible = OtherCount > 0
                }
            };
            
            // 2. HTTP Status Code Distribution (bar chart)
            var statusCodeGroups = iisEntries
                .GroupBy(e => e.HttpStatus)
                .OrderBy(g => g.Key)
                .Select(g => new { StatusCode = g.Key, Count = g.Count() })
                .ToList();
            
            if (statusCodeGroups.Any())
            {
                var statusCodeValues = new List<double>();
                var statusLabels = new List<string>();
                var statusColors = new List<SolidColorPaint>();
                
                foreach (var group in statusCodeGroups)
                {
                    statusCodeValues.Add(group.Count);
                    statusLabels.Add(group.StatusCode.ToString());
                    
                    // Цвета по категориям статус-кодов
                    if (group.StatusCode >= 500)
                        statusColors.Add(new SolidColorPaint(SKColors.DarkRed));
                    else if (group.StatusCode >= 400)
                        statusColors.Add(new SolidColorPaint(SKColors.Crimson));
                    else if (group.StatusCode >= 300)
                        statusColors.Add(new SolidColorPaint(SKColors.DarkGray));
                    else if (group.StatusCode >= 200)
                        statusColors.Add(new SolidColorPaint(SKColors.RoyalBlue));
                    else
                        statusColors.Add(new SolidColorPaint(SKColors.DimGray));
                }
                
                TopErrorsSeries = new ISeries[]
                {
                    new ColumnSeries<double>
                    {
                        Values = statusCodeValues.ToArray(),
                        Name = "HTTP Status Codes",
                        Fill = new SolidColorPaint(SKColors.DodgerBlue),
                        Stroke = null,
                        Padding = 5,
                        DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                        DataLabelsFormatter = point => $"{statusLabels[(int)point.Index]}: {point.Coordinate.PrimaryValue}",
                        DataLabelsPaint = new SolidColorPaint(SKColors.White)
                    }
                };
                
                ErrorMessageAxis = new Axis[]
                {
                    new Axis
                    {
                        Labels = statusLabels.ToArray(),
                        LabelsRotation = 0,
                        Padding = new LiveChartsCore.Drawing.Padding(15),
                        SeparatorsPaint = new SolidColorPaint(SKColors.LightSlateGray) { StrokeThickness = 1 }
                    }
                };
            }
            else
            {
                TopErrorsSeries = Array.Empty<ISeries>();
            }
            
            // Временная активность по часам, как и для обычных логов
            if (iisEntries.Any(e => e.DateTime.HasValue))
            {
                var timeGroups = iisEntries
                    .Where(e => e.DateTime.HasValue)
                    .GroupBy(e => new { 
                        Hour = e.DateTime!.Value.Hour, 
                        IsError = e.HttpStatus >= 400 
                    })
                    .Select(g => new { g.Key.Hour, g.Key.IsError, Count = g.Count() })
                    .ToList();
                
                var hours = Enumerable.Range(0, 24).ToList();
                var errorByHour = new double[24];
                var successByHour = new double[24];
                
                foreach (var group in timeGroups)
                {
                    if (group.IsError)
                        errorByHour[group.Hour] += group.Count;
                    else
                        successByHour[group.Hour] += group.Count;
                }
                
                TimeHeatmapSeries = new ISeries[]
                {
                    new ColumnSeries<double>
                    {
                        Values = successByHour,
                        Name = "Success",
                        Fill = new SolidColorPaint(SKColors.RoyalBlue),
                        Stroke = null
                    },
                    new ColumnSeries<double>
                    {
                        Values = errorByHour,
                        Name = "Errors",
                        Fill = new SolidColorPaint(SKColors.Crimson),
                        Stroke = null
                    }
                };
                
                HoursAxis = new Axis[]
                {
                    new Axis
                    {
                        Labels = hours.Select(h => h.ToString("00") + ":00").ToArray(),
                        LabelsRotation = 45,
                        Padding = new LiveChartsCore.Drawing.Padding(15),
                        SeparatorsPaint = new SolidColorPaint(SKColors.LightSlateGray) { StrokeThickness = 1 }
                    }
                };
            }
            else
            {
                TimeHeatmapSeries = Array.Empty<ISeries>();
            }
            
            // Прочие графики оставляем пустыми
            LevelsOverTimeSeries = Array.Empty<ISeries>();
            ErrorTrendSeries = Array.Empty<ISeries>();
            SourcesDistributionSeries = Array.Empty<ISeries>();
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
            int maxValue = totalByTime.Any() ? totalByTime.Select(p => (int)(p.Value ?? 0)).Max() : 0;
            var timeHeatData = totalByTime.Select(p => p.Value ?? 0).ToArray();
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
            if (SelectedTab == null) return;

            await Task.Run(() =>
            {
                IEnumerable<LogEntry> currentFiltered = SelectedTab.LogEntries;

                // First, apply the "Errors only" filter if it's active.
                if (SelectedTab.IsErrorsOnly) // Assuming you add this property to TabViewModel
                {
                    currentFiltered = currentFiltered.Where(e => e.Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase));
                }

                // Then, apply the user-defined criteria.
                foreach (var criterion in SelectedTab.FilterCriteria)
                {
                    if (criterion.IsActive && !string.IsNullOrEmpty(criterion.SelectedField) && !string.IsNullOrEmpty(criterion.SelectedOperator))
                    {
                        currentFiltered = ApplySingleFilterCriterion(currentFiltered, criterion);
                    }
                }

                var filteredList = currentFiltered.ToList();
                
                Dispatcher.UIThread.Invoke(() =>
                {
                    SelectedTab.FilteredLogEntries.Clear();
                    foreach (var entry in filteredList)
                    {
                        SelectedTab.FilteredLogEntries.Add(entry);
                    }
                });
            });
            UpdateLogStatistics(); // Make sure this is called to refresh UI.
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
                    Dispatcher.UIThread.Invoke(() =>
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
                 await Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
            }
        }

        private IEnumerable<LogEntry> ApplySingleFilterCriterion(IEnumerable<LogEntry> entries, FilterCriterion criterion)
        {
            return entries.Where(entry =>
            {
                var value = GetLogEntryPropertyValue(entry, criterion.SelectedField);
                if (value == null) return false;

                switch (criterion.SelectedOperator)
                {
                    case "Equals":
                        return string.Equals(value, criterion.Value, StringComparison.OrdinalIgnoreCase);
                    case "Not Equals":
                        return !string.Equals(value, criterion.Value, StringComparison.OrdinalIgnoreCase);
                    case "Contains":
                        return value.Contains(criterion.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                    case "Regex":
                        if (string.IsNullOrEmpty(criterion.Value)) return true;
                        try
                        {
                            return System.Text.RegularExpressions.Regex.Match(value, criterion.Value, System.Text.RegularExpressions.RegexOptions.IgnoreCase).Success;
                        }
                        catch (System.Text.RegularExpressions.RegexParseException)
                        {
                            return false;
                        }
                    case "Regex Not Contains":
                        if (string.IsNullOrEmpty(criterion.Value)) return true;
                        try
                        {
                            return !System.Text.RegularExpressions.Regex.Match(value, criterion.Value, System.Text.RegularExpressions.RegexOptions.IgnoreCase).Success;
                        }
                        catch (System.Text.RegularExpressions.RegexParseException)
                        {
                            return false;
                        }

                    default:
                        return false;
                }
            });
        }

        private string? GetLogEntryPropertyValue(LogEntry entry, string? fieldName)
        {
            if (string.IsNullOrEmpty(fieldName)) return null;

            switch (fieldName)
            {
                case "Timestamp":
                    return entry.Timestamp.ToString("o");
                case "Level":
                    return entry.Level;
                case "Message":
                    return entry.Message;
                case "Source":
                    return entry.Source;
                case "RawData":
                    return entry.RawData;
                case "CorrelationId":
                    return entry.CorrelationId;
                case "ErrorType":
                    return entry.ErrorType;
                default:
                    return null;
            }
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
            if (SelectedTab == null) return;
            
            var newCriterion = new FilterCriterion
            {
                ParentViewModel = SelectedTab,
            };

            foreach (var field in SelectedTab.MasterAvailableFields)
            {
                newCriterion.AvailableFields.Add(field);
            }

            SelectedTab.FilterCriteria.Add(newCriterion);
        }

        [RelayCommand]
        private async Task RemoveFilterCriterion(FilterCriterion? criterion)
        {
            if (SelectedTab == null || criterion == null) return;
            SelectedTab.FilterCriteria.Remove(criterion);
            await ApplyFilters(); // Re-apply filters after removing a criterion
        }

        [RelayCommand]
        private void ShowStandardLogSection()
        {
            IsStartScreenVisible = false;
            IsStandardDashboardVisible = true;
            IsIISDashboardVisible = false;
        }

        [RelayCommand]
        private void ShowIISLogSection()
        {
            IsStartScreenVisible = false;
            IsStandardDashboardVisible = false;
            IsIISDashboardVisible = true;
        }

        [RelayCommand]
        private void ShowStartScreen()
        {
            IsStartScreenVisible = true;
            IsStandardDashboardVisible = false;
            IsIISDashboardVisible = false;
        }
    }
}
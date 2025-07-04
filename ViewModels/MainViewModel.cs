namespace Log_Parser_App.ViewModels
{
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
    using System.Threading;
    using Log_Parser_App.Interfaces;
    using Avalonia.Controls.ApplicationLifetimes;
    using Avalonia;

    public partial class MainViewModel : ViewModelBase
    {
        private readonly ILogParserService _logParserService;
        private readonly ILogger<MainViewModel> _logger;
        private readonly IFileService _fileService;
        private readonly ISimpleErrorRecommendationService _simpleErrorRecommendationService;
        private readonly IFilePickerService _filePickerService;
        private readonly IIISLogParserService _iisLogParserService;
        private readonly IRabbitMqLogParserService _rabbitMqLogParserService;
        
        // Phase 2 SOLID refactored services
        private readonly IChartService _chartService;
        private readonly ITabManagerService _tabManagerService;
        private readonly IFilterService _filterService;
        
        // IIS Analytics Service
        private readonly StatisticsViewModel _statisticsViewModel;
        
        // RabbitMQ Dashboard Analytics Service (RDB-003)
        private readonly RabbitMQDashboardViewModel _rabbitMqDashboardViewModel;
        
        // Error Detection Service
        	private readonly Log_Parser_App.Services.ErrorDetection.IErrorDetectionService _errorDetectionService;
	private readonly IFileTypeDetectionService _fileTypeDetectionService;

        [ObservableProperty]
        private string _statusMessage = "Ready to work";

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isLoadingDirectory;

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
        public ObservableCollection<TabViewModel> FileTabs {
            get => _fileTabs;
            set {
                _fileTabs = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<LogEntry> AllErrorLogEntries { get; } = new();

        private TabViewModel? _selectedTab;
        public TabViewModel? SelectedTab {
            get => _selectedTab;
            set {
                if (_selectedTab != null) {
                    _selectedTab.PropertyChanged -= SelectedTab_PropertyChanged;
                }

                if (SetProperty(ref _selectedTab, value)) {
                    OnPropertyChanged(nameof(IsCurrentTabIIS));
                    if (_selectedTab != null) {
                        _selectedTab.PropertyChanged += SelectedTab_PropertyChanged;
                        FilePath = _selectedTab.FilePath;
                        FileStatus = _selectedTab.Title;

                        IsStartScreenVisible = false;

                        // Update error entries for the new tab
                        UpdateErrorLogEntries();

                        // UpdateLogStatistics will be called due to PropertyChanged event if counts change, 
                        // or immediately if the tab type dictates a full refresh.
                    } else {
                        FilePath = string.Empty;
                        FileStatus = "No file selected";
                        IsStartScreenVisible = !FileTabs.Any();
                    }
                    UpdateLogStatistics(); // Call this to refresh stats when tab selection changes
                }
            }
        }

        private void SelectedTab_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (SelectedTab != null && SelectedTab.IsThisTabIIS) {
                if (e.PropertyName == nameof(TabViewModel.IIS_TotalCount) || e.PropertyName == nameof(TabViewModel.IIS_ErrorCount) ||
                    e.PropertyName == nameof(TabViewModel.IIS_InfoCount) || e.PropertyName == nameof(TabViewModel.IIS_RedirectCount)) {
                    UpdateLogStatistics();
                }
            }
            // For standard logs, existing mechanisms (e.g., ApplyFiltersCommand directly calling UpdateLogStatistics) should handle updates.
            // Or, if TabViewModel for standard logs also exposed aggregated counts via PropertyChanged, we could listen here too.
        }

        // Property to indicate if the current tab is an IIS log
        public bool IsCurrentTabIIS => SelectedTab?.LogType == LogFormatType.IIS;

        private List<LogEntry> _logEntries = new();
        public List<LogEntry> LogEntries {
            get => _logEntries;
            set {
                _logEntries = value;
                OnPropertyChanged();
            }
        }

        private List<LogEntry> _filteredLogEntries = new();
        public List<LogEntry> FilteredLogEntries {
            get => _filteredLogEntries;
            set {
                _filteredLogEntries = value;
                OnPropertyChanged();
            }
        }

        private List<LogEntry> _errorLogEntries = new();
        public List<LogEntry> ErrorLogEntries {
            get => _errorLogEntries;
            set {
                _errorLogEntries = value;
                OnPropertyChanged();
            }
        }

        [ObservableProperty]
        private LogStatistics _logStatistics = new();

        // Statistics ViewModel for dashboard analytics
        public StatisticsViewModel Statistics => _statisticsViewModel;

        // RabbitMQ Dashboard Analytics ViewModel (RDB-003)
        public RabbitMQDashboardViewModel RabbitMQDashboard => _rabbitMqDashboardViewModel;



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
        private int _uniqueProcessUIDCount;

        [ObservableProperty]
        private int _totalCount;

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
        public Dictionary<string, List<string>> OperatorsByFieldType { get; } = new() {
            { "Timestamp", new List<string> { "Equals", "Before", "After", "Between" } },
            { "Level", new List<string> { "Equals", "Not Equals" } },
            { "Message", new List<string> { "Contains", "Equals", "StartsWith", "EndsWith", "Regex Not Contains" } },
            { "Source", new List<string> { "Equals", "Contains" } },
            { "RawData", new List<string> { "Contains", "Regex" } },
            { "CorrelationId", new List<string> { "Equals" } },
            { "ErrorType", new List<string> { "Equals", "Contains" } }
        };
        public Dictionary<string, ObservableCollection<string>> AvailableValuesByField { get; } = new() {
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
        public string? LastOpenedFilePath {
            get => _lastOpenedFilePath;
            set => SetProperty(ref _lastOpenedFilePath, value);
        }

        		public MainViewModel(
			ILogParserService logParserService,
			ILogger<MainViewModel> logger,
			IFileService fileService,
			ISimpleErrorRecommendationService simpleErrorRecommendationService,
			IFilePickerService filePickerService,
			IIISLogParserService iisLogParserService,
			IRabbitMqLogParserService rabbitMqLogParserService,
			IChartService chartService,
			ITabManagerService tabManagerService,
			IFilterService filterService,
			Log_Parser_App.Services.ErrorDetection.IErrorDetectionService errorDetectionService,
			IFileTypeDetectionService fileTypeDetectionService,
			StatisticsViewModel statisticsViewModel,
			RabbitMQDashboardViewModel rabbitMqDashboardViewModel) {
            _logParserService = logParserService;
            _logger = logger;
            _fileService = fileService;
            _simpleErrorRecommendationService = simpleErrorRecommendationService;
            _filePickerService = filePickerService;
            _iisLogParserService = iisLogParserService;
            _rabbitMqLogParserService = rabbitMqLogParserService;
            _chartService = chartService;
            _tabManagerService = tabManagerService;
            _filterService = filterService;

            _errorDetectionService = errorDetectionService;
            _fileTypeDetectionService = fileTypeDetectionService;
            _statisticsViewModel = statisticsViewModel;
            _rabbitMqDashboardViewModel = rabbitMqDashboardViewModel;

            InitializeErrorRecommendationService();

            // Subscribe to service events
            _tabManagerService.TabChanged += OnTabChanged;
            _tabManagerService.TabClosed += OnTabClosed;
            // TODO: Refactor to use new FilteringViewModel events
            // _filterService.FiltersApplied += OnFiltersApplied;
            // _filterService.FiltersReset += OnFiltersReset;

            // Проверяем аргументы командной строки для автоматической загрузки файла
            CheckCommandLineArgs();

            _logger.LogInformation("MainViewModel initialized with SOLID refactored services");
        }

        private async void InitializeErrorRecommendationService() {
            try {
                await _simpleErrorRecommendationService.LoadAsync();
                _logger.LogInformation("Simple error recommendation service initialized");
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to initialize simple error recommendation service");
            }
        }

        private void CheckCommandLineArgs() {
            // Используем новую логику из Program.cs
            var startupFilePath = Program.StartupFilePath;

            if (!string.IsNullOrEmpty(startupFilePath)) {
                _logger.LogInformation("Loading file from command line arguments: {FilePath}", startupFilePath);
                LastOpenedFilePath = startupFilePath;

                // Делаем небольшую задержку перед загрузкой файла
                Task.Delay(500).ContinueWith(async _ => {
                    await Dispatcher.UIThread.InvokeAsync(async () => {
                        try {
                            // Всегда используем стандартный парсер для всех файлов
                            _logger.LogInformation("Loading standard log file: {FilePath}", startupFilePath);
                            await LoadFileAsync(startupFilePath);

                            // Check which tab was selected after loading
                            if (SelectedTab != null) {
                                _logger.LogInformation("After loading file {FilePath}, selected tab LogType: {LogType}, IsThisTabIIS: {IsIIS}, IsThisTabStandard: {IsStandard}",
                                    startupFilePath,
                                    SelectedTab.LogType,
                                    SelectedTab.IsThisTabIIS,
                                    SelectedTab.IsThisTabStandard);
                            }
                        } catch (Exception ex) {
                            _logger.LogError(ex, "Failed to load startup file: {FilePath}", startupFilePath);
                            StatusMessage = $"Error loading startup file: {ex.Message}";
                        }
                    });
                });
            }
        }

        private async Task LoadFileAsync(string filePath) {
            try {
                StatusMessage = $"Opening {Path.GetFileName(filePath)}...";
                IsLoading = true;
                FileStatus = Path.GetFileName(filePath);
                _logger.LogInformation("PERF: Начало загрузки файла {FilePath}", filePath);
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // Determine file type by checking first few lines
                var isIISLog = await IsIISLogFileAsync(filePath);
                _logger.LogInformation("File {FilePath} detected as {LogType}", filePath, isIISLog ? "IIS" : "Standard");

                if (isIISLog) {
                    await LoadIISFileAsync(filePath);
                    return;
                }

                await Dispatcher.UIThread.InvokeAsync(() => {
                        LogEntries.Clear();
                        FilteredLogEntries.Clear();
                        ErrorLogEntries.Clear();
                    },
                    DispatcherPriority.Background);

                // Объявляем переменные для подсчета в правильной области видимости
                int failedEntriesCount = 0;
                int processedEntriesCount = 0;

                // Выполняем парсинг полностью отдельно от UI-потока
                var entries = await Task.Run(async () => {
                    try {
                        _logger.LogDebug("PERF: Начало парсинга файла {FilePath}", filePath);
                        var parseStopwatch = System.Diagnostics.Stopwatch.StartNew();

                        var entriesList = new List<LogEntry>();

                        try {
                            await foreach (var entryValue in _logParserService.ParseLogFileAsync(filePath, CancellationToken.None)) {
                                try {
                                    entriesList.Add(entryValue);
                                    processedEntriesCount++;
                                } catch (Exception entryEx) {
                                    failedEntriesCount++;
                                    _logger.LogWarning(entryEx,
                                        "Failed to process individual log entry from line {LineNumber}, continuing with next entry. Failed entries so far: {FailedCount}",
                                        entryValue?.LineNumber ?? -1,
                                        failedEntriesCount);
                                    // Continue processing without breaking the loop
                                }
                            }
                        } catch (Exception ex) {
                            _logger.LogError(ex,
                                "Exception occurred during log parsing enumeration for file {FilePath}. Successfully processed {ProcessedCount} entries, failed {FailedCount} entries before enumeration exception.",
                                filePath,
                                processedEntriesCount,
                                failedEntriesCount);
                            // Continue with the entries we've already collected
                        }
                        var logEntriesResult = entriesList; // Use this variable below

                        _logger.LogDebug("PERF: Парсинг файла завершен за {ElapsedMs}ms", parseStopwatch.ElapsedMilliseconds);
                        return logEntriesResult;
                    } catch (Exception ex) {
                        _logger.LogError(ex, "Ошибка при парсинге файла {FilePath}", filePath);
                        throw;
                    }
                });

                // var logEntries = entries as LogEntry[] ?? entries.ToArray(); // Old line
                var loadedLogEntries = entries; // 'entries' is now the List<LogEntry> from Task.Run

                _logger.LogDebug("PERF: Начало предварительной обработки {Count} записей", loadedLogEntries.Count);

                var processedEntries = await Task.Run(() => {
                    var processed = new List<LogEntry>(loadedLogEntries.Count);
                    foreach (var entry in loadedLogEntries) // Use loadedLogEntries here
                    {
                        // Analyze error BEFORE any message cleanup
                        if (entry.Level.Equals("Error", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(entry.Message)) {
                            // Use RawData for analysis if available (contains full original text), otherwise Message
                            string textForAnalysis = !string.IsNullOrEmpty(entry.RawData) ? entry.RawData : entry.Message;
                            var simpleResult = _simpleErrorRecommendationService.AnalyzeError(textForAnalysis);
                            if (simpleResult != null) {
                                entry.ErrorType = "PatternMatch";
                                entry.ErrorDescription = simpleResult.Message;
                                entry.ErrorRecommendations.Clear();
                                entry.ErrorRecommendations.Add(simpleResult.Fix);
                                entry.Recommendation = simpleResult.Fix;
                            } else {
                                entry.ErrorType = "UnknownError";
                                entry.ErrorDescription = "Unknown error pattern";
                                entry.ErrorRecommendations.Clear();
                                entry.ErrorRecommendations.Add("Please contact developer to add this error pattern");
                                entry.Recommendation = "Please contact developer to add this error pattern";
                            }
                        }

                        // Now clean up message for display
                        if (!string.IsNullOrEmpty(entry.Message)) {
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
                    return processed;
                });

                _logger.LogDebug("PERF: Обработка рекомендаций завершена");

                // Обновляем UI и статистику после полной загрузки
                await Dispatcher.UIThread.InvokeAsync(() => {
                    // Create a new tab instead of just setting LogEntries
                    var title = Path.GetFileName(filePath);
                    var newTab = new TabViewModel(filePath, title, processedEntries.ToList(), _filePickerService);

                    // Add debug logging to check tab type
                    _logger.LogInformation("Created new tab in LoadFileAsync for file {FilePath}. LogType: {LogType}, IsThisTabIIS: {IsIIS}, IsThisTabStandard: {IsStandard}",
                        filePath,
                        newTab.LogType,
                        newTab.IsThisTabIIS,
                        newTab.IsThisTabStandard);

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
                        LogEntries.Count,
                        totalAttemptedEntries,
                        sw.ElapsedMilliseconds,
                        successRate,
                        failedEntriesCount);

                    StatusMessage = failedEntriesCount > 0
                        ? $"Loaded {LogEntries.Count} log entries ({successRate}% success rate, {failedEntriesCount} parsing errors)"
                        : $"Loaded {LogEntries.Count} log entries (100% success rate)";
                    SelectedTabIndex = 0;
                    IsLoading = false;
                });
            } catch (Exception ex) {
                _logger.LogError(ex, "Ошибка загрузки файла логов");
                await Dispatcher.UIThread.InvokeAsync(() => {
                    StatusMessage = $"Error: {ex.Message}";
                    IsLoading = false;
                });
            }
        }

        [RelayCommand]
        private async Task LoadFile() {
            try {
                var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
                var files = await _filePickerService.PickFilesAsync(mainWindow);
                if (files != null && files.Any()) {
                    await LoadFilesAsync(files);
            
                    IsStartScreenVisible = false;
                }
            } catch (Exception ex) {
                _logger.LogError(ex, "Ошибка загрузки файлов логов");
                await Dispatcher.UIThread.InvokeAsync(() => {
                    StatusMessage = $"Error: {ex.Message}";
                    IsLoading = false;
                });
            }
        }

        private async Task LoadFilesAsync(IEnumerable<string> files) {
            StatusMessage = $"Opening {files.Count()} files...";
            IsLoading = true;
            FileStatus = $"{files.Count()} files";

            foreach (var file in files) {
                // Check if the file is already opened
                if (FileTabs.Any(tab => tab.FilePath == file)) {
                    var existingTab = FileTabs.First(tab => tab.FilePath == file);
                    SelectedTab = existingTab;
                    continue;
                }

                await LoadFileToTab(file);
            }

            UpdateMultiFileModeStatus();
            _logger.LogInformation("[LoadFilesAsync] After UpdateMultiFileModeStatus. IsMultiFileModeActive: {IsActive}", IsMultiFileModeActive);
            _ = Task.Run(UpdateAllErrorLogEntries);
            IsLoading = false;
            StatusMessage = $"Finished processing {files.Count()} files. {FileTabs.Count(t => files.Contains(t.FilePath))} new tab(s) added.";
            if (FileTabs.Any() && SelectedTab == null) {
                SelectedTab = FileTabs.LastOrDefault(t => files.Contains(t.FilePath)) ?? FileTabs.LastOrDefault();
            }
        }

        private async Task LoadFileToTab(string filePath) {
            try {
                var entriesList = new List<LogEntry>();
                int failedEntriesCount = 0;
                int processedEntriesCount = 0;

                try {
                    await foreach (var entryValue in _logParserService.ParseLogFileAsync(filePath, CancellationToken.None)) {
                        try {
                            entriesList.Add(entryValue);
                            processedEntriesCount++;
                        } catch (Exception entryEx) {
                            failedEntriesCount++;
                            _logger.LogWarning(entryEx,
                                "Failed to process individual log entry from line {LineNumber} in tab {FilePath}, continuing with next entry. Failed entries so far: {FailedCount}",
                                entryValue?.LineNumber ?? -1,
                                filePath,
                                failedEntriesCount);
                            // Continue processing without breaking the loop
                        }
                    }
                } catch (Exception ex) {
                    _logger.LogError(ex,
                        "Exception occurred during log parsing enumeration for tab file {FilePath}. Successfully processed {ProcessedCount} entries, failed {FailedCount} entries before enumeration exception.",
                        filePath,
                        processedEntriesCount,
                        failedEntriesCount);
                    // Continue with the entries we've already collected
                }
                // var logEntriesArr = entries as LogEntry[] ?? entries.ToArray(); // Old logic
                var logEntriesArr = entriesList.ToArray(); // New logic based on collected list

                var processedEntries = await Task.Run(() => {
                    var processed = new List<LogEntry>(logEntriesArr.Length);
                    foreach (var entry in logEntriesArr) // Iterate over the array collected from streaming
                    {
                        // Analyze error BEFORE any message cleanup
                        if (entry.Level.Equals("Error", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(entry.Message)) {
                            // Use RawData for analysis if available (contains full original text), otherwise Message
                            string textForAnalysis = !string.IsNullOrEmpty(entry.RawData) ? entry.RawData : entry.Message;
                            var simpleResult = _simpleErrorRecommendationService.AnalyzeError(textForAnalysis);
                            if (simpleResult != null) {
                                entry.ErrorType = "PatternMatch";
                                entry.ErrorDescription = simpleResult.Message;
                                entry.ErrorRecommendations.Clear();
                                entry.ErrorRecommendations.Add(simpleResult.Fix);
                                entry.Recommendation = simpleResult.Fix;
                            } else {
                                entry.ErrorType = "UnknownError";
                                entry.ErrorDescription = "Unknown error pattern";
                                entry.ErrorRecommendations.Clear();
                                entry.ErrorRecommendations.Add("Please contact developer to add this error pattern");
                                entry.Recommendation = "Please contact developer to add this error pattern";
                            }
                        }

                        // Now clean up message for display
                        if (!string.IsNullOrEmpty(entry.Message)) {
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
                    return processed;
                });

                await Dispatcher.UIThread.InvokeAsync(() => {
                    var title = Path.GetFileName(filePath);
                    var newTab = new TabViewModel(filePath, title, processedEntries.ToList(), _filePickerService); // Use ToList() for safety if processedEntries is modified elsewhere

                    // Add debug logging to check tab type
                    _logger.LogInformation("Created new tab for file {FilePath}. LogType: {LogType}, IsThisTabIIS: {IsIIS}, IsThisTabStandard: {IsStandard}",
                        filePath,
                        newTab.LogType,
                        newTab.IsThisTabIIS,
                        newTab.IsThisTabStandard);

                    FileTabs.Add(newTab);
                    SelectedTab = newTab;
                    var totalAttemptedEntries = processedEntriesCount + failedEntriesCount;
                    var successRate = totalAttemptedEntries > 0 ? Math.Round((double)processedEntriesCount / totalAttemptedEntries * 100, 1) : 100.0;

                    StatusMessage = failedEntriesCount > 0
                        ? $"Loaded {processedEntries.Count} log entries from {title} ({successRate}% success rate, {failedEntriesCount} parsing errors)"
                        : $"Loaded {processedEntries.Count} log entries from {title} (100% success rate)";
                });
            } catch (Exception ex) {
                _logger.LogError(ex, $"Error loading file: {filePath}");
                StatusMessage = $"Error loading {Path.GetFileName(filePath)}: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task LoadDirectory() {
            try {
                var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
                var dir = await _filePickerService.PickDirectoryAsync(mainWindow);
                if (string.IsNullOrEmpty(dir))
                    return;

                await LoadDirectoryAsync(dir);
            } catch (Exception ex) {
                _logger.LogError(ex, "Error loading directory");
                await Dispatcher.UIThread.InvokeAsync(() => {
                    StatusMessage = $"Error: {ex.Message}";
                    IsLoading = false;
                });
            }
        }

        private async Task LoadDirectoryAsync(string dir) {
            _logger.LogInformation("Attempting to load directory: {DirectoryPath}", dir);
            StatusMessage = $"Opening directory {Path.GetFileName(dir)}...";
            IsLoading = true;
            IsLoadingDirectory = true;
            FileStatus = $"Dir: {Path.GetFileName(dir)}";

            try {
                				var allFiles = Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
					.Where(f => new[] { ".txt", ".log", ".config", ".xml", ".csv" }
						.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
					.ToList();

				if (!allFiles.Any()) {
					_logger.LogWarning("No supported files found in directory: {Directory}", dir);
					return;
				}

				_logger.LogInformation("Found {FileCount} files in {DirectoryPath}. Analyzing file types...", allFiles.Count, dir);

				foreach (var file in allFiles) {
					if (FileTabs.Any(tab => tab.FilePath == file)) {
						_logger.LogInformation("File {FilePath} is already open. Selecting existing tab.", file);
						var existingTab = FileTabs.First(tab => tab.FilePath == file);
						await Dispatcher.UIThread.InvokeAsync(() => SelectedTab = existingTab);
						continue;
					}

					_logger.LogInformation("Loading file {FilePath} from directory {DirectoryPath}", file, dir);
					await LoadFileWithTypeDetection(file);
				}

                				await Dispatcher.UIThread.InvokeAsync(() => {
					var successfullyLoadedTabsCount = allFiles.Count(f => FileTabs.Any(t => t.FilePath == f));
					StatusMessage = $"Finished loading files from directory {Path.GetFileName(dir)}. Loaded {successfullyLoadedTabsCount} new tab(s).";
					IsLoading = false;
					IsLoadingDirectory = false;

					var lastAddedTabFromDir = FileTabs.LastOrDefault(t => allFiles.Contains(t.FilePath));
					if (lastAddedTabFromDir != null) {
						SelectedTab = lastAddedTabFromDir;
					} else if (!FileTabs.Any() && SelectedTab == null) {
                                        FileStatus = "No file selected";
					}
					UpdateMultiFileModeStatus();
					_logger.LogInformation("[LoadDirectoryAsync - Success] After UpdateMultiFileModeStatus. IsMultiFileModeActive: {IsActive}",
						IsMultiFileModeActive);
					_ = Task.Run(UpdateAllErrorLogEntries);
				});
            } catch (UnauthorizedAccessException ex) {
                _logger.LogError(ex, "Access denied to directory: {DirectoryPath}", dir);
                await Dispatcher.UIThread.InvokeAsync(() => {
                    StatusMessage = $"Error: Access denied to {Path.GetFileName(dir)}.";
                    IsLoading = false;
                    IsLoadingDirectory = false;
                    UpdateMultiFileModeStatus();
                    _logger.LogInformation("[LoadDirectoryAsync - UnauthorizedAccessException] After UpdateMultiFileModeStatus. IsMultiFileModeActive: {IsActive}", IsMultiFileModeActive);
                    _ = Task.Run(UpdateAllErrorLogEntries);
                });
            } catch (DirectoryNotFoundException ex) {
                _logger.LogError(ex, "Directory not found: {DirectoryPath}", dir);
                await Dispatcher.UIThread.InvokeAsync(() => {
                    StatusMessage = $"Error: Directory {Path.GetFileName(dir)} not found.";
                    IsLoading = false;
                    IsLoadingDirectory = false;
                    UpdateMultiFileModeStatus();
                    _logger.LogInformation("[LoadDirectoryAsync - DirectoryNotFoundException] After UpdateMultiFileModeStatus. IsMultiFileModeActive: {IsActive}", IsMultiFileModeActive);
                    _ = Task.Run(UpdateAllErrorLogEntries);
                });
            } catch (Exception ex) {
                _logger.LogError(ex, "Error processing directory {DirectoryPath}", dir);
                await Dispatcher.UIThread.InvokeAsync(() => {
                    StatusMessage = $"Error processing directory {Path.GetFileName(dir)}: {ex.Message}";
                    IsLoading = false;
                    IsLoadingDirectory = false;
                    UpdateMultiFileModeStatus();
                    _logger.LogInformation("[LoadDirectoryAsync - Exception] After UpdateMultiFileModeStatus. IsMultiFileModeActive: {IsActive}", IsMultiFileModeActive);
                    _ = Task.Run(UpdateAllErrorLogEntries);
                });
            }
        }

        [RelayCommand]
        private void CloseTab(TabViewModel tab) {
            if (tab == null)
                return;

            var tabIndex = FileTabs.IndexOf(tab);
            FileTabs.Remove(tab);

            if (SelectedTab == tab) {
                SelectedTab = FileTabs.Count > tabIndex ? FileTabs[tabIndex] : FileTabs.LastOrDefault();
            }

            if (SelectedTab == null && FileTabs.Any()) {
                SelectedTab = FileTabs.FirstOrDefault();
            } else if (!FileTabs.Any()) {
                SelectedTab = null;
            }

            if (SelectedTab != null) {
                foreach (var t in FileTabs) {
                    t.IsSelected = (t == SelectedTab);
                }
            }

            UpdateMultiFileModeStatus();
            _logger.LogInformation("[CloseTab] After UpdateMultiFileModeStatus. IsMultiFileModeActive: {IsActive}", IsMultiFileModeActive);
            UpdateAllErrorLogEntries();
            _logger.LogInformation("Closed tab: {TabTitle}. Remaining tabs: {Count}", tab.Title, FileTabs.Count);
        }

        [RelayCommand]
        private void ToggleTheme() {
            IsDarkTheme = !IsDarkTheme;
            _logger.LogInformation("Theme changed to: {Theme}", IsDarkTheme ? "Dark" : "Light");
        }

        private async void UpdateErrorLogEntries() {
            try {
                if (SelectedTab == null) {
                    ErrorLogEntries = new List<LogEntry>();
                    AllErrorLogEntries.Clear();
                    OnPropertyChanged(nameof(ErrorLogEntries));
                    _logger.LogInformation("No selected tab - clearing error collections");
                    return;
                }

                // Use entries from the selected tab, not the global LogEntries
                var tabEntries = SelectedTab.LogEntries;
                if (tabEntries == null || tabEntries.Count == 0) {
                    ErrorLogEntries = new List<LogEntry>();
                    AllErrorLogEntries.Clear();
                    OnPropertyChanged(nameof(ErrorLogEntries));
                    _logger.LogInformation("No log entries in selected tab - clearing error collections");
                    return;
                }

                _logger.LogInformation("🔍 Starting error detection for {LogType} with {Count} entries from tab '{TabTitle}'", 
                    SelectedTab.LogType, tabEntries.Count, SelectedTab.Title);

                // Use the new error detection service to detect ALL errors from tab data
                var errorEntries = await _errorDetectionService.DetectErrorsAsync(tabEntries, SelectedTab.LogType);
                var errorList = errorEntries.ToList();

                // Apply recommendations to error entries (if not already set from LoadFileAsync)
                foreach (var errorEntry in errorList) {
                    if (!string.IsNullOrEmpty(errorEntry.Message)) {
                        // Check if recommendation is already set from LoadFileAsync processing
                        if (string.IsNullOrEmpty(errorEntry.Recommendation)) {
                            var simpleResult = _simpleErrorRecommendationService.AnalyzeError(errorEntry.Message);
                            if (simpleResult != null) {
                                errorEntry.Recommendation = simpleResult.Fix;
                                _logger.LogDebug("Applied simple recommendation to error: {Message} -> {Recommendation}", 
                                    errorEntry.Message.Substring(0, Math.Min(50, errorEntry.Message.Length)), 
                                    simpleResult.Fix.Substring(0, Math.Min(100, simpleResult.Fix.Length)));
                            } else {
                                // Fallback recommendation when no specific pattern matched
                                errorEntry.Recommendation = "Please contact developer to add this error pattern";
                                _logger.LogDebug("Applied fallback recommendation to error: {Message}", 
                                    errorEntry.Message.Substring(0, Math.Min(50, errorEntry.Message.Length)));
                            }
                        } else {
                            _logger.LogDebug("Error already has recommendation: {Message}", 
                                errorEntry.Message.Substring(0, Math.Min(50, errorEntry.Message.Length)));
                        }
                    }
                }
                
                ErrorLogEntries = errorList;

                // Update the observable collection for UI binding
                AllErrorLogEntries.Clear();
                foreach (var entry in errorList) {
                    AllErrorLogEntries.Add(entry);
                }

                OnPropertyChanged(nameof(ErrorLogEntries));
                OnPropertyChanged(nameof(AllErrorLogEntries));
                
                var errorsWithRecommendations = errorList.Count(e => !string.IsNullOrEmpty(e.Recommendation));
                _logger.LogInformation("✅ Updated ErrorLogEntries collection with {Count} errors using {LogType} strategy. {RecommendationCount} errors have recommendations", 
                    ErrorLogEntries.Count, SelectedTab.LogType, errorsWithRecommendations);

                // Log first error for debugging
                if (errorList.Count > 0) {
                    var firstError = errorList.First();
                    var messagePreview = firstError.Message?.Substring(0, Math.Min(50, firstError.Message.Length)) ?? "No message";
                    var recommendationPreview = !string.IsNullOrEmpty(firstError.Recommendation) 
                        ? firstError.Recommendation.Substring(0, Math.Min(100, firstError.Recommendation.Length)) + "..."
                        : "No recommendation";
                    _logger.LogInformation("📋 First error: Level='{Level}', Message='{Message}', Recommendation='{Recommendation}'", 
                        firstError.Level, messagePreview, recommendationPreview);
                }
            } catch (Exception ex) {
                _logger.LogError(ex, "❌ Error updating error log entries using error detection service");
                
                // Fallback to empty collections on error
                ErrorLogEntries = new List<LogEntry>();
                AllErrorLogEntries.Clear();
                OnPropertyChanged(nameof(ErrorLogEntries));
            }
        }

        private void UpdateLogStatistics() {
            if (SelectedTab == null) {
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

            // Handle Standard and RabbitMQ logs (they all use standard LogEntry format)
            if (SelectedTab.IsThisTabStandardOrRabbitMQ) {
                // Special handling for RabbitMQ logs - INDEPENDENT LOGIC
                if (SelectedTab.IsThisTabRabbitMQ) {
                    var rabbitMqEntries = SelectedTab.FilteredRabbitMQLogEntries;
                    _logger.LogInformation("RabbitMQ Tab Analysis: Tab={Title}, LogType={LogType}, FilteredEntries={FilteredCount}, AllEntries={AllCount}", 
                        SelectedTab.Title, SelectedTab.LogType, rabbitMqEntries?.Count() ?? 0, SelectedTab.LogEntries?.Count ?? 0);
                    
                    if (rabbitMqEntries == null || !rabbitMqEntries.Any()) {
                        // Clear stats for empty RabbitMQ
                        ErrorCount = 0;
                        WarningCount = 0;
                        InfoCount = 0;
                        OtherCount = 0;
                        UniqueProcessUIDCount = 0;
                        TotalCount = 0;
                        ErrorPercent = 0;
                        WarningPercent = 0;
                        InfoPercent = 0;
                        OtherPercent = 0;
                        LogStatistics = new LogStatistics();
                        ClearAllCharts();
                        return;
                    }

                    // Calculate RabbitMQ-specific dashboard INDEPENDENTLY
                    TotalCount = rabbitMqEntries.Count();
                    
                    // Find ALL messages and group by identical content
                    var allMessages = rabbitMqEntries
                        .Where(e => !string.IsNullOrEmpty(e.Message))
                        .ToList();
                    
                    _logger.LogInformation("RabbitMQ Processing: Total entries={Total}, Entries with messages={WithMessages}", 
                        TotalCount, allMessages.Count);
                    
                    // Group ALL messages by identical content
                    var messageGroups = allMessages
                        .GroupBy(e => e.Message?.Trim())
                        .ToList();
                    
                    // Find groups with repeated messages (more than 1 occurrence)
                    var repeatedMessageGroups = messageGroups
                        .Where(group => group.Count() > 1) // Only repeated messages
                        .OrderByDescending(group => group.Count())
                        .ToList();
                    
                    // MESSAGE ERRORS: Total count of all repeated messages
                    ErrorCount = repeatedMessageGroups.Sum(group => group.Count());
                    
                    // PROCESS UID: Count unique ProcessUIDs that have repeated messages
                    UniqueProcessUIDCount = repeatedMessageGroups
                        .SelectMany(group => group.Where(e => !string.IsNullOrEmpty(e.EffectiveProcessUID)))
                        .Select(e => e.EffectiveProcessUID)
                        .Distinct()
                        .Count();
                    
                    // Log detailed analysis
                    _logger.LogInformation("RabbitMQ Message Analysis: TotalGroups={TotalGroups}, RepeatedGroups={RepeatedGroups}, RepeatedMessages={RepeatedMessages}", 
                        messageGroups.Count, repeatedMessageGroups.Count, ErrorCount);
                    
                    // Log top repeated message groups for debugging
                    foreach (var group in repeatedMessageGroups.Take(5))
                    {
                        _logger.LogInformation("Repeated message: '{Message}' appears {Count} times", 
                            group.Key?.Substring(0, Math.Min(150, group.Key.Length)), group.Count());
                    }
                    
                    // Calculate basic level counts independently
                    WarningCount = rabbitMqEntries.Count(e => 
                        e.Level?.ToLowerInvariant() == "warning" || 
                        e.Level?.ToLowerInvariant() == "warn");
                    
                    InfoCount = rabbitMqEntries.Count(e => 
                        e.Level?.ToLowerInvariant() == "info" || 
                        e.Level?.ToLowerInvariant() == "information");
                    
                    OtherCount = rabbitMqEntries.Count(e => 
                        !string.IsNullOrEmpty(e.Level) &&
                        e.Level.ToLowerInvariant() != "error" &&
                        e.Level.ToLowerInvariant() != "warning" &&
                        e.Level.ToLowerInvariant() != "warn" &&
                        e.Level.ToLowerInvariant() != "info" &&
                        e.Level.ToLowerInvariant() != "information");
                    
                    // Debug logging
                    _logger.LogInformation("RabbitMQ Dashboard FINAL: Total={Total}, MessageErrors={MessageErrors}, ProcessUIDs={ProcessUIDs}, Warnings={Warnings}, Info={Info}, Other={Other}", 
                        TotalCount, ErrorCount, UniqueProcessUIDCount, WarningCount, InfoCount, OtherCount);

                    // Calculate percentages
                    if (TotalCount > 0) {
                        ErrorPercent = Math.Round((double)ErrorCount / TotalCount * 100, 1);
                        WarningPercent = Math.Round((double)WarningCount / TotalCount * 100, 1);
                        InfoPercent = Math.Round((double)InfoCount / TotalCount * 100, 1);
                        OtherPercent = Math.Round((double)OtherCount / TotalCount * 100, 1);
                    } else {
                        ErrorPercent = WarningPercent = InfoPercent = OtherPercent = 0;
                    }

                    // Use enhanced RabbitMQ chart generation with improved error and user grouping
                    var chartData = _chartService.GenerateRabbitMQCharts(rabbitMqEntries);
                    LogStatistics = new LogStatistics {
                        TotalCount = TotalCount,
                        ErrorCount = ErrorCount,
                        WarningCount = WarningCount,
                        InfoCount = InfoCount,
                        OtherCount = OtherCount,
                        ErrorPercent = ErrorPercent,
                        WarningPercent = WarningPercent,
                        InfoPercent = InfoPercent,
                        OtherPercent = OtherPercent
                    };
                    
                    LevelsOverTimeSeries = chartData.LevelsOverTimeSeries;
                    TopErrorsSeries = chartData.TopErrorsSeries;
                    LogDistributionSeries = chartData.LogDistributionSeries;
                    TimeHeatmapSeries = chartData.TimeHeatmapSeries;
                    ErrorTrendSeries = chartData.ErrorTrendSeries;
                    SourcesDistributionSeries = chartData.SourcesDistributionSeries;
                    TimeAxis = chartData.TimeAxis;
                    CountAxis = chartData.CountAxis;
                    DaysAxis = chartData.DaysAxis;
                    HoursAxis = chartData.HoursAxis;
                    SourceAxis = chartData.SourceAxis;
                    ErrorMessageAxis = chartData.ErrorMessageAxis;
                    
                    // Use enhanced RabbitMQ-specific charts for error and user analysis
                    if (chartData.UserDistributionSeries != null && chartData.UserDistributionSeries.Any()) {
                        // Add user distribution charts to existing series
                        var existingSeries = SourcesDistributionSeries?.ToList() ?? new List<ISeries>();
                        existingSeries.AddRange(chartData.UserDistributionSeries);
                        SourcesDistributionSeries = existingSeries.ToArray();
                    }
                    
                    if (chartData.ErrorGroupingSeries != null && chartData.ErrorGroupingSeries.Any()) {
                        // Replace top errors with enhanced error grouping
                        TopErrorsSeries = chartData.ErrorGroupingSeries;
                    }

                    // Update RabbitMQ Dashboard Analytics (RDB-003)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _rabbitMqDashboardViewModel.RefreshAnalyticsAsync(rabbitMqEntries).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing RabbitMQ analytics for tab: {Title}", SelectedTab.Title);
                        }
                    });
                } else {
                    // Standard logs processing
                    var entriesToAnalyze = SelectedTab.LogEntries; // Or FilteredLogEntries if standard filters are applied at TabViewModel level
                    if (entriesToAnalyze == null || !entriesToAnalyze.Any()) {
                        // Same clearing logic as if SelectedTab was null
                        ErrorCount = 0;
                        WarningCount = 0;
                        InfoCount = 0;
                        OtherCount = 0;
                        UniqueProcessUIDCount = 0;
                        TotalCount = 0;
                        ErrorPercent = 0;
                        WarningPercent = 0;
                        InfoPercent = 0;
                        OtherPercent = 0;
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
                    TotalCount = entriesToAnalyze.Count;
                    UniqueProcessUIDCount = 0; // Standard logs don't have ProcessUID
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

            } else if (SelectedTab.IsThisTabIIS) {
                // Start IIS Analytics processing
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _statisticsViewModel.UpdateIISAnalyticsAsync(SelectedTab).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing IIS analytics for tab: {Title}", SelectedTab.Title);
                    }
                });

                // Set immediate basic counts for legacy compatibility
                ErrorCount = SelectedTab.IIS_ErrorCount;
                InfoCount = SelectedTab.IIS_InfoCount;
                WarningCount = 0; // No warnings for IIS logs
                OtherCount = SelectedTab.IIS_RedirectCount; // Redirects as "Other"
                UniqueProcessUIDCount = 0; // IIS logs don't have ProcessUID
                TotalCount = SelectedTab.IIS_TotalCount;

                int totalIISEntries = SelectedTab.IIS_TotalCount;
                if (totalIISEntries > 0) {
                    ErrorPercent = Math.Round((double)ErrorCount / totalIISEntries * 100, 1);
                    InfoPercent = Math.Round((double)InfoCount / totalIISEntries * 100, 1);
                    WarningPercent = 0;
                    OtherPercent = Math.Round((double)OtherCount / totalIISEntries * 100, 1);

                    LogStatistics = new LogStatistics {
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
                } else {
                    ErrorPercent = 0;
                    InfoPercent = 0;
                    WarningPercent = 0;
                    OtherPercent = 0;
                    UniqueProcessUIDCount = 0;
                    TotalCount = 0;
                    LogStatistics = new LogStatistics();
                    ClearAllCharts();
                }

            } else {
                // Should not happen if SelectedTab is not null, but as a fallback:
                ErrorCount = 0;
                WarningCount = 0;
                InfoCount = 0;
                OtherCount = 0;
                UniqueProcessUIDCount = 0;
                TotalCount = 0;
                ErrorPercent = 0;
                WarningPercent = 0;
                InfoPercent = 0;
                OtherPercent = 0;
                LogStatistics = new LogStatistics();
                ClearAllCharts();
            }
        }

        private void CalculateIISCharts() {
            if (SelectedTab == null || !SelectedTab.IsThisTabIIS || SelectedTab.FilteredIISLogEntries.Count == 0) {
                ClearAllCharts();
                return;
            }

            var iisEntries = SelectedTab.FilteredIISLogEntries.ToList();

            // 1. Log Type Distribution chart (Pie chart) для IIS логов
            LogDistributionSeries = new ISeries[] {
                new PieSeries<double> {
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
                new PieSeries<double> {
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
                new PieSeries<double> {
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
            var statusCodeGroups = iisEntries.GroupBy(e => e.HttpStatus).OrderBy(g => g.Key).Select(g => new { StatusCode = g.Key, Count = g.Count() }).ToList();

            if (statusCodeGroups.Any()) {
                var statusCodeValues = new List<double>();
                var statusLabels = new List<string>();
                var statusColors = new List<SolidColorPaint>();

                foreach (var group in statusCodeGroups) {
                    statusCodeValues.Add(group.Count);
                    statusLabels.Add(group.StatusCode?.ToString() ?? "Unknown");

                    // Цвета по категориям статус-кодов
                    var statusCode = group.StatusCode ?? 0;
                    if (statusCode >= 500)
                        statusColors.Add(new SolidColorPaint(SKColors.DarkRed));
                    else if (statusCode >= 400)
                        statusColors.Add(new SolidColorPaint(SKColors.Crimson));
                    else if (statusCode >= 300)
                        statusColors.Add(new SolidColorPaint(SKColors.DarkGray));
                    else if (statusCode >= 200)
                        statusColors.Add(new SolidColorPaint(SKColors.RoyalBlue));
                    else
                        statusColors.Add(new SolidColorPaint(SKColors.DimGray));
                }

                TopErrorsSeries = new ISeries[] {
                    new ColumnSeries<double> {
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

                ErrorMessageAxis = new Axis[] {
                    new Axis {
                        Labels = statusLabels.ToArray(),
                        LabelsRotation = 0,
                        Padding = new LiveChartsCore.Drawing.Padding(15),
                        SeparatorsPaint = new SolidColorPaint(SKColors.LightSlateGray) { StrokeThickness = 1 }
                    }
                };
            } else {
                TopErrorsSeries = Array.Empty<ISeries>();
            }

            // Временная активность по часам, как и для обычных логов
            if (iisEntries.Any(e => e.DateTime.HasValue)) {
                var timeGroups = iisEntries.Where(e => e.DateTime.HasValue).GroupBy(e => new {
                    Hour = e.DateTime!.Value.Hour,
                    IsError = (e.HttpStatus ?? 0) >= 400
                }).Select(g => new { g.Key.Hour, g.Key.IsError, Count = g.Count() }).ToList();

                var hours = Enumerable.Range(0, 24).ToList();
                var errorByHour = new double[24];
                var successByHour = new double[24];

                foreach (var group in timeGroups) {
                    if (group.IsError)
                        errorByHour[group.Hour] += group.Count;
                    else
                        successByHour[group.Hour] += group.Count;
                }

                TimeHeatmapSeries = new ISeries[] {
                    new ColumnSeries<double> {
                        Values = successByHour,
                        Name = "Success",
                        Fill = new SolidColorPaint(SKColors.RoyalBlue),
                        Stroke = null
                    },
                    new ColumnSeries<double> {
                        Values = errorByHour,
                        Name = "Errors",
                        Fill = new SolidColorPaint(SKColors.Crimson),
                        Stroke = null
                    }
                };

                HoursAxis = new Axis[] {
                    new Axis {
                        Labels = hours.Select(h => h.ToString("00") + ":00").ToArray(),
                        LabelsRotation = 45,
                        Padding = new LiveChartsCore.Drawing.Padding(15),
                        SeparatorsPaint = new SolidColorPaint(SKColors.LightSlateGray) { StrokeThickness = 1 }
                    }
                };
            } else {
                TimeHeatmapSeries = Array.Empty<ISeries>();
            }

            // Прочие графики оставляем пустыми
            LevelsOverTimeSeries = Array.Empty<ISeries>();
            ErrorTrendSeries = Array.Empty<ISeries>();
            SourcesDistributionSeries = Array.Empty<ISeries>();
        }

        private void ClearAllCharts() {
            LevelsOverTimeSeries = Array.Empty<ISeries>();
            TopErrorsSeries = Array.Empty<ISeries>();
            LogDistributionSeries = Array.Empty<ISeries>();
            TimeHeatmapSeries = Array.Empty<ISeries>();
            ErrorTrendSeries = Array.Empty<ISeries>();
            SourcesDistributionSeries = Array.Empty<ISeries>();
            // Reset Axes if necessary, or they might adjust automatically with empty series
        }

        private (int ErrorCount, int WarningCount, int InfoCount, int OtherCount, double ErrorPercent, double WarningPercent, double InfoPercent, double OtherPercent, ISeries[]
            LevelsOverTimeSeries, ISeries[] TopErrorsSeries, ISeries[] LogDistributionSeries, ISeries[] TimeHeatmapSeries, ISeries[] ErrorTrendSeries, ISeries[] SourcesDistributionSeries,
            LogStatistics LogStatistics, Axis[] TimeAxis, Axis[] CountAxis, Axis[] DaysAxis, Axis[] HoursAxis, Axis[] SourceAxis, Axis[] ErrorMessageAxis) CalculateStatisticsAndCharts(
                List<LogEntry> logEntries) {
            int errorCount = logEntries.Count(e => e.Level.Equals("Error", StringComparison.OrdinalIgnoreCase));
            int warningCount = logEntries.Count(e => e.Level == "WARNING");
            int infoCount = logEntries.Count(e => e.Level == "INFO");
            int otherCount = logEntries.Count - errorCount - warningCount - infoCount;
            int total = logEntries.Count;
            double errorPercent = total > 0 ? Math.Round((double)errorCount / total * 100, 1) : 0;
            double warningPercent = total > 0 ? Math.Round((double)warningCount / total * 100, 1) : 0;
            double infoPercent = total > 0 ? Math.Round((double)infoCount / total * 100, 1) : 0;
            double otherPercent = total > 0 ? Math.Round((double)otherCount / total * 100, 1) : 0;
            var logStats = new LogStatistics {
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
            var logDistributionSeries = new ISeries[] {
                new PieSeries<double> {
                    Values = new double[] { errorCount },
                    Name = "Errors",
                    Fill = new SolidColorPaint(SKColors.Crimson),
                    InnerRadius = 50,
                    MaxRadialColumnWidth = 50,
                    DataLabelsSize = 16,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = point =>
                        errorCount > 0 && total > 0 ? $"Errors: {point.Coordinate.PrimaryValue}\n({Math.Round(point.Coordinate.PrimaryValue / total * 100)}%)" : "",
                    IsVisible = total > 0
                },
                new PieSeries<double> {
                    Values = new double[] { warningCount },
                    Name = "Warnings",
                    Fill = new SolidColorPaint(SKColors.DarkOrange),
                    InnerRadius = 50,
                    MaxRadialColumnWidth = 50,
                    DataLabelsSize = 16,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = point => warningCount > 0 && total > 0
                        ? $"Warnings: {point.Coordinate.PrimaryValue}\n({Math.Round(point.Coordinate.PrimaryValue / total * 100)}%)"
                        : "",
                    IsVisible = total > 0
                },
                new PieSeries<double> {
                    Values = new double[] { infoCount },
                    Name = "Info",
                    Fill = new SolidColorPaint(SKColors.RoyalBlue),
                    InnerRadius = 50,
                    MaxRadialColumnWidth = 50,
                    DataLabelsSize = 16,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = point =>
                        infoCount > 0 && total > 0 ? $"Info: {point.Coordinate.PrimaryValue}\n({Math.Round(point.Coordinate.PrimaryValue / total * 100)}%)" : "",
                    IsVisible = total > 0
                },
                new PieSeries<double> {
                    Values = new double[] { otherCount },
                    Name = "Others",
                    Fill = new SolidColorPaint(SKColors.DarkGray),
                    InnerRadius = 50,
                    MaxRadialColumnWidth = 50,
                    DataLabelsSize = 16,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = point =>
                        otherCount > 0 && total > 0 ? $"Others: {point.Coordinate.PrimaryValue}\n({Math.Round(point.Coordinate.PrimaryValue / total * 100)}%)" : "",
                    IsVisible = total > 0
                }
            };
            var actualTimeGroups = logEntries.GroupBy(e => new { Date = e.Timestamp.Date, Hour = e.Timestamp.Hour, Minute = e.Timestamp.Minute / 10 * 10 }).OrderBy(g => g.Key.Date)
                .ThenBy(g => g.Key.Hour).ThenBy(g => g.Key.Minute).ToList();
            var minTimestamp = logEntries.Any() ? logEntries.Min(e => e.Timestamp) : DateTime.Now.AddHours(-1);
            var maxTimestamp = logEntries.Any() ? logEntries.Max(e => e.Timestamp) : DateTime.Now;
            if ((maxTimestamp - minTimestamp).TotalMinutes < 60) {
                maxTimestamp = minTimestamp.AddHours(1);
            }
            var timePoints = new List<DateTime>();
            int tickInterval = DetermineOptimalTimeInterval(minTimestamp, maxTimestamp);
            var currentTime = new DateTime(minTimestamp.Year, minTimestamp.Month, minTimestamp.Day, minTimestamp.Hour, minTimestamp.Minute / tickInterval * tickInterval, 0);
            while (currentTime <= maxTimestamp.AddMinutes(tickInterval)) {
                timePoints.Add(currentTime);
                currentTime = currentTime.AddMinutes(tickInterval);
            }
            var errorsByTime = new List<DateTimePoint>();
            var warningsByTime = new List<DateTimePoint>();
            var infosByTime = new List<DateTimePoint>();
            var totalByTime = new List<DateTimePoint>();
            foreach (var time in timePoints) {
                var endTime = time.AddMinutes(tickInterval);
                var entries = logEntries.Where(e => e.Timestamp >= time && e.Timestamp < endTime).ToList();
                int errorC = entries.Count(e => e.Level.Equals("Error", StringComparison.OrdinalIgnoreCase));
                int warningC = entries.Count(e => e.Level == "WARNING");
                int infoC = entries.Count(e => e.Level == "INFO");
                int totalC = entries.Count;
                errorsByTime.Add(new DateTimePoint(time, errorC));
                warningsByTime.Add(new DateTimePoint(time, warningC));
                infosByTime.Add(new DateTimePoint(time, infoC));
                totalByTime.Add(new DateTimePoint(time, totalC));
            }
            List<string> timeLabels = new List<string>();
            string format = DetermineTimeFormat(minTimestamp, maxTimestamp, tickInterval);
            var timeAxis = new[] {
                new Axis {
                    Name = "Time",
                    NamePaint = new SolidColorPaint(SKColors.LightGray),
                    LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                    TextSize = 12,
                    Labeler = value => {
                        try {
                            return new DateTime((long)value).ToString(format);
                        } catch {
                            return string.Empty;
                        }
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
            var levelsOverTimeSeries = new ISeries[] {
                new LineSeries<DateTimePoint> {
                    Values = totalByTime,
                    Name = "Все логи",
                    Stroke = new SolidColorPaint(SKColors.LightGray, 2),
                    Fill = new SolidColorPaint(SKColors.Gray.WithAlpha(40)),
                    GeometryFill = new SolidColorPaint(SKColors.White),
                    GeometryStroke = new SolidColorPaint(SKColors.Gray, 2),
                    GeometrySize = 8,
                    LineSmoothness = 0.2,
                },
                new LineSeries<DateTimePoint> {
                    Values = errorsByTime,
                    Name = "Ошибки",
                    Stroke = new SolidColorPaint(SKColors.Crimson, 3),
                    Fill = new SolidColorPaint(SKColors.Crimson.WithAlpha(40)),
                    GeometryFill = new SolidColorPaint(SKColors.White),
                    GeometryStroke = new SolidColorPaint(SKColors.Crimson, 2),
                    GeometrySize = 10,
                    LineSmoothness = 0.2,
                },
                new LineSeries<DateTimePoint> {
                    Values = warningsByTime,
                    Name = "Предупреждения",
                    Stroke = new SolidColorPaint(SKColors.Orange, 3),
                    Fill = new SolidColorPaint(SKColors.Orange.WithAlpha(40)),
                    GeometryFill = new SolidColorPaint(SKColors.White),
                    GeometryStroke = new SolidColorPaint(SKColors.Orange, 2),
                    GeometrySize = 10,
                    LineSmoothness = 0.2,
                },
                new LineSeries<DateTimePoint> {
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
            double[] timeHeatData = totalByTime.Select(p => p.Value ?? 0).ToArray();
            var timeHeatmapSeries = new ISeries[] {
                new ColumnSeries<double> {
                    Values = timeHeatData,
                    Name = "Activity",
                    Fill = new LinearGradientPaint(new[] {
                            new SKColor(65, 105, 225, 80),
                            new SKColor(65, 105, 225, 140),
                            new SKColor(65, 105, 225, 200),
                            new SKColor(65, 105, 225, 255)
                        },
                        new SKPoint(0, 1),
                        new SKPoint(0, 0)),
                    Stroke = null,
                    MaxBarWidth = double.MaxValue,
                    IgnoresBarPosition = true
                }
            };
            double?[] errorTrend = errorsByTime.Select(p => p.Value).ToArray();
            var errorTrendSeries = errorTrend.Any(v => v > 0)
                ? new ISeries[] {
                    new LineSeries<double> {
                        Values = errorTrend,
                        Name = "Error Trend",
                        Stroke = new SolidColorPaint(SKColors.Crimson, 3),
                        Fill = new SolidColorPaint(SKColors.Crimson.WithAlpha(40)),
                        GeometryFill = new SolidColorPaint(SKColors.White),
                        GeometryStroke = new SolidColorPaint(SKColors.Crimson, 2),
                        LineSmoothness = 0.8
                    }
                }
                : Array.Empty<ISeries>();
            var topErrors = logEntries.Where(e => e.Level.Equals("Error", StringComparison.OrdinalIgnoreCase)).GroupBy(e => e.Message)
                .Select(g => new { Message = g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).Take(10).ToList();
            if (topErrors.Any()) {
                double[] values = topErrors.Select(e => (double)e.Count).ToArray();
                var labels = topErrors.Select(e => TruncateMessage(e.Message, 30)).ToList();
                errorMessageAxis[0].Labels = labels;
                errorMessageAxis[0].Name = "Error Messages";
                try {
                    errorMessageAxis[0].TextSize = 11;
                    errorMessageAxis[0].LabelsRotation = 25;
                } catch {
                }
                var topErrorsSeries = new ISeries[] {
                    new ColumnSeries<double> {
                        Values = values,
                        Name = "Count",
                        Fill = new LinearGradientPaint(new[] {
                                new SKColor(220, 53, 69, 190),
                                new SKColor(220, 53, 69, 230)
                            },
                            new SKPoint(0, 0),
                            new SKPoint(0, 1)),
                        Stroke = new SolidColorPaint(SKColors.Crimson.WithAlpha(220), 2),
                        MaxBarWidth = 50,
                        DataLabelsPaint = new SolidColorPaint(SKColors.White),
                        DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                        DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue}"
                    }
                };
                return (errorCount, warningCount, infoCount, otherCount, errorPercent, warningPercent, infoPercent, otherPercent, levelsOverTimeSeries, topErrorsSeries,
                    logDistributionSeries, timeHeatmapSeries, errorTrendSeries, Array.Empty<ISeries>(), logStats, timeAxis, countAxis, daysAxis, hoursAxis, sourceAxis, errorMessageAxis);
            } else {
                errorMessageAxis[0].Labels = new List<string>();
                errorMessageAxis[0].Name = "Error Messages";
                return (errorCount, warningCount, infoCount, otherCount, errorPercent, warningPercent, infoPercent, otherPercent, levelsOverTimeSeries, Array.Empty<ISeries>(),
                    logDistributionSeries, timeHeatmapSeries, errorTrendSeries, Array.Empty<ISeries>(), logStats, timeAxis, countAxis, daysAxis, hoursAxis, sourceAxis, errorMessageAxis);
            }
        }

        private string TruncateMessage(string message, int maxLength) {
            if (string.IsNullOrEmpty(message))
                return string.Empty;

            return message.Length <= maxLength ? message : message.Substring(0, maxLength) + "...";
        }

        private int DetermineOptimalTimeInterval(DateTime minTimestamp, DateTime maxTimestamp) {
            double totalMinutes = (maxTimestamp - minTimestamp).TotalMinutes;
            if (totalMinutes <= 60)
                return 10; // 10-минутные интервалы для периода менее часа
            if (totalMinutes <= 180)
                return 30; // 30-минутные интервалы для периода менее 3 часов
            if (totalMinutes <= 720)
                return 60; // 1-часовые интервалы для периода менее 12 часов

            return 120; // 2-часовые интервалы для более длительных периодов
        }

        private string DetermineTimeFormat(DateTime minTimestamp, DateTime maxTimestamp, int tickInterval) {
            if ((maxTimestamp - minTimestamp).TotalDays > 1)
                return "dd.MM HH:mm"; // Включаем день для многодневных логов

            return "HH:mm"; // Только время для однодневных логов
        }

        [RelayCommand]
        private void ShowPackageErrorDetails(PackageLogEntry? entry) {
            if (entry == null)
                return;

            SelectedPackageEntry = entry;
            _logger.LogInformation("Selected package error entry: {PackageId}", entry.PackageId);
        }

        [RelayCommand]
        private async Task ApplyFilters() {
            if (SelectedTab == null)
                return;

            await Task.Run(() => {
                IEnumerable<LogEntry> currentFiltered = SelectedTab.LogEntries;

                // First, apply the "Errors only" filter if it's active.
                if (SelectedTab.IsErrorsOnly) // Assuming you add this property to TabViewModel
                {
                    currentFiltered = currentFiltered.Where(e => e.Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase));
                }

                // Then, apply the user-defined criteria.
                foreach (var criterion in SelectedTab.FilterCriteria) {
                    if (criterion.IsActive && !string.IsNullOrEmpty(criterion.SelectedField) && !string.IsNullOrEmpty(criterion.SelectedOperator)) {
                        currentFiltered = ApplySingleFilterCriterion(currentFiltered, criterion);
                    }
                }

                var filteredList = currentFiltered.ToList();

                Dispatcher.UIThread.Invoke(() => {
                    SelectedTab.FilteredLogEntries.Clear();
                    foreach (var entry in filteredList) {
                        SelectedTab.FilteredLogEntries.Add(entry);
                    }
                });
            });
            UpdateLogStatistics(); // Make sure this is called to refresh UI.
        }

        [RelayCommand]
        private async Task ResetFilters() {
            if (LogEntries.Count == 0) {
                StatusMessage = "No log entries to reset filters on.";
                return;
            }
            StatusMessage = "Resetting filters...";
            IsLoading = true;
            try {
                await Task.Run(() => {
                    var allEntries = LogEntries.ToList();
                    Dispatcher.UIThread.Invoke(() => {
                        FilterCriteria.Clear(); // Also clear the criteria themselves
                        FilteredLogEntries = allEntries;
                        StatusMessage = $"Filters reset. Displaying {FilteredLogEntries.Count} entries.";
                        _logger.LogInformation("Filters reset. Displaying {Count} log entries.", FilteredLogEntries.Count);
                    });
                });
            } catch (Exception ex) {
                _logger.LogError(ex, "Error resetting filters");
                StatusMessage = $"Error resetting filters: {ex.Message}";
            } finally {
                await Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
            }
        }

        private IEnumerable<LogEntry> ApplySingleFilterCriterion(IEnumerable<LogEntry> entries, FilterCriterion criterion) {
            return entries.Where(entry => {
                string? value = GetLogEntryPropertyValue(entry, criterion.SelectedField);
                if (value == null)
                    return false;

                switch (criterion.SelectedOperator) {
                    case "Equals":
                        return string.Equals(value, criterion.Value, StringComparison.OrdinalIgnoreCase);
                    case "Not Equals":
                        return !string.Equals(value, criterion.Value, StringComparison.OrdinalIgnoreCase);
                    case "Contains":
                        return value.Contains(criterion.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                    case "Regex":
                        if (string.IsNullOrEmpty(criterion.Value))
                            return true;

                        try {
                            return System.Text.RegularExpressions.Regex.Match(value, criterion.Value, System.Text.RegularExpressions.RegexOptions.IgnoreCase).Success;
                        } catch (System.Text.RegularExpressions.RegexParseException) {
                            return false;
                        }
                    case "Regex Not Contains":
                        if (string.IsNullOrEmpty(criterion.Value))
                            return true;

                        try {
                            return !System.Text.RegularExpressions.Regex.Match(value, criterion.Value, System.Text.RegularExpressions.RegexOptions.IgnoreCase).Success;
                        } catch (System.Text.RegularExpressions.RegexParseException) {
                            return false;
                        }

                    default:
                        return false;
                }
            });
        }

        private string? GetLogEntryPropertyValue(LogEntry entry, string? fieldName) {
            if (string.IsNullOrEmpty(fieldName))
                return null;

            switch (fieldName) {
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
        private void OpenLogFile(LogEntry? entry) {
            if (entry == null)
                return;

            string? filePath = LastOpenedFilePath;
            if (string.IsNullOrEmpty(filePath)) {
                StatusMessage = "File path is empty. Cannot open file.";
                return;
            }
            try {
                bool opened = false;
                string? error = null;
                if (OperatingSystem.IsMacOS()) {
                    try {
                        var psi = new System.Diagnostics.ProcessStartInfo {
                            FileName = "/usr/bin/open",
                            Arguments = $"\"{filePath}\"",
                            UseShellExecute = false
                        };
                        var proc = System.Diagnostics.Process.Start(psi);
                        if (proc != null)
                            opened = true;
                    } catch (Exception ex) {
                        error = $"Can't open: {ex.Message}\n{ex.StackTrace}";
                        _logger.LogError(ex, "open failed");
                    }
                } else if (OperatingSystem.IsWindows()) {
                    try {
                        var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true });
                        if (proc != null)
                            opened = true;
                    } catch (Exception ex) {
                        error = $"Cannot open: {ex.Message}\n{ex.StackTrace}";
                        _logger.LogError(ex, "Windows open failed");
                    }
                } else if (OperatingSystem.IsLinux()) {
                    try {
                        var psi = new System.Diagnostics.ProcessStartInfo {
                            FileName = "xdg-open",
                            Arguments = $"\"{filePath}\"",
                            UseShellExecute = false
                        };
                        var proc = System.Diagnostics.Process.Start(psi);
                        if (proc != null)
                            opened = true;
                    } catch (Exception ex) {
                        error = $"Не удалось открыть файл через xdg-open: {ex.Message}\n{ex.StackTrace}";
                        _logger.LogError(ex, "Linux open failed");
                    }
                } else {
                    error = "Неизвестная ОС, не могу открыть файл";
                }
                if (!opened) {
                    StatusMessage = error ?? "Не удалось открыть файл (неизвестная ошибка)";
                }
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to open file {FilePath}", filePath);
                StatusMessage = $"Ошибка открытия файла: {ex.Message}\n{ex.StackTrace}";
            }
        }

        [RelayCommand]
        private async Task ShowFilePickerContextMenu() {
            try {
                var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
                var result = await _filePickerService.ShowFilePickerContextMenuAsync(mainWindow);
                if (result.Files != null && result.Files.Any()) {
                    await LoadFilesAsync(result.Files);
                } else if (!string.IsNullOrEmpty(result.Directory)) {
                    await LoadDirectoryAsync(result.Directory);
                }
            } catch (Exception ex) {
                _logger.LogError(ex, "Error showing file picker context menu");
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private void SelectTab(TabViewModel tab) {
            if (tab == null)
                return;

            foreach (var t in FileTabs) {
                t.IsSelected = (t == tab);
            }

            SelectedTab = tab;
        }



        private void UpdateMultiFileModeStatus() {
            bool previousState = IsMultiFileModeActive;
            IsMultiFileModeActive = FileTabs.Count > 1;
            _logger.LogInformation("UpdateMultiFileModeStatus executed. FileTabs.Count: {Count}. IsMultiFileModeActive changed from {Previous} to {Current}",
                FileTabs.Count,
                previousState,
                IsMultiFileModeActive);
        }

        private async void UpdateAllErrorLogEntries() {
            try {
                if (IsMultiFileModeActive) {
                    _logger.LogDebug("[UpdateAllErrorLogEntries] Starting to collect errors. FileTabs.Count: {FileTabsCount}", FileTabs.Count);
                    var allErrors = new List<LogEntry>();
                    foreach (var tab in FileTabs) {
                        _logger.LogDebug("[UpdateAllErrorLogEntries] Processing tab: '{TabTitle}'. Total entries in this tab: {LogEntriesCount}", tab.Title, tab.LogEntries.Count);
                        
                        // Use error detection service instead of simple Level filtering
                        var tabErrors = await _errorDetectionService.DetectErrorsAsync(tab.LogEntries, tab.LogType);
                        var tabErrorsList = tabErrors.ToList();
                        
                        _logger.LogDebug("[UpdateAllErrorLogEntries] Found {ErrorCount} errors in tab: '{TabTitle}' using {LogType} strategy", tabErrorsList.Count, tab.Title, tab.LogType);
                        foreach (var error in tabErrorsList) {
                            error.SourceTabTitle = tab.Title; // Set the source tab title
                            allErrors.Add(error);
                        }
                    }
                    _logger.LogDebug("[UpdateAllErrorLogEntries] Total errors collected from all tabs: {TotalErrorCount}", allErrors.Count);
                    allErrors.Sort((x, y) => x.Timestamp.CompareTo(y.Timestamp));
                    
                    // Update UI collections on UI thread
                    await Dispatcher.UIThread.InvokeAsync(() => {
                        AllErrorLogEntries.Clear();
                        foreach (var error in allErrors) {
                            AllErrorLogEntries.Add(error);
                        }
                        OnPropertyChanged(nameof(AllErrorLogEntries));
                    });
                } else {
                    // Clear collections on UI thread when not in multi-file mode
                    await Dispatcher.UIThread.InvokeAsync(() => {
                        AllErrorLogEntries.Clear();
                        OnPropertyChanged(nameof(AllErrorLogEntries));
                    });
                }
                _logger.LogInformation("AllErrorLogEntries updated. Count: {Count}. Active: {IsMultiFileModeActive}", AllErrorLogEntries.Count, IsMultiFileModeActive);
            } catch (Exception ex) {
                _logger.LogError(ex, "Error in UpdateAllErrorLogEntries");
                // Safely clear on UI thread in case of error
                await Dispatcher.UIThread.InvokeAsync(() => {
                    AllErrorLogEntries.Clear();
                    OnPropertyChanged(nameof(AllErrorLogEntries));
                });
            }
        }

        [RelayCommand]
        private void AddFilterCriterion() {
            if (SelectedTab == null)
                return;

            var newCriterion = new FilterCriterion {
                ParentViewModel = SelectedTab,
            };

            foreach (string field in SelectedTab.MasterAvailableFields) {
                newCriterion.AvailableFields.Add(field);
            }

            SelectedTab.FilterCriteria.Add(newCriterion);
        }

        [RelayCommand]
        private async Task RemoveFilterCriterion(FilterCriterion? criterion) {
            if (SelectedTab == null || criterion == null)
                return;

            SelectedTab.FilterCriteria.Remove(criterion);
            await ApplyFilters(); 
        }

        [RelayCommand]
        private void ShowStandardLogSection() {
            IsStartScreenVisible = false;
        }

        [RelayCommand]
        private void ShowIISLogSection() {
            IsStartScreenVisible = false;
        }

        [RelayCommand]
        private void ShowStartScreen() {
            IsStartScreenVisible = true;
        }

        [RelayCommand]
        private async Task LoadIISLogs() {
            try {
                var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
                var (files, directory) = await _filePickerService.ShowFilePickerContextMenuAsync(mainWindow);

                if (files != null && files.Any()) {
                    // Load selected files - filter for .log files or validate IIS format
                    var validIISFiles = new List<string>();
                    foreach (var file in files) {
                        if (System.IO.Path.GetExtension(file).Equals(".log", StringComparison.OrdinalIgnoreCase) || 
                            await IsIISLogFileAsync(file)) {
                            validIISFiles.Add(file);
                        }
                    }
                    await LoadIISFilesAsync(validIISFiles);
                } else if (!string.IsNullOrEmpty(directory)) {
                    // Load all .log files from directory
                    var logFiles = System.IO.Directory.EnumerateFiles(directory, "*.log", System.IO.SearchOption.TopDirectoryOnly);
                    await LoadIISFilesAsync(logFiles);
                }

                if (FileTabs.Any()) {
                    IsStartScreenVisible = false;
                }
            } catch (Exception ex) {
                _logger.LogError(ex, "Ошибка загрузки IIS файлов логов");
                await Dispatcher.UIThread.InvokeAsync(() => {
                    StatusMessage = $"Error loading IIS logs: {ex.Message}";
                    IsLoading = false;
                });
            }
        }

        private async Task<bool> IsIISLogFileAsync(string filePath) {
            try {
                using var reader = new StreamReader(filePath);
                var lines = new List<string>();
                for (int i = 0; i < 10 && !reader.EndOfStream; i++) {
                    var line = await reader.ReadLineAsync();
                    if (!string.IsNullOrEmpty(line))
                        lines.Add(line);
                }

                return lines.Any(line =>
                    line.StartsWith("#Software: Microsoft Internet Information Services") || line.StartsWith("#Version:") || line.StartsWith("#Fields:") ||
                    (line.Contains("GET") || line.Contains("POST")) && System.Text.RegularExpressions.Regex.IsMatch(line, @"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}"));
            } catch (Exception ex) {
                _logger.LogError(ex, "Error detecting file type for {FilePath}", filePath);
                return false;
            }
        }

        private async Task LoadIISFilesAsync(IEnumerable<string> filePaths) {
            var allIISEntries = new List<IisLogEntry>();
            int totalFailedEntriesCount = 0;
            int totalProcessedEntriesCount = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    IsLoading = true;
                    StatusMessage = "Loading IIS log files...";
                });

                var semaphore = new System.Threading.SemaphoreSlim(Environment.ProcessorCount * 2);
                var tasks = new List<Task>();

                foreach (string filePath in filePaths) {
                    await semaphore.WaitAsync();
                    tasks.Add(Task.Run(async () => {
                        try {
                            int fileFailedEntriesCount = 0;
                            int fileProcessedEntriesCount = 0;

                            await foreach (var entry in _iisLogParserService.ParseLogFileAsync(filePath, CancellationToken.None)) {
                                try {
                                    lock (allIISEntries) {
                                        allIISEntries.Add(entry);
                                    }
                                    System.Threading.Interlocked.Increment(ref totalProcessedEntriesCount);
                                    fileProcessedEntriesCount++;
                                } catch (Exception entryEx) {
                                    System.Threading.Interlocked.Increment(ref totalFailedEntriesCount);
                                    fileFailedEntriesCount++;
                                    _logger.LogWarning(entryEx, "Failed to process IIS log entry from file {FilePath}, continuing. Failed entries so far: {FailedCount}", filePath, fileFailedEntriesCount);
                                }
                            }

                            _logger.LogDebug("Processed file {FilePath}: {ProcessedCount} entries, {FailedCount} errors", 
                                           filePath, fileProcessedEntriesCount, fileFailedEntriesCount);
                        } catch (Exception ex) {
                            _logger.LogError(ex, "Error processing IIS log file {FilePath}", filePath);
                        } finally {
                            semaphore.Release();
                        }
                    }));
                }

                await Task.WhenAll(tasks);

                await Dispatcher.UIThread.InvokeAsync(() => {
                    if (allIISEntries.Any()) {
                        // Sort by timestamp for better viewing experience
                        var sortedEntries = allIISEntries.OrderBy(e => e.DateTime ?? DateTimeOffset.MinValue).ToList();
                        
                        string title = filePaths.Count() == 1 ? 
                                     Path.GetFileName(filePaths.First()) : 
                                     $"IIS Logs ({filePaths.Count()} files)";
                        
                        // Use IIS-specific constructor to ensure proper data binding
                        			var newTab = new TabViewModel(filePaths.First(), title, (List<IisLogEntry>)sortedEntries, _filePickerService);

                        _logger.LogInformation("Created new IIS tab for {FileCount} files. LogType: {LogType}, IsThisTabIIS: {IsIIS}", 
                                             filePaths.Count(), newTab.LogType, newTab.IsThisTabIIS);

                        FileTabs.Clear();
                        FileTabs.Add(newTab);
                        SelectedTab = newTab;

                        UpdateLogStatistics();

                        int totalAttemptedEntries = totalProcessedEntriesCount + totalFailedEntriesCount;
                        double successRate = totalAttemptedEntries > 0 ? Math.Round((double)totalProcessedEntriesCount / totalAttemptedEntries * 100, 1) : 100.0;

                        _logger.LogInformation("Загружено {ProcessedCount} из {TotalCount} IIS записей за {ElapsedMs}ms (успешность: {SuccessRate}%). Ошибок парсинга: {FailedCount}",
                            allIISEntries.Count,
                            totalAttemptedEntries,
                            sw.ElapsedMilliseconds,
                            successRate,
                            totalFailedEntriesCount);

                        StatusMessage = totalFailedEntriesCount > 0
                            ? $"Loaded {allIISEntries.Count} IIS log entries from {filePaths.Count()} files ({successRate}% success rate, {totalFailedEntriesCount} parsing errors)"
                            : $"Loaded {allIISEntries.Count} IIS log entries from {filePaths.Count()} files (100% success rate)";
                    } else {
                        _logger.LogWarning("No IIS entries found in provided files");
                        StatusMessage = "No IIS log entries found in selected files";
                    }

                    IsLoading = false;
                });
            } catch (Exception ex) {
                _logger.LogError(ex, "Ошибка загрузки IIS файлов логов");
                await Dispatcher.UIThread.InvokeAsync(() => {
                    StatusMessage = $"Error loading IIS logs: {ex.Message}";
                    IsLoading = false;
                });
            }
        }

        private async Task LoadIISFileAsync(string filePath) {
            await LoadIISFilesAsync(new[] { filePath });
        }

        [RelayCommand]
        private async Task LoadRabbitMqLogs() {
            try {
                var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
                var (files, directory) = await _filePickerService.ShowFilePickerContextMenuAsync(mainWindow);

                if (files != null && files.Any()) {
                    var jsonSelected = files.Where(f => System.IO.Path.GetExtension(f).Equals(".json", StringComparison.OrdinalIgnoreCase));
                    await LoadRabbitMqFilesAsync(jsonSelected);
                } else if (!string.IsNullOrEmpty(directory)) {
                    // Use new paired file parsing for directories
                    await LoadRabbitMqDirectoryAsync(directory);
                }

                if (FileTabs.Any()) {
                    IsStartScreenVisible = false;
                }
            } catch (Exception ex) {
                _logger.LogError(ex, "Error loading RabbitMQ log files");
                await Dispatcher.UIThread.InvokeAsync(() => {
                    StatusMessage = $"Error loading RabbitMQ logs: {ex.Message}";
                    IsLoading = false;
                });
            }
        }

        private async Task LoadRabbitMqFileAsync(string filePath) {
            try {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                int failedEntriesCount = 0;
                int processedEntriesCount = 0;

                var rabbitEntries = await Task.Run(async () => {
                    var entriesList = new List<RabbitMqLogEntry>();
                    await foreach (var rabbitEntry in _rabbitMqLogParserService.ParseLogFileAsync(filePath, CancellationToken.None)) {
                        try {
                            entriesList.Add(rabbitEntry);
                            processedEntriesCount++;
                        } catch (Exception entryEx) {
                            failedEntriesCount++;
                            _logger.LogWarning(entryEx, "Failed to process RabbitMQ log entry, continuing. Failed entries so far: {FailedCount}", failedEntriesCount);
                        }
                    }
                    return entriesList;
                });

                await Dispatcher.UIThread.InvokeAsync(() => {
                    string title = System.IO.Path.GetFileName(filePath);
                    			var newTab = new TabViewModel(filePath, title, rabbitEntries, _filePickerService);

                    FileTabs.Clear();
                    FileTabs.Add(newTab);
                    SelectedTab = newTab;

                    UpdateLogStatistics();

                    int totalAttemptedEntries = processedEntriesCount + failedEntriesCount;
                    double successRate = totalAttemptedEntries > 0 ? Math.Round((double)processedEntriesCount / totalAttemptedEntries * 100, 1) : 100.0;

                    StatusMessage = failedEntriesCount > 0
                        ? $"Loaded {rabbitEntries.Count} RabbitMQ log entries ({successRate}% success rate, {failedEntriesCount} parsing errors)"
                        : $"Loaded {rabbitEntries.Count} RabbitMQ log entries (100% success rate)";

                    IsLoading = false;
                });
            } catch (Exception ex) {
                _logger.LogError(ex, "Error loading RabbitMQ log file");
                await Dispatcher.UIThread.InvokeAsync(() => {
                    StatusMessage = $"Error loading RabbitMQ log: {ex.Message}";
                    IsLoading = false;
                });
            }
        }

        private async Task LoadRabbitMqDirectoryAsync(string directoryPath) {
            try {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                int failedEntriesCount = 0;
                int processedEntriesCount = 0;

                _logger.LogInformation("Starting RabbitMQ directory parsing with paired file detection: {DirectoryPath}", directoryPath);
                Console.WriteLine($"[MAINVIEWMODEL] Starting RabbitMQ directory parsing: {directoryPath}");

                var rabbitEntries = await Task.Run(async () => {
                    var entriesList = new List<RabbitMqLogEntry>();
                    Console.WriteLine($"[MAINVIEWMODEL] About to call ParseLogDirectoryAsync");
                    await foreach (var rabbitEntry in _rabbitMqLogParserService.ParseLogDirectoryAsync(directoryPath, CancellationToken.None)) {
                        try {
                            entriesList.Add(rabbitEntry);
                            processedEntriesCount++;
                            Console.WriteLine($"[MAINVIEWMODEL] Added entry #{processedEntriesCount}");
                        } catch (Exception entryEx) {
                            failedEntriesCount++;
                            _logger.LogWarning(entryEx, "Failed to process RabbitMQ log entry, continuing. Failed entries so far: {FailedCount}", failedEntriesCount);
                        }
                    }
                    Console.WriteLine($"[MAINVIEWMODEL] ParseLogDirectoryAsync completed. Total entries: {entriesList.Count}");
                    return entriesList;
                });

                await Dispatcher.UIThread.InvokeAsync(() => {
                    string title = $"RabbitMQ ({System.IO.Path.GetFileName(directoryPath)})";
                    				var newTab = new TabViewModel(directoryPath, title, rabbitEntries, _filePickerService);

                    FileTabs.Clear();
                    FileTabs.Add(newTab);
                    SelectedTab = newTab;

                    UpdateLogStatistics();

                    int totalAttemptedEntries = processedEntriesCount + failedEntriesCount;
                    double successRate = totalAttemptedEntries > 0 ? Math.Round((double)processedEntriesCount / totalAttemptedEntries * 100, 1) : 100.0;

                    StatusMessage = failedEntriesCount > 0
                        ? $"Loaded {rabbitEntries.Count} RabbitMQ paired file entries ({successRate}% success rate, {failedEntriesCount} parsing errors)"
                        : $"Loaded {rabbitEntries.Count} RabbitMQ paired file entries (100% success rate)";

                    IsLoading = false;
                });

                _logger.LogInformation("Completed RabbitMQ directory parsing in {ElapsedMs}ms", sw.ElapsedMilliseconds);
            } catch (Exception ex) {
                _logger.LogError(ex, "Error loading RabbitMQ directory");
                await Dispatcher.UIThread.InvokeAsync(() => {
                    StatusMessage = $"Error loading RabbitMQ directory: {ex.Message}";
                    IsLoading = false;
                });
            }
        }

        private async Task LoadRabbitMqFilesAsync(IEnumerable<string> filePaths) {
            var allEntries = new List<RabbitMqLogEntry>();
            int failedEntriesCount = 0;

            var semaphore = new System.Threading.SemaphoreSlim(Environment.ProcessorCount * 2);
            var tasks = new List<Task>();

            foreach (string file in filePaths) {
                await semaphore.WaitAsync();
                tasks.Add(Task.Run(async () => {
                    try {
                        await foreach (var rabbitEntry in _rabbitMqLogParserService.ParseLogFileAsync(file, CancellationToken.None)) {
                            lock (allEntries) {
                                allEntries.Add(rabbitEntry);
                            }
                        }
                    } catch (Exception ex) {
                        System.Threading.Interlocked.Increment(ref failedEntriesCount);
                        _logger.LogWarning(ex, "Failed to parse RabbitMQ file {File}", file);
                    } finally {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);

            await Dispatcher.UIThread.InvokeAsync(() => {
                string title = filePaths.Count() == 1 ? System.IO.Path.GetFileName(filePaths.First()) : $"RabbitMQ ({filePaths.Count()} files)";
                			var newTab = new TabViewModel("RabbitMQ", title, allEntries, _filePickerService);

                FileTabs.Clear();
                FileTabs.Add(newTab);
                SelectedTab = newTab;

                UpdateLogStatistics();

                StatusMessage = $"Loaded {allEntries.Count} RabbitMQ log entries from {filePaths.Count()} files";
            });
        }

        /// <summary>
        /// Loads file with intelligent type detection
        /// Uses FileTypeDetectionService to determine appropriate handler
        /// </summary>
        private async Task LoadFileWithTypeDetection(string filePath) {
            try {
                _logger.LogInformation("Detecting file type for: {FilePath}", filePath);
                
                // Use intelligent file type detection
                var detectedType = await _fileTypeDetectionService.DetectFileTypeAsync(filePath);
                
                _logger.LogInformation("File {FilePath} detected as type: {LogType}", filePath, detectedType);
                
                switch (detectedType) {
                    case LogFormatType.IIS:
                        await LoadIISFileAsync(filePath);
                        break;
                    case LogFormatType.RabbitMQ:
                        await LoadRabbitMqFileAsync(filePath);
                        break;
                    case LogFormatType.Standard:
                        await LoadFileToTab(filePath);
                        break;
                    default:
                        _logger.LogWarning("Unknown file type {LogType} for {FilePath}, treating as Standard", detectedType, filePath);
                        await LoadFileToTab(filePath);
                        break;
                }
            } catch (Exception ex) {
                _logger.LogError(ex, "Error loading file with type detection: {FilePath}", filePath);
                // Fallback to Standard
                await LoadFileToTab(filePath);
            }
        }

        #region Service Event Handlers

        /// <summary>
        /// Handle tab changed events from TabManagerService
        /// </summary>
        private void OnTabChanged(object? sender, TabChangedEventArgs e)
        {
            try
            {
                SelectedTab = e.NewTab;
                OnPropertyChanged(nameof(IsCurrentTabIIS));
                
                if (SelectedTab != null)
                {
                    FilePath = SelectedTab.FilePath;
                    FileStatus = SelectedTab.Title;


                    IsStartScreenVisible = false;
                }
                else
                {
                    FilePath = string.Empty;
                    FileStatus = "No file selected";
                }
                
                UpdateLogStatistics();
                _logger.LogDebug($"Tab changed to: {SelectedTab?.Title}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling tab changed event");
            }
        }

        /// <summary>
        /// Handle tab closed events from TabManagerService
        /// </summary>
        private void OnTabClosed(object? sender, TabClosedEventArgs e)
        {
            try
            {
                            UpdateMultiFileModeStatus();
            _ = Task.Run(UpdateAllErrorLogEntries);
                
                if (_tabManagerService.FileTabs.Count == 0)
                {
                    IsStartScreenVisible = true;
                }
                
                _logger.LogDebug($"Tab closed: {e.ClosedTab.Title}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling tab closed event");
            }
        }

        /// <summary>
        /// Handle filters applied events from FilterService
        /// </summary>
        private void OnFiltersApplied(object? sender, FiltersAppliedEventArgs e)
        {
            try
            {
                FilteredLogEntries = e.FilteredEntries.ToList();
                UpdateLogStatistics();
                _logger.LogDebug($"Filters applied: {e.OriginalEntries.Count()} -> {e.FilteredEntries.Count()} entries");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling filters applied event");
            }
        }

        /// <summary>
        /// Handle filters reset events from FilterService
        /// </summary>
        private void OnFiltersReset(object? sender, EventArgs e)
        {
            try
            {
                if (SelectedTab?.LogEntries != null)
                {
                    FilteredLogEntries = SelectedTab.LogEntries.ToList();
                }
                UpdateLogStatistics();
                _logger.LogDebug("Filters reset");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling filters reset event");
            }
        }

        #endregion





        [RelayCommand]
        private async Task ShowUpdateSettings()
        {
            try
            {
                var updateWindow = new Log_Parser_App.Views.UpdateSettingsWindow();
                
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow != null)
                {
                    await updateWindow.ShowDialog(desktop.MainWindow);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to show update settings window");
            }
        }

    }
}

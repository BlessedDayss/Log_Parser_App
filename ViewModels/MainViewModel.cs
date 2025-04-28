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
        
        // COMMENTED OUT: Chart Series Properties
        /*
        [ObservableProperty]
        private ISeries[] _levelsOverTimeSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private ISeries[] _topNErrorsSeries = Array.Empty<ISeries>();

        [ObservableProperty]
        private ISeries[] _logIntensitySeries = Array.Empty<ISeries>();
        
        [ObservableProperty]
        private Axis[] _timeAxis = { new Axis { Name = "Time", Labeler = value => new DateTime((long)value).ToString("HH:mm:ss") } };
        
        [ObservableProperty]
        private Axis[] _countAxis = { new Axis { Name = "Count", MinLimit = 0 } };
        
        [ObservableProperty]
        private Axis[] _errorMessageAxis = { new Axis { LabelsRotation = 15, Name = "Error Message"} };
        */
        
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
            
            // COMMENTED OUT: React to SelectedLogEntry changes for highlighting
            /*
            this.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(SelectedLogEntry))
                {
                    UpdateHighlightedEntries();
                }
            };
            */
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
                return;
            }
            
            _logger.LogDebug("Updating LogStatistics. Current LogEntries count: {Count}", LogEntries.Count);
            ErrorCount = LogEntries.Count<LogEntry>(e => e.Level == "ERROR");
            WarningCount = LogEntries.Count<LogEntry>(e => e.Level == "WARNING");
            InfoCount = LogEntries.Count<LogEntry>(e => e.Level == "INFO");
            OtherCount = LogEntries.Count - ErrorCount - WarningCount - InfoCount;
            _logger.LogDebug("Calculated counts - Error: {ErrorCount}, Warning: {WarningCount}, Info: {InfoCount}, Other: {OtherCount}", ErrorCount, WarningCount, InfoCount, OtherCount);
            
            var total = (double)LogEntries.Count;
            ErrorPercent = total > 0 ? Math.Round((ErrorCount / total) * 100, 1) : 0;
            WarningPercent = total > 0 ? Math.Round((WarningCount / total) * 100, 1) : 0;
            InfoPercent = total > 0 ? Math.Round((InfoCount / total) * 100, 1) : 0;
            OtherPercent = total > 0 ? Math.Round((OtherCount / total) * 100, 1) : 0;
            
            _logger.LogInformation("Updated basic statistics");
            
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
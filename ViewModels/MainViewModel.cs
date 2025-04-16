using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
// using LiveChartsCore; // Comment out
// using LiveChartsCore.SkiaSharpView; // Comment out
// using SkiaSharp; // Comment out
using Avalonia.Controls;
using LogParserApp.Models;
using LogParserApp.Services;
using Microsoft.Extensions.Logging;

namespace LogParserApp.ViewModels
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
        
        // COMMENTED OUT: Property for selected log entry in main grid
        // [ObservableProperty]
        // private LogEntry? _selectedLogEntry;
        
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
                await Task.Run(async () =>
                {
                    // Clear collections only through UI thread
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        LogEntries.Clear();
                        FilteredLogEntries.Clear();
                        ErrorLogEntries.Clear();
                    });

                    var entries = await _logParserService.ParseLogFileAsync(file);
                    
                    // Присваиваю команду открытия файла каждой записи
                    foreach (var entry in entries)
                    {
                        entry.OpenFileCommand = ExternalOpenFileCommand;
                    }
                    
                    // Pre-process all entries before adding them
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _logger.LogInformation("Processing {Count} log entries", entries.Count());
                        
                        // Clear collections
                        LogEntries.Clear();
                        ErrorLogEntries.Clear();
                        
                        int errorCount = 0;
                        int warningCount = 0;
                        
                        // Add all entries to main collection and classify them
                        foreach (var entry in entries)
                        {
                            // Add entry to main collection
                            LogEntries.Add(entry);
                            
                            // Check log level and add to appropriate collection
                            if (entry.Level == "ERROR")
                            {
                                _logger.LogDebug("Found ERROR entry: '{Message}'", entry.Message);
                                
                                // Анализируем ошибку и добавляем рекомендации
                                var recommendation = _errorRecommendationService.AnalyzeError(entry.Message);
                                _logger.LogDebug("Recommendation result: {Result}", recommendation != null ? "Found" : "Not found");
                                
                                if (recommendation != null)
                                {
                                    entry.ErrorType = recommendation.ErrorType;
                                    entry.ErrorDescription = recommendation.Description;
                                    entry.ErrorRecommendations.Clear();
                                    entry.ErrorRecommendations.AddRange(recommendation.Recommendations);
                                    
                                    _logger.LogDebug("Added recommendations for error: {ErrorType} with {Count} recommendations", 
                                        entry.ErrorType, entry.ErrorRecommendations.Count);
                                    _logger.LogDebug("HasRecommendations: {HasRecommendations}", entry.HasRecommendations);
                                }
                                else
                                {
                                    // Если рекомендация не найдена, добавляем дефолтное сообщение
                                    entry.ErrorType = "UnknownError";
                                    entry.ErrorDescription = "Unknown error. Recommendations not found.";
                                    entry.ErrorRecommendations.Add("Check error log for additional information.");
                                    entry.ErrorRecommendations.Add("Contact documentation or support.");
                                    
                                    _logger.LogWarning("No recommendation found for error message: {Message}", entry.Message);
                                    _logger.LogDebug("Added default recommendations. HasRecommendations: {HasRecommendations}", entry.HasRecommendations);
                                }
                                
                                ErrorLogEntries.Add(entry);
                                errorCount++;
                            }
                            else if (entry.Level == "WARNING")
                            {
                                warningCount++;
                            }
                        }
                        
                        _logger.LogInformation("Added {Count} entries, including {ErrorCount} errors and {WarningCount} warnings", 
                            LogEntries.Count, errorCount, warningCount);
                    });
                });
                
                // Update statistics and status
                await Dispatcher.UIThread.InvokeAsync(() => {
                    UpdateLogStatistics(); // Reverted method name
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
        
        // Renamed back to UpdateLogStatistics
        private void UpdateLogStatistics()
        {
            if (LogEntries.Count == 0)
            {
                // Reset stats 
                ErrorCount = 0;
                WarningCount = 0;
                InfoCount = 0;
                OtherCount = 0;
                ErrorPercent = 0;
                WarningPercent = 0;
                InfoPercent = 0;
                OtherPercent = 0;
                // COMMENTED OUT: Chart resets
                // LevelsOverTimeSeries = Array.Empty<ISeries>();
                // TopNErrorsSeries = Array.Empty<ISeries>();
                // LogIntensitySeries = Array.Empty<ISeries>();
                return;
            }
            
            // Calculate basic counts
            ErrorCount = LogEntries.Count(e => e.Level == "ERROR");
            WarningCount = LogEntries.Count(e => e.Level == "WARNING");
            InfoCount = LogEntries.Count(e => e.Level == "INFO");
            OtherCount = LogEntries.Count - ErrorCount - WarningCount - InfoCount;
            
            var total = (double)LogEntries.Count;
            ErrorPercent = total > 0 ? Math.Round((ErrorCount / total) * 100, 1) : 0;
            WarningPercent = total > 0 ? Math.Round((WarningCount / total) * 100, 1) : 0;
            InfoPercent = total > 0 ? Math.Round((InfoCount / total) * 100, 1) : 0;
            OtherPercent = total > 0 ? Math.Round((OtherCount / total) * 100, 1) : 0;
            
            _logger.LogInformation("Updated basic statistics");

            // COMMENTED OUT: Calculate Chart Data
            // CalculateLevelsOverTimeChart();
            // CalculateTopNErrorsChart();
            // CalculateLogIntensityChart();
        }

        // COMMENTED OUT: Chart calculation methods
        /*
        private void CalculateLevelsOverTimeChart() { ... }
        private void CalculateTopNErrorsChart(int N = 10) { ... }
        private void CalculateLogIntensityChart() { ... }
        */

        // COMMENTED OUT: Method to update highlighting based on SelectedLogEntry
        /*
        private void UpdateHighlightedEntries()
        {
            // ... implementation ...
        }
        */
        
        [RelayCommand]
        private void ShowPackageErrorDetails(PackageLogEntry entry)
        {
            if (entry == null)
                return;
                
            SelectedPackageEntry = entry;
            _logger.LogInformation("Selected package error entry: {PackageId}", entry.PackageId);
        }

        [RelayCommand]
        private void ApplyFilters()
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
                StatusMessage = "Файл не был загружен";
                return;
            }
            try
            {
                bool opened = false;
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
                        error = $"Не удалось открыть файл через open: {ex.Message}\n{ex.StackTrace}";
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
                        error = $"Не удалось открыть файл: {ex.Message}\n{ex.StackTrace}";
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
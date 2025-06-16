using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Log_Parser_App.Models;
using Log_Parser_App.Services;
using Log_Parser_App.Services.Dashboard;
using Log_Parser_App.Interfaces;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.ViewModels
{
    /// <summary>
    /// Main ViewModel that coordinates all sub-ViewModels
    /// Follows Single Responsibility Principle - only coordinates, doesn't implement business logic
    /// </summary>
    public partial class MainViewModelRefactored : ViewModelBase
    {
        #region Dependencies

        private readonly ILogger<MainViewModelRefactored> _logger;
        private readonly IDashboardTypeService _dashboardTypeService;

        #endregion

        #region Sub-ViewModels

        public FileLoadingViewModel FileLoading { get; }
        public FilteringViewModel Filtering { get; }
        public TabManagerViewModel TabManager { get; }
        public StatisticsViewModel Statistics { get; }

        #endregion

        #region Properties

        [ObservableProperty]
        private bool _isDarkTheme;

        [ObservableProperty]
        private bool _isStartScreenVisible = true;

        [ObservableProperty]
        private bool _isDashboardVisible = false;

        [ObservableProperty]
        private bool _isIISDashboardVisible = false;

        [ObservableProperty]
        private bool _isStandardDashboardVisible = false;

        // Dashboard properties
        [ObservableProperty]
        private DashboardType _currentDashboardType = DashboardType.Overview;

        [ObservableProperty]
        private DashboardData? _currentDashboardData;

        [ObservableProperty]
        private bool _isDashboardLoading;

        [ObservableProperty]
        private string _dashboardErrorMessage = string.Empty;

        public IReadOnlyList<DashboardType> AvailableDashboardTypes => 
            _dashboardTypeService?.AvailableDashboardTypes ?? new List<DashboardType>();

        // Current log data
        private List<LogEntry> _logEntries = new();
        public List<LogEntry> LogEntries
        {
            get => _logEntries;
            set => SetProperty(ref _logEntries, value);
        }

        private List<LogEntry> _filteredLogEntries = new();
        public List<LogEntry> FilteredLogEntries
        {
            get => _filteredLogEntries;
            set => SetProperty(ref _filteredLogEntries, value);
        }

        private List<LogEntry> _errorLogEntries = new();
        public List<LogEntry> ErrorLogEntries
        {
            get => _errorLogEntries;
            set => SetProperty(ref _errorLogEntries, value);
        }

        public ObservableCollection<LogEntry> AllErrorLogEntries { get; } = new();

        // Convenience properties for UI binding
        public string StatusMessage => FileLoading.StatusMessage;
        public bool IsLoading => FileLoading.IsLoading;
        public string FilePath => FileLoading.FilePath;
        public string FileStatus => FileLoading.FileStatus;
        public ObservableCollection<TabViewModel> FileTabs => TabManager.FileTabs;
        public TabViewModel? SelectedTab => TabManager.SelectedTab;
        public bool IsCurrentTabIIS => TabManager.IsCurrentTabIIS;

        #endregion

        #region Constructor

        public MainViewModelRefactored(
            FileLoadingViewModel fileLoadingViewModel,
            FilteringViewModel filteringViewModel,
            TabManagerViewModel tabManagerViewModel,
            StatisticsViewModel statisticsViewModel,
            IDashboardTypeService dashboardTypeService,
            ILogger<MainViewModelRefactored> logger)
        {
            FileLoading = fileLoadingViewModel;
            Filtering = filteringViewModel;
            TabManager = tabManagerViewModel;
            Statistics = statisticsViewModel;
            _dashboardTypeService = dashboardTypeService;
            _logger = logger;

            InitializeEventHandlers();
            CheckCommandLineArgs();

            _logger.LogInformation("MainViewModel initialized with SOLID architecture");
        }

        #endregion

        #region Commands

        [RelayCommand]
        private void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
            _logger.LogDebug("Theme toggled to: {Theme}", IsDarkTheme ? "Dark" : "Light");
        }

        [RelayCommand]
        private void ToggleDashboardVisibility()
        {
            IsDashboardVisible = !IsDashboardVisible;
            _logger.LogDebug("Dashboard visibility toggled to: {Visible}", IsDashboardVisible);
        }

        [RelayCommand]
        private void ShowStandardLogSection()
        {
            IsStartScreenVisible = false;
            _logger.LogDebug("Showing standard log section");
        }

        [RelayCommand]
        private void ShowIISLogSection()
        {
            IsStartScreenVisible = false;
            _logger.LogDebug("Showing IIS log section");
        }

        [RelayCommand]
        private void ShowStartScreen()
        {
            IsStartScreenVisible = true;
            IsDashboardVisible = false;
            _logger.LogDebug("Showing start screen");
        }

        [RelayCommand]
        private Task ChangeDashboardType(DashboardType dashboardType)
        {
            return ChangeDashboardTypeAsync(dashboardType);
        }

        [RelayCommand]
        private Task ChangeDashboardTypeFromString(string dashboardTypeString)
        {
            if (Enum.TryParse<DashboardType>(dashboardTypeString, out var dashboardType))
            {
                return ChangeDashboardTypeAsync(dashboardType);
            }
            
            return Task.CompletedTask;
        }

        [RelayCommand]
        private async Task RefreshCurrentDashboard()
        {
            await RefreshCurrentDashboardAsync();
        }

        [RelayCommand]
        private void OpenLogFile(LogEntry? entry)
        {
            if (entry?.FilePath != null)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = entry.FilePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error opening log file: {FilePath}", entry.FilePath);
                }
            }
        }

        #endregion

        #region Private Methods

        private void InitializeEventHandlers()
        {
            // Subscribe to file loading events
            FileLoading.FileLoaded += OnFileLoaded;
            FileLoading.FileLoadingFailed += OnFileLoadingFailed;

            // Subscribe to filtering events
            Filtering.FiltersApplied += OnFiltersApplied;
            Filtering.FiltersReset += OnFiltersReset;

            // Subscribe to tab manager events
            TabManager.TabSelected += OnTabSelected;
            TabManager.TabClosed += OnTabClosed;
            TabManager.TabAdded += OnTabAdded;

            // Subscribe to statistics events
            Statistics.StatisticsUpdated += OnStatisticsUpdated;

            // Forward property changes from sub-ViewModels
            FileLoading.PropertyChanged += (s, e) => ForwardPropertyChange(e.PropertyName);
            TabManager.PropertyChanged += (s, e) => ForwardPropertyChange(e.PropertyName);
        }

        private void ForwardPropertyChange(string? propertyName)
        {
            // Forward relevant property changes to UI
            switch (propertyName)
            {
                case nameof(FileLoadingViewModel.StatusMessage):
                    OnPropertyChanged(nameof(StatusMessage));
                    break;
                case nameof(FileLoadingViewModel.IsLoading):
                    OnPropertyChanged(nameof(IsLoading));
                    break;
                case nameof(FileLoadingViewModel.FilePath):
                    OnPropertyChanged(nameof(FilePath));
                    break;
                case nameof(FileLoadingViewModel.FileStatus):
                    OnPropertyChanged(nameof(FileStatus));
                    break;
                case nameof(TabManagerViewModel.SelectedTab):
                    OnPropertyChanged(nameof(SelectedTab));
                    OnPropertyChanged(nameof(IsCurrentTabIIS));
                    break;
                case nameof(TabManagerViewModel.FileTabs):
                    OnPropertyChanged(nameof(FileTabs));
                    break;
            }
        }

        private void CheckCommandLineArgs()
        {
            var startupFilePath = Program.StartupFilePath;

            if (!string.IsNullOrEmpty(startupFilePath))
            {
                _logger.LogInformation("Loading file from command line arguments: {FilePath}", startupFilePath);
                FileLoading.LastOpenedFilePath = startupFilePath;

                Task.Delay(500).ContinueWith(async _ =>
                {
                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        try
                        {
                            await FileLoading.LoadFileAsync(startupFilePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to load startup file: {FilePath}", startupFilePath);
                        }
                    });
                });
            }
        }

        private async Task ChangeDashboardTypeAsync(DashboardType dashboardType)
        {
            try
            {
                _logger.LogInformation("Changing dashboard type to: {DashboardType}", dashboardType);
                
                CurrentDashboardType = dashboardType;
                
                // Don't automatically show dashboard - user must click dashboard button
                _logger.LogInformation("Dashboard type context prepared, visibility: {IsVisible}", IsDashboardVisible);
                
                await UpdateDashboardContextAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing dashboard type to: {DashboardType}", dashboardType);
                DashboardErrorMessage = $"Error changing dashboard: {ex.Message}";
            }
        }

        private async Task RefreshCurrentDashboardAsync()
        {
            try
            {
                _logger.LogInformation("Refreshing current dashboard: {DashboardType}", CurrentDashboardType);
                
                IsDashboardLoading = true;
                DashboardErrorMessage = string.Empty;
                
                await UpdateDashboardContextAsync();
                
                _logger.LogInformation("Dashboard refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing dashboard");
                DashboardErrorMessage = $"Error refreshing dashboard: {ex.Message}";
            }
            finally
            {
                IsDashboardLoading = false;
            }
        }

        private Task UpdateDashboardContextAsync()
        {
            try
            {
                if (SelectedTab == null) return Task.CompletedTask;

                // Update dashboard context based on current tab and log type
                if (SelectedTab.LogType == LogFormatType.IIS)
                {
                    IsIISDashboardVisible = true;
                    IsStandardDashboardVisible = false;
                }
                else
                {
                    IsIISDashboardVisible = false;
                    IsStandardDashboardVisible = true;
                }

                _logger.LogDebug("Dashboard context updated for {LogType} logs", SelectedTab.LogType);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating dashboard context");
                return Task.FromException(ex);
            }
        }

        private void UpdateLogStatistics()
        {
            try
            {
                if (SelectedTab?.IsThisTabIIS == true)
                {
                    Statistics.UpdateIISStatistics(SelectedTab);
                }
                else if (FilteredLogEntries.Any())
                {
                    Statistics.UpdateStatistics(FilteredLogEntries);
                }
                else if (LogEntries.Any())
                {
                    Statistics.UpdateStatistics(LogEntries);
                }
                else
                {
                    Statistics.ClearAllCharts();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating log statistics");
            }
        }

        private void UpdateErrorLogEntries()
        {
            try
            {
                var errorEntries = LogEntries.Where(e => e.Level?.ToLowerInvariant() == "error").ToList();
                ErrorLogEntries = errorEntries;

                AllErrorLogEntries.Clear();
                foreach (var entry in errorEntries)
                {
                    AllErrorLogEntries.Add(entry);
                }

                _logger.LogDebug("Updated error log entries: {Count} errors", errorEntries.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating error log entries");
            }
        }

        #endregion

        #region Event Handlers

        private async void OnFileLoaded(object? sender, FileLoadedEventArgs e)
        {
            try
            {
                _logger.LogInformation("File loaded: {FilePath} with {Count} entries", e.FilePath, e.LogEntries.Count);

                // Add tab for the loaded file
                var tab = TabManager.AddTab(e.FilePath, e.LogType);
                
                // Update tab with entries
                TabManager.UpdateTabWithEntries(tab, e.LogEntries);

                // Update current log data
                LogEntries = e.LogEntries;
                FilteredLogEntries = e.LogEntries;

                // Update filter values
                Filtering.UpdateAvailableFilterValues(e.LogEntries);

                // Update statistics
                UpdateLogStatistics();
                UpdateErrorLogEntries();

                // Hide start screen
                IsStartScreenVisible = false;

                _logger.LogInformation("File processing completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing loaded file: {FilePath}", e.FilePath);
            }
        }

        private void OnFileLoadingFailed(object? sender, FileLoadingFailedEventArgs e)
        {
            _logger.LogError(e.Exception, "File loading failed");
        }

        private async void OnFiltersApplied(object? sender, FiltersAppliedEventArgs e)
        {
            try
            {
                var filteredEntries = await Filtering.ApplyFiltersToEntriesAsync(LogEntries);
                FilteredLogEntries = filteredEntries.ToList();
                
                UpdateLogStatistics();
                
                _logger.LogInformation("Filters applied: {Count} entries after filtering", FilteredLogEntries.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying filters");
            }
        }

        private void OnFiltersReset(object? sender, FiltersResetEventArgs e)
        {
            try
            {
                FilteredLogEntries = LogEntries;
                UpdateLogStatistics();
                
                _logger.LogInformation("Filters reset: showing all {Count} entries", LogEntries.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting filters");
            }
        }

        private async void OnTabSelected(object? sender, TabSelectedEventArgs e)
        {
            try
            {
                _logger.LogDebug("Tab selected: {Title}", e.SelectedTab.Title);
                
                // Update dashboard context
                await UpdateDashboardContextAsync();
                
                // Update statistics for the selected tab
                UpdateLogStatistics();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling tab selection");
            }
        }

        private void OnTabClosed(object? sender, TabClosedEventArgs e)
        {
            try
            {
                _logger.LogDebug("Tab closed: {Title}", e.ClosedTab.Title);
                
                // If no tabs left, show start screen
                if (!TabManager.FileTabs.Any())
                {
                    IsStartScreenVisible = true;
                    IsDashboardVisible = false;
                    LogEntries.Clear();
                    FilteredLogEntries.Clear();
                    ErrorLogEntries.Clear();
                    AllErrorLogEntries.Clear();
                    Statistics.ClearAllCharts();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling tab closure");
            }
        }

        private void OnTabAdded(object? sender, TabAddedEventArgs e)
        {
            _logger.LogDebug("Tab added: {Title}", e.AddedTab.Title);
        }

        private void OnStatisticsUpdated(object? sender, StatisticsUpdatedEventArgs e)
        {
            _logger.LogDebug("Statistics updated: {Total} total entries", e.Statistics.TotalEntries);
        }

        #endregion
    }
} 
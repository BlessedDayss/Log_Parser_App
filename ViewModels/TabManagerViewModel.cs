using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Log_Parser_App.Models;
using Log_Parser_App.Services;
using Log_Parser_App.Interfaces;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.ViewModels
{
    /// <summary>
    /// ViewModel responsible for tab management operations
    /// Follows Single Responsibility Principle
    /// </summary>
    public partial class TabManagerViewModel : ViewModelBase
    {
        #region Dependencies

        private readonly ITabManagerService _tabManagerService;
        private readonly ILogger<TabManagerViewModel> _logger;

        #endregion

        #region Properties

        private ObservableCollection<TabViewModel> _fileTabs = new();
        public ObservableCollection<TabViewModel> FileTabs
        {
            get => _fileTabs;
            set => SetProperty(ref _fileTabs, value);
        }

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
                    OnPropertyChanged(nameof(HasSelectedTab));
                    OnPropertyChanged(nameof(SelectedTabTitle));
                    OnPropertyChanged(nameof(SelectedTabFilePath));

                    if (_selectedTab != null)
                    {
                        _selectedTab.PropertyChanged += SelectedTab_PropertyChanged;
                        OnTabSelected(new TabSelectedEventArgs(_selectedTab));
                    }

                    UpdateMultiFileModeStatus();
                }
            }
        }

        [ObservableProperty]
        private int _selectedTabIndex = 0;

        [ObservableProperty]
        private bool _isMultiFileModeActive = false;

        public bool IsCurrentTabIIS => SelectedTab?.LogType == LogFormatType.IIS;
        public bool HasSelectedTab => SelectedTab != null;
        public string SelectedTabTitle => SelectedTab?.Title ?? "No file selected";
        public string SelectedTabFilePath => SelectedTab?.FilePath ?? string.Empty;

        #endregion

        #region Events

        public event EventHandler<TabSelectedEventArgs>? TabSelected;
        public event EventHandler<TabClosedEventArgs>? TabClosed;
        public event EventHandler<TabAddedEventArgs>? TabAdded;

        #endregion

        #region Constructor

        public TabManagerViewModel(
            ITabManagerService tabManagerService,
            ILogger<TabManagerViewModel> logger)
        {
            _tabManagerService = tabManagerService;
            _logger = logger;

            // Subscribe to service events
            _tabManagerService.TabChanged += OnTabManagerTabChanged;
            _tabManagerService.TabClosed += OnTabManagerTabClosed;
        }

        #endregion

        #region Commands

        [RelayCommand]
        private void SelectTab(TabViewModel tab)
        {
            try
            {
                if (tab == null) return;

                SelectedTab = tab;
                SelectedTabIndex = FileTabs.IndexOf(tab);
                
                _logger.LogDebug("Selected tab: {Title}", tab.Title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting tab");
            }
        }

        [RelayCommand]
        private void CloseTab(TabViewModel tab)
        {
            try
            {
                if (tab == null) return;

                _logger.LogInformation("Closing tab: {Title}", tab.Title);

                var wasSelected = SelectedTab == tab;
                FileTabs.Remove(tab);

                if (wasSelected)
                {
                    SelectedTab = FileTabs.LastOrDefault();
                }

                UpdateMultiFileModeStatus();
                OnTabClosed(new TabClosedEventArgs { ClosedTab = tab, WasLastTab = !FileTabs.Any() });

                _logger.LogInformation("Tab closed successfully: {Title}", tab.Title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing tab: {Title}", tab?.Title);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Add a new tab for a loaded file
        /// </summary>
        public TabViewModel AddTab(string filePath, LogFormatType logType)
        {
            try
            {
                var existingTab = FileTabs.FirstOrDefault(t => t.FilePath == filePath);
                if (existingTab != null)
                {
                    SelectedTab = existingTab;
                    return existingTab;
                }

                var tab = new TabViewModel(filePath, System.IO.Path.GetFileName(filePath), new List<LogEntry>(), logType);

                FileTabs.Add(tab);
                SelectedTab = tab;
                
                UpdateMultiFileModeStatus();
                OnTabAdded(new TabAddedEventArgs(tab));

                _logger.LogInformation("Added new tab: {Title} ({LogType})", tab.Title, logType);
                
                return tab;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding tab for file: {FilePath}", filePath);
                throw;
            }
        }

        /// <summary>
        /// Update tab with log entries
        /// </summary>
        public void UpdateTabWithEntries(TabViewModel tab, System.Collections.Generic.List<LogEntry> logEntries)
        {
            try
            {
                if (tab == null) return;

                // Update tab statistics based on log type
                if (tab.LogType == LogFormatType.IIS)
                {
                    UpdateIISTabStatistics(tab, logEntries);
                }
                else
                {
                    UpdateStandardTabStatistics(tab, logEntries);
                }

                _logger.LogDebug("Updated tab statistics: {Title} - {Count} entries", tab.Title, logEntries.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tab with entries: {Title}", tab?.Title);
            }
        }

        /// <summary>
        /// Clear all tabs
        /// </summary>
        public void ClearAllTabs()
        {
            try
            {
                _logger.LogInformation("Clearing all tabs");
                
                FileTabs.Clear();
                SelectedTab = null;
                IsMultiFileModeActive = false;
                
                _logger.LogInformation("All tabs cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all tabs");
            }
        }

        #endregion

        #region Private Methods

        private void UpdateMultiFileModeStatus()
        {
            IsMultiFileModeActive = FileTabs.Count > 1;
        }

        private void UpdateIISTabStatistics(TabViewModel tab, System.Collections.Generic.List<LogEntry> logEntries)
        {
            // Update IIS-specific statistics
            // Note: IIS statistics are read-only properties calculated from FilteredIISLogEntries
            // We need to update the underlying IIS log entries collection instead
            _logger.LogDebug("IIS statistics are calculated automatically from FilteredIISLogEntries");
        }

        private void UpdateStandardTabStatistics(TabViewModel tab, System.Collections.Generic.List<LogEntry> logEntries)
        {
            // Update standard log statistics
            // This could be expanded based on TabViewModel properties
        }

        private bool IsIISError(LogEntry entry)
        {
            // IIS error detection logic
            return entry.Level?.ToLowerInvariant() == "error" || 
                   (entry.Message?.Contains("500") == true) ||
                   (entry.Message?.Contains("404") == true);
        }

        private bool IsIISInfo(LogEntry entry)
        {
            return entry.Level?.ToLowerInvariant() == "info" ||
                   entry.Level?.ToLowerInvariant() == "information";
        }

        private bool IsIISRedirect(LogEntry entry)
        {
            return entry.Message?.Contains("301") == true ||
                   entry.Message?.Contains("302") == true;
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
                    // Notify that tab statistics changed
                    OnPropertyChanged(nameof(SelectedTab));
                }
            }
        }

        #endregion

        #region Event Handlers

        private void OnTabManagerTabChanged(object? sender, TabChangedEventArgs e)
        {
            // Handle tab manager service events
        }

        private void OnTabManagerTabClosed(object? sender, TabClosedEventArgs e)
        {
            // Handle tab manager service events
        }

        private void OnTabSelected(TabSelectedEventArgs e)
        {
            TabSelected?.Invoke(this, e);
        }

        private void OnTabClosed(TabClosedEventArgs e)
        {
            TabClosed?.Invoke(this, e);
        }

        private void OnTabAdded(TabAddedEventArgs e)
        {
            TabAdded?.Invoke(this, e);
        }

        #endregion
    }

    #region Event Args

    public class TabSelectedEventArgs : EventArgs
    {
        public TabViewModel SelectedTab { get; }
        public TabSelectedEventArgs(TabViewModel selectedTab) => SelectedTab = selectedTab;
    }

    public class TabAddedEventArgs : EventArgs
    {
        public TabViewModel AddedTab { get; }
        public TabAddedEventArgs(TabViewModel addedTab) => AddedTab = addedTab;
    }

    #endregion
} 
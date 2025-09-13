using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Log_Parser_App.Services.Interfaces;
using Log_Parser_App.ViewModels;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services
{
    public class TabManagerService : ITabManagerService
    {
        private readonly ILogger<TabManagerService> _logger;
        private readonly IFilePickerService _filePickerService;
        private readonly IUiDispatcher _uiDispatcher;
        private readonly ObservableCollection<TabViewModel> _fileTabs = new();
        private TabViewModel? _selectedTab;

        public TabManagerService(
            ILogger<TabManagerService> logger,
            IFilePickerService filePickerService,
            IUiDispatcher uiDispatcher)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
            _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
        }

        public ObservableCollection<TabViewModel> FileTabs => _fileTabs;

        public TabViewModel? SelectedTab
        {
            get => _selectedTab;
            set => _selectedTab = value;
        }

        public event EventHandler<TabChangedEventArgs>? TabChanged;
        public event EventHandler<TabClosedEventArgs>? TabClosed;

        public async Task<TabViewModel> CreateTabAsync(string fileName, string filePath, LogFormatType logType) {
            return await Task.Run(() => {
                try {
                    string tabName = !string.IsNullOrWhiteSpace(fileName) ? fileName : Path.GetFileName(filePath);
                    var existingTab = _fileTabs.FirstOrDefault(t => t.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

                    if (existingTab != null) {
                        _logger.LogInformation("Tab already exists for file: {FilePath}", filePath);
                        return existingTab;
                    }

                    var newTab = new TabViewModel(filePath, tabName, new List<LogEntry>(), logType, _filePickerService);

                    _uiDispatcher.Invoke(() => {
                        _fileTabs.Add(newTab);
                    });

                    _logger.LogInformation("Created new tab for file: {FilePath}, type: {LogType}", filePath, logType);
                    return newTab;
                } catch (Exception ex) {
                    _logger.LogError(ex, "Error creating tab for file: {FilePath}", filePath);
                    throw;
                }
            });
        }

        public async Task<bool> CloseTabAsync(TabViewModel? tab) {
            try {
                if (tab == null) {
                    _logger.LogWarning("Attempted to close null tab");
                    return false;
                }

                if (!_fileTabs.Contains(tab)) {
                    _logger.LogWarning("Attempted to close tab that is not in collection: {TabTitle}", tab.Title);
                    return false;
                }

                _logger.LogDebug("Closing tab: {TabTitle}", tab.Title);

                bool wasSelected = _selectedTab == tab;
                int tabIndex = _fileTabs.IndexOf(tab);

                _fileTabs.Remove(tab);

                if (tab is IDisposable disposableTab) {
                    disposableTab.Dispose();
                }

                if (wasSelected) {
                    await HandleClosedSelectedTab(tabIndex);
                }

                _logger.LogInformation("Closed tab: {TabTitle}", tab.Title);

                TabClosed?.Invoke(this, new TabClosedEventArgs { ClosedTab = tab, WasLastTab = wasSelected });

                return true;
            } catch (Exception ex) {
                _logger.LogError(ex, "Error closing tab: {TabTitle}", tab?.Title);
                return false;
            }
        }

        private async Task SelectTabAsync(TabViewModel? tab) {
            try {
                if (tab == null) {
                    _logger.LogWarning("Attempted to select null tab");
                    return;
                }

                if (!_fileTabs.Contains(tab)) {
                    _logger.LogWarning("Attempted to select tab that is not in collection: {TabTitle}", tab.Title);
                    return;
                }

                var previousTab = _selectedTab;
                _selectedTab = tab;

                _logger.LogDebug("Selected tab: {TabTitle}", tab.Title);

                TabChanged?.Invoke(this, new TabChangedEventArgs(previousTab, tab));

                await Task.CompletedTask;
            } catch (Exception ex) {
                _logger.LogError(ex, "Error selecting tab: {TabTitle}", tab?.Title);
                throw;
            }
        }

        public void SelectTab(TabViewModel? tab) {
            try {
                if (tab == null) {
                    _logger.LogWarning("Attempted to select null tab");
                    return;
                }

                if (!_fileTabs.Contains(tab)) {
                    _logger.LogWarning("Attempted to select tab that is not in collection: {TabTitle}", tab.Title);
                    return;
                }

                SelectedTab = tab;

                _logger.LogDebug("Selected tab: {TabTitle}", tab.Title);
            } catch (Exception ex) {
                _logger.LogError(ex, "Error selecting tab: {TabTitle}", tab?.Title);
            }
        }

        private TabViewModel? GetTabByFilePath(string filePath) {
            try {
                return _fileTabs.FirstOrDefault(t => t.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            } catch (Exception ex) {
                _logger.LogError(ex, "Error getting tab by file path: {FilePath}", filePath);
                return null;
            }
        }

        public async Task CloseAllTabsAsync() {
            try {
                _logger.LogDebug("Closing all tabs");

                var tabsToClose = _fileTabs.ToList();

                foreach (var tab in tabsToClose) {
                    await CloseTabAsync(tab);
                }

                _selectedTab = null;

                _logger.LogInformation($"Closed all tabs ({tabsToClose.Count} total)");
            } catch (Exception ex) {
                _logger.LogError(ex, "Error closing all tabs");
                throw;
            }
        }

        public IEnumerable<TabViewModel?> GetTabsByLogType(LogFormatType logType) {
            try {
                return _fileTabs.Where(t => t.LogType == logType).ToList();
            } catch (Exception ex) {
                _logger.LogError(ex, "Error getting tabs by log type: {LogType}", logType);
                return Enumerable.Empty<TabViewModel>();
            }
        }

        public Task UpdateTabTitleAsync(TabViewModel? tab, string newTitle) {
            return Task.Run(() => {
                try {
                    if (tab == null) {
                        _logger.LogWarning("Attempted to update title of null tab");
                        return;
                    }

                    if (!_fileTabs.Contains(tab)) {
                        _logger.LogWarning("Attempted to update title of tab not in collection: {TabTitle}", tab.Title);
                        return;
                    }

                    _uiDispatcher.Invoke(() => {
                        var oldTitle = tab.Title;
                        tab.Title = newTitle;
                        _logger.LogDebug("Updated tab title from '{OldTitle}' to '{NewTitle}'", oldTitle, newTitle);
                    });
                } catch (Exception ex) {
                    _logger.LogError(ex, "Error updating tab title: {TabTitle}", tab?.Title);
                }
            });
        }

        public bool ValidateTabState() {
            try {
                var duplicatePaths = _fileTabs.GroupBy(t => t?.FilePath, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

                if (duplicatePaths.Any()) {
                    _logger.LogWarning("Found duplicate file paths in tabs: {DuplicatePaths}", string.Join(", ", duplicatePaths));
                    return false;
                }

                if (_selectedTab == null || _fileTabs.Contains(_selectedTab))
                    return true;

                _logger.LogWarning("Selected tab is not in FileTabs collection");
                return false;

            } catch (Exception ex) {
                _logger.LogError(ex, "Error validating tab state");
                return false;
            }
        }

        public async Task RefreshSelectedTabAsync() {
            try {
                if (_selectedTab == null) {
                    _logger.LogWarning("No tab selected for refresh");
                    return;
                }

                await Task.Run(() => {
                    _logger.LogDebug("Refreshing selected tab: {TabTitle}", _selectedTab.Title);

                    TabChanged?.Invoke(this, new TabChangedEventArgs(null, _selectedTab));
                });
            } catch (Exception ex) {
                _logger.LogError(ex, "Error refreshing selected tab");
            }
        }

        public TabViewModel? FindTabByFilePath(string filePath) {
            return GetTabByFilePath(filePath);
        }

        private async Task HandleClosedSelectedTab(int closedTabIndex) {
            if (!HasTabs) {
                _selectedTab = null;
                _logger.LogDebug("No tabs remaining after close");
                return;
            }

            TabViewModel? newSelectedTab = null;

            if (closedTabIndex < _fileTabs.Count) {
                newSelectedTab = _fileTabs[closedTabIndex];
            } else if (closedTabIndex > 0 && closedTabIndex - 1 < _fileTabs.Count) {
                newSelectedTab = _fileTabs[closedTabIndex - 1];
            } else if (_fileTabs.Any()) {
                newSelectedTab = _fileTabs[0];
            }

            if (newSelectedTab != null) {
                await SelectTabAsync(newSelectedTab);
                _logger.LogDebug("Auto-selected replacement tab: {Title}", newSelectedTab.Title);
            }
        }

        private bool HasTabs => _fileTabs.Any();

    }
}
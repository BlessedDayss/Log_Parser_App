using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Log_Parser_App.ViewModels;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services;

/// <summary>
/// Tab manager service implementation handling tab lifecycle and management.
/// Extracted from MainViewModel to follow SRP principle.
/// </summary>
public class TabManagerService : ITabManagerService
{
    private readonly ILogger<TabManagerService> _logger;
    private readonly ObservableCollection<TabViewModel> _fileTabs;
    private TabViewModel? _selectedTab;

    public TabManagerService(ILogger<TabManagerService> logger)
    {
        _logger = logger;
        _fileTabs = new ObservableCollection<TabViewModel>();
    }

    /// <summary>
    /// Collection of all open file tabs
    /// </summary>
    public ObservableCollection<TabViewModel> FileTabs => _fileTabs;

    /// <summary>
    /// Currently selected tab
    /// </summary>
    public TabViewModel? SelectedTab
    {
        get => _selectedTab;
        set => _selectedTab = value;
    }

    /// <summary>
    /// Event fired when selected tab changes
    /// </summary>
    public event EventHandler<TabChangedEventArgs>? TabChanged;

    /// <summary>
    /// Event fired when selected tab is closed
    /// </summary>
    public event EventHandler<TabClosedEventArgs>? TabClosed;

    /// <summary>
    /// Create a new tab for the specified file path
    /// </summary>
    public async Task<TabViewModel> CreateTabAsync(string fileName, string filePath, LogFormatType logType)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Use provided fileName or extract from path if not provided
                var tabName = !string.IsNullOrWhiteSpace(fileName) ? fileName : Path.GetFileName(filePath);
                var existingTab = _fileTabs.FirstOrDefault(t => t.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
                
                if (existingTab != null)
                {
                    _logger.LogInformation("Tab already exists for file: {FilePath}", filePath);
                    return existingTab;
                }

                var newTab = new TabViewModel(filePath, tabName, new List<LogEntry>(), logType);

                Dispatcher.UIThread.Invoke(() =>
                {
                    _fileTabs.Add(newTab);
                });

                _logger.LogInformation("Created new tab for file: {FilePath}, type: {LogType}", filePath, logType);
                return newTab;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tab for file: {FilePath}", filePath);
                throw;
            }
        });
    }

    /// <summary>
    /// Close the specified tab
    /// </summary>
    public async Task<bool> CloseTabAsync(TabViewModel tab)
    {
        try
        {
            if (tab == null)
            {
                _logger.LogWarning("Attempted to close null tab");
                return false;
            }

            if (!_fileTabs.Contains(tab))
            {
                _logger.LogWarning("Attempted to close tab that is not in collection: {TabTitle}", tab.Title);
                return false;
            }

            _logger.LogDebug($"Closing tab: {tab.Title}");

            var wasSelected = _selectedTab == tab;
            var tabIndex = _fileTabs.IndexOf(tab);

            // Remove the tab
            _fileTabs.Remove(tab);

            // Dispose if needed
            if (tab is IDisposable disposableTab)
            {
                disposableTab.Dispose();
            }

            // Handle selection logic if closed tab was selected
            if (wasSelected)
            {
                await HandleClosedSelectedTab(tabIndex);
            }

            _logger.LogInformation($"Closed tab: {tab.Title}");

            // Raise event for listeners
            TabClosed?.Invoke(this, new TabClosedEventArgs { ClosedTab = tab, WasLastTab = wasSelected });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error closing tab: {tab?.Title}");
            return false;
        }
    }

    /// <summary>
    /// Select the specified tab as active (async version)
    /// </summary>
    public async Task SelectTabAsync(TabViewModel tab)
    {
        try
        {
            if (tab == null)
            {
                _logger.LogWarning("Attempted to select null tab");
                return;
            }

            if (!_fileTabs.Contains(tab))
            {
                _logger.LogWarning("Attempted to select tab that is not in collection: {TabTitle}", tab.Title);
                return;
            }

            var previousTab = _selectedTab;
            _selectedTab = tab;

            _logger.LogDebug($"Selected tab: {tab.Title}");

            // Raise event for listeners
            TabChanged?.Invoke(this, new TabChangedEventArgs(previousTab, tab));

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error selecting tab: {tab?.Title}");
            throw;
        }
    }

    /// <summary>
    /// Select the specified tab as active (sync version for interface)
    /// </summary>
    public void SelectTab(TabViewModel tab)
    {
        try
        {
            if (tab == null)
            {
                _logger.LogWarning("Attempted to select null tab");
                return;
            }

            if (!_fileTabs.Contains(tab))
            {
                _logger.LogWarning("Attempted to select tab that is not in collection: {TabTitle}", tab.Title);
                return;
            }

            // Set selected tab (IsActive property doesn't exist in TabViewModel)
            SelectedTab = tab;

            _logger.LogDebug("Selected tab: {TabTitle}", tab.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting tab: {TabTitle}", tab?.Title);
        }
    }

    /// <summary>
    /// Get tab by file path
    /// </summary>
    public TabViewModel? GetTabByFilePath(string filePath)
    {
        try
        {
            return _fileTabs.FirstOrDefault(t => t.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tab by file path: {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Close all tabs
    /// </summary>
    public async Task CloseAllTabsAsync()
    {
        try
        {
            _logger.LogDebug("Closing all tabs");

            var tabsToClose = _fileTabs.ToList(); // Create copy to avoid collection modification during iteration

            foreach (var tab in tabsToClose)
            {
                await CloseTabAsync(tab);
            }

            _selectedTab = null;

            _logger.LogInformation($"Closed all tabs ({tabsToClose.Count} total)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing all tabs");
            throw;
        }
    }

    /// <summary>
    /// Check if multi-file mode is active (more than one tab open)
    /// </summary>
    public bool IsMultiFileModeActive => _fileTabs.Count > 1;

    /// <summary>
    /// Get the count of open tabs
    /// </summary>
    public int TabCount => _fileTabs.Count;

    /// <summary>
    /// Check if any tab with specified log type exists
    /// </summary>
    public bool HasTabWithLogType(LogFormatType logType)
    {
        try
        {
            return _fileTabs.Any(t => t.LogType == logType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for tabs with log type: {LogType}", logType);
            return false;
        }
    }

    /// <summary>
    /// Get all tabs with specified log type
    /// </summary>
    public IEnumerable<TabViewModel> GetTabsByLogType(LogFormatType logType)
    {
        try
        {
            return _fileTabs.Where(t => t.LogType == logType).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tabs by log type: {LogType}", logType);
            return Enumerable.Empty<TabViewModel>();
        }
    }

    /// <summary>
    /// Update tab title
    /// </summary>
    public Task UpdateTabTitleAsync(TabViewModel tab, string newTitle)
    {
        return Task.Run(() =>
        {
            try
            {
                if (tab == null)
                {
                    _logger.LogWarning("Attempted to update title of null tab");
                    return;
                }

                if (!_fileTabs.Contains(tab))
                {
                    _logger.LogWarning("Attempted to update title of tab not in collection: {TabTitle}", tab.Title);
                    return;
                }

                Dispatcher.UIThread.Invoke(() =>
                {
                    var oldTitle = tab.Title;
                    tab.Title = newTitle;
                    _logger.LogDebug("Updated tab title from '{OldTitle}' to '{NewTitle}'", oldTitle, newTitle);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tab title: {TabTitle}", tab?.Title);
            }
        });
    }

    /// <summary>
    /// Validate tab state for debugging purposes
    /// </summary>
    public bool ValidateTabState()
    {
        try
        {
            // Check for duplicate file paths
            var duplicatePaths = _fileTabs
                .GroupBy(t => t.FilePath, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicatePaths.Any())
            {
                _logger.LogWarning("Found duplicate file paths in tabs: {DuplicatePaths}", string.Join(", ", duplicatePaths));
                return false;
            }

            // Check if selected tab is in collection
            if (_selectedTab != null && !_fileTabs.Contains(_selectedTab))
            {
                _logger.LogWarning("Selected tab is not in FileTabs collection");
                return false;
            }

            // Tab validation passed (IsActive property doesn't exist in TabViewModel)

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating tab state");
            return false;
        }
    }

    /// <summary>
    /// Refresh selected tab data asynchronously
    /// </summary>
    public async Task RefreshSelectedTabAsync()
    {
        try
        {
            if (_selectedTab == null)
            {
                _logger.LogWarning("No tab selected for refresh");
                return;
            }

            await Task.Run(() =>
            {
                // Trigger property change notifications for selected tab
                // This can be expanded to reload file data if needed
                _logger.LogDebug("Refreshing selected tab: {TabTitle}", _selectedTab.Title);
                
                // Notify that selected tab changed to trigger UI updates
                TabChanged?.Invoke(this, new TabChangedEventArgs(null, _selectedTab));
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing selected tab");
        }
    }

    /// <summary>
    /// Find tab by file path (synchronous version of GetTabByFilePath)
    /// </summary>
    public TabViewModel? FindTabByFilePath(string filePath)
    {
        return GetTabByFilePath(filePath);
    }

    #region Private Helper Methods

    /// <summary>
    /// Handle selection logic when the currently selected tab is closed
    /// </summary>
    private async Task HandleClosedSelectedTab(int closedTabIndex)
    {
        if (!HasTabs)
        {
            _selectedTab = null;
            _logger.LogDebug("No tabs remaining after close");
            return;
        }

        // Select appropriate replacement tab
        TabViewModel? newSelectedTab = null;

        // Try to select the tab at the same index
        if (closedTabIndex < _fileTabs.Count)
        {
            newSelectedTab = _fileTabs[closedTabIndex];
        }
        // Otherwise select the previous tab
        else if (closedTabIndex > 0 && closedTabIndex - 1 < _fileTabs.Count)
        {
            newSelectedTab = _fileTabs[closedTabIndex - 1];
        }
        // Otherwise select the first tab
        else if (_fileTabs.Any())
        {
            newSelectedTab = _fileTabs[0];
        }

        if (newSelectedTab != null)
        {
            await SelectTabAsync(newSelectedTab);
            _logger.LogDebug($"Auto-selected replacement tab: {newSelectedTab.Title}");
        }
    }

    /// <summary>
    /// Check if any tabs are open
    /// </summary>
    public bool HasTabs => _fileTabs.Any();

    /// <summary>
    /// Get selected tab index
    /// </summary>
    public int SelectedTabIndex => _selectedTab != null ? _fileTabs.IndexOf(_selectedTab) : -1;

    /// <summary>
    /// Update multi-file mode status and notify listeners
    /// </summary>
    public void UpdateMultiFileModeStatus()
    {
        var isMultiFileMode = IsMultiFileModeActive;
        _logger.LogDebug($"Multi-file mode status: {isMultiFileMode} ({TabCount} tabs)");
    }

    #endregion
}

#region Event Args Classes

// Event args classes moved to interface file

#endregion 
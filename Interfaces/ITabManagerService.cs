using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Log_Parser_App.ViewModels;
using Log_Parser_App.Models;

namespace Log_Parser_App.Interfaces;

/// <summary>
/// Service interface for tab management following SRP principle.
/// Handles tab lifecycle, selection, and file loading operations.
/// </summary>
public interface ITabManagerService
{
    /// <summary>
    /// Collection of currently open file tabs
    /// </summary>
    ObservableCollection<TabViewModel> FileTabs { get; }
    
    /// <summary>
    /// Currently selected tab
    /// </summary>
    TabViewModel? SelectedTab { get; set; }
    
    /// <summary>
    /// Create new tab for file with specified format
    /// </summary>
    /// <param name="fileName">Name of the file to load</param>
    /// <param name="filePath">Full path to the file</param>
    /// <param name="logFormat">Log format type for proper parsing</param>
    /// <returns>Task that completes when tab is created and file is loaded</returns>
    Task<TabViewModel> CreateTabAsync(string fileName, string filePath, LogFormatType logFormat);
    
    /// <summary>
    /// Close specific tab and clean up resources
    /// </summary>
    /// <param name="tab">Tab to close</param>
    /// <returns>True if tab was closed successfully</returns>
    Task<bool> CloseTabAsync(TabViewModel tab);
    
    /// <summary>
    /// Close all open tabs
    /// </summary>
    /// <returns>Task that completes when all tabs are closed</returns>
    Task CloseAllTabsAsync();
    
    /// <summary>
    /// Select tab for viewing
    /// </summary>
    /// <param name="tab">Tab to select</param>
    void SelectTab(TabViewModel tab);
    
    /// <summary>
    /// Refresh content of currently selected tab
    /// </summary>
    /// <returns>Task that completes when tab is refreshed</returns>
    Task RefreshSelectedTabAsync();
    
    /// <summary>
    /// Check if tab with specified file path already exists
    /// </summary>
    /// <param name="filePath">File path to check</param>
    /// <returns>Existing tab or null if not found</returns>
    TabViewModel? FindTabByFilePath(string filePath);
    
    /// <summary>
    /// Event fired when tab selection changes
    /// </summary>
    event EventHandler<TabChangedEventArgs>? TabChanged;

    /// <summary>
    /// Event fired when a tab is closed
    /// </summary>
    event EventHandler<TabClosedEventArgs>? TabClosed;
}

/// <summary>
/// Event arguments for tab closing
/// </summary>
public class TabClosedEventArgs : EventArgs
{
    public TabViewModel ClosedTab { get; set; } = null!;
    public bool WasLastTab { get; set; }
} 

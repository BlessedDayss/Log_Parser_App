using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Log_Parser_App.Services.Dashboard
{
    /// <summary>
    /// Service for managing different dashboard types and their strategies
    /// </summary>
    public interface IDashboardTypeService
    {
        /// <summary>
        /// Event fired when dashboard type changes
        /// </summary>
        event EventHandler<DashboardTypeChangedEventArgs> DashboardTypeChanged;

        /// <summary>
        /// Gets the current active dashboard type
        /// </summary>
        DashboardType CurrentDashboardType { get; }

        /// <summary>
        /// Gets all available dashboard types
        /// </summary>
        IReadOnlyList<DashboardType> AvailableDashboardTypes { get; }

        /// <summary>
        /// Gets the current dashboard strategy
        /// </summary>
        IDashboardStrategy CurrentStrategy { get; }

        /// <summary>
        /// Changes the dashboard type and applies the corresponding strategy
        /// </summary>
        /// <param name="dashboardType">The dashboard type to switch to</param>
        /// <returns>Task representing the async operation</returns>
        Task ChangeDashboardTypeAsync(DashboardType dashboardType);

        /// <summary>
        /// Gets the strategy for a specific dashboard type
        /// </summary>
        /// <param name="dashboardType">The dashboard type</param>
        /// <returns>The corresponding strategy</returns>
        IDashboardStrategy GetStrategy(DashboardType dashboardType);

        /// <summary>
        /// Determines the best dashboard type based on current context
        /// </summary>
        /// <param name="context">The current application context</param>
        /// <returns>The recommended dashboard type</returns>
        DashboardType DetermineBestDashboardType(DashboardContext context);

        /// <summary>
        /// Refreshes the current dashboard data
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        Task RefreshCurrentDashboardAsync();

        /// <summary>
        /// Checks if a dashboard type is available in current context
        /// </summary>
        /// <param name="dashboardType">The dashboard type to check</param>
        /// <returns>True if available, false otherwise</returns>
        bool IsDashboardTypeAvailable(DashboardType dashboardType);
    }

    /// <summary>
    /// Event arguments for dashboard type change events
    /// </summary>
    public class DashboardTypeChangedEventArgs : EventArgs
    {
        public DashboardType PreviousType { get; }
        public DashboardType NewType { get; }
        public DateTime ChangedAt { get; }

        public DashboardTypeChangedEventArgs(DashboardType previousType, DashboardType newType)
        {
            PreviousType = previousType;
            NewType = newType;
            ChangedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Enumeration of available dashboard types
    /// </summary>
    public enum DashboardType
    {
        /// <summary>
        /// Default overview dashboard
        /// </summary>
        Overview,

        /// <summary>
        /// File options and statistics dashboard
        /// </summary>
        FileOptions,

        /// <summary>
        /// Performance metrics dashboard
        /// </summary>
        Performance,

        /// <summary>
        /// Error analysis dashboard
        /// </summary>
        ErrorAnalysis,

        /// <summary>
        /// Real-time monitoring dashboard
        /// </summary>
        RealTime,

        /// <summary>
        /// Custom user-defined dashboard
        /// </summary>
        Custom
    }

    /// <summary>
    /// Context information for dashboard type determination
    /// </summary>
    public class DashboardContext
    {
        /// <summary>
        /// Currently loaded files information
        /// </summary>
        public IReadOnlyList<string> LoadedFiles { get; set; } = new List<string>();

        /// <summary>
        /// Current log parsing state
        /// </summary>
        public bool IsParsingActive { get; set; }

        /// <summary>
        /// Number of parsed log entries
        /// </summary>
        public int ParsedEntriesCount { get; set; }

        /// <summary>
        /// Number of errors detected
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Current performance metrics availability
        /// </summary>
        public bool HasPerformanceData { get; set; }

        /// <summary>
        /// User preferences for dashboard type
        /// </summary>
        public DashboardType? PreferredDashboardType { get; set; }

        /// <summary>
        /// Time when context was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

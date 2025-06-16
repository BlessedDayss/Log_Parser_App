using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Services.Dashboard
{
    /// <summary>
    /// Strategy interface for different dashboard implementations
    /// </summary>
    public interface IDashboardStrategy
    {
        /// <summary>
        /// Gets the dashboard type this strategy handles
        /// </summary>
        DashboardType DashboardType { get; }

        /// <summary>
        /// Gets the display name for this dashboard
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Gets the description of this dashboard
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the icon identifier for this dashboard
        /// </summary>
        string IconKey { get; }

        /// <summary>
        /// Initializes the dashboard strategy with required services
        /// </summary>
        /// <param name="context">The dashboard context</param>
        /// <returns>Task representing the async operation</returns>
        Task InitializeAsync(DashboardContext context);

        /// <summary>
        /// Loads and prepares dashboard data
        /// </summary>
        /// <param name="logEntries">Current log entries</param>
        /// <returns>Dashboard data model</returns>
        Task<DashboardData> LoadDashboardDataAsync(IReadOnlyList<LogEntry> logEntries);

        /// <summary>
        /// Refreshes the dashboard data
        /// </summary>
        /// <returns>Updated dashboard data</returns>
        Task<DashboardData> RefreshDataAsync();

        /// <summary>
        /// Gets the chart configurations for this dashboard
        /// </summary>
        /// <returns>List of chart configurations</returns>
        IReadOnlyList<ChartConfiguration> GetChartConfigurations();

        /// <summary>
        /// Gets the metrics displayed on this dashboard
        /// </summary>
        /// <returns>List of dashboard metrics</returns>
        IReadOnlyList<DashboardMetric> GetMetrics();

        /// <summary>
        /// Checks if this strategy can handle the current context
        /// </summary>
        /// <param name="context">The current context</param>
        /// <returns>True if can handle, false otherwise</returns>
        bool CanHandle(DashboardContext context);

        /// <summary>
        /// Gets the priority of this strategy for auto-selection
        /// </summary>
        /// <param name="context">The current context</param>
        /// <returns>Priority value (higher = more preferred)</returns>
        int GetPriority(DashboardContext context);

        /// <summary>
        /// Performs cleanup when switching away from this dashboard
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        Task CleanupAsync();
    }

    /// <summary>
    /// Dashboard data model containing all information for display
    /// </summary>
    public class DashboardData
    {
        /// <summary>
        /// Dashboard title
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Dashboard subtitle or description
        /// </summary>
        public string Subtitle { get; set; } = string.Empty;

        /// <summary>
        /// Key metrics to display
        /// </summary>
        public IList<DashboardMetric> Metrics { get; set; } = new List<DashboardMetric>();

        /// <summary>
        /// Chart data for visualization
        /// </summary>
        public IList<ChartData> Charts { get; set; } = new List<ChartData>();

        /// <summary>
        /// Additional data tables
        /// </summary>
        public IList<DataTable> Tables { get; set; } = new List<DataTable>();

        /// <summary>
        /// Last update timestamp
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Loading state
        /// </summary>
        public bool IsLoading { get; set; }

        /// <summary>
        /// Error message if any
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Dashboard metric model
    /// </summary>
    public class DashboardMetric
    {
        /// <summary>
        /// Metric name/label
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Metric value
        /// </summary>
        public object Value { get; set; } = string.Empty;

        /// <summary>
        /// Formatted display value
        /// </summary>
        public string DisplayValue { get; set; } = string.Empty;

        /// <summary>
        /// Metric unit (e.g., "ms", "%", "count")
        /// </summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>
        /// Metric type for styling
        /// </summary>
        public MetricType Type { get; set; } = MetricType.Info;

        /// <summary>
        /// Icon for the metric
        /// </summary>
        public string IconKey { get; set; } = string.Empty;

        /// <summary>
        /// Trend information
        /// </summary>
        public MetricTrend? Trend { get; set; }
    }

    /// <summary>
    /// Chart configuration model
    /// </summary>
    public class ChartConfiguration
    {
        /// <summary>
        /// Chart identifier
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Chart title
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Chart type
        /// </summary>
        public ChartType Type { get; set; } = ChartType.Line;

        /// <summary>
        /// Chart height in pixels
        /// </summary>
        public int Height { get; set; } = 300;

        /// <summary>
        /// Chart width (responsive if not set)
        /// </summary>
        public int? Width { get; set; }

        /// <summary>
        /// Chart options/settings
        /// </summary>
        public Dictionary<string, object> Options { get; set; } = new();
    }

    /// <summary>
    /// Chart data model
    /// </summary>
    public class ChartData
    {
        /// <summary>
        /// Chart configuration reference
        /// </summary>
        public string ConfigurationId { get; set; } = string.Empty;

        /// <summary>
        /// Chart series data
        /// </summary>
        public IList<ChartSeries> Series { get; set; } = new List<ChartSeries>();

        /// <summary>
        /// X-axis labels
        /// </summary>
        public IList<string> Labels { get; set; } = new List<string>();

        /// <summary>
        /// Chart metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Chart series model
    /// </summary>
    public class ChartSeries
    {
        /// <summary>
        /// Series name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Series data points
        /// </summary>
        public IList<object> Data { get; set; } = new List<object>();

        /// <summary>
        /// Series color
        /// </summary>
        public string Color { get; set; } = string.Empty;
    }

    /// <summary>
    /// Data table model
    /// </summary>
    public class DataTable
    {
        /// <summary>
        /// Table title
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Table columns
        /// </summary>
        public IList<DataColumn> Columns { get; set; } = new List<DataColumn>();

        /// <summary>
        /// Table rows
        /// </summary>
        public IList<Dictionary<string, object>> Rows { get; set; } = new List<Dictionary<string, object>>();
    }

    /// <summary>
    /// Data column model
    /// </summary>
    public class DataColumn
    {
        /// <summary>
        /// Column key
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Column display name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Column data type
        /// </summary>
        public Type DataType { get; set; } = typeof(string);

        /// <summary>
        /// Column width
        /// </summary>
        public string Width { get; set; } = "auto";
    }

    /// <summary>
    /// Metric type enumeration
    /// </summary>
    public enum MetricType
    {
        Info,
        Success,
        Warning,
        Error,
        Performance
    }

    /// <summary>
    /// Chart type enumeration
    /// </summary>
    public enum ChartType
    {
        Line,
        Bar,
        Pie,
        Doughnut,
        Area,
        Scatter
    }

    /// <summary>
    /// Metric trend information
    /// </summary>
    public class MetricTrend
    {
        /// <summary>
        /// Trend direction
        /// </summary>
        public TrendDirection Direction { get; set; }

        /// <summary>
        /// Trend percentage change
        /// </summary>
        public double PercentageChange { get; set; }

        /// <summary>
        /// Trend period description
        /// </summary>
        public string Period { get; set; } = string.Empty;
    }

    /// <summary>
    /// Trend direction enumeration
    /// </summary>
    public enum TrendDirection
    {
        Up,
        Down,
        Stable
    }
} 
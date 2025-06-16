using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Services.Dashboard
{
    /// <summary>
    /// Base abstract class for dashboard strategies providing common functionality
    /// </summary>
    public abstract class BaseDashboardStrategy : IDashboardStrategy
    {
        protected DashboardContext? _context;
        protected IReadOnlyList<LogEntry> _logEntries = new List<LogEntry>();
        
        public abstract DashboardType DashboardType { get; }
        public abstract string DisplayName { get; }
        public abstract string Description { get; }
        public abstract string IconKey { get; }

        public virtual async Task InitializeAsync(DashboardContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            await OnInitializeAsync();
        }

        public abstract Task<DashboardData> LoadDashboardDataAsync(IReadOnlyList<LogEntry> logEntries);

        public virtual async Task<DashboardData> RefreshDataAsync()
        {
            if (_logEntries == null)
                return CreateEmptyDashboardData();

            return await LoadDashboardDataAsync(_logEntries);
        }

        public abstract IReadOnlyList<ChartConfiguration> GetChartConfigurations();
        public abstract IReadOnlyList<DashboardMetric> GetMetrics();

        public virtual bool CanHandle(DashboardContext context)
        {
            return context != null;
        }

        public virtual int GetPriority(DashboardContext context)
        {
            return 0; // Default priority
        }

        public virtual async Task CleanupAsync()
        {
            await OnCleanupAsync();
            _context = null;
            _logEntries = new List<LogEntry>();
        }

        /// <summary>
        /// Called during initialization - override for custom initialization logic
        /// </summary>
        protected virtual Task OnInitializeAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called during cleanup - override for custom cleanup logic
        /// </summary>
        protected virtual Task OnCleanupAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Creates empty dashboard data for error states
        /// </summary>
        protected virtual DashboardData CreateEmptyDashboardData()
        {
            return new DashboardData
            {
                Title = DisplayName,
                Subtitle = "No data available",
                LastUpdated = DateTime.UtcNow,
                IsLoading = false
            };
        }

        /// <summary>
        /// Creates basic metrics from log entries
        /// </summary>
        protected virtual IList<DashboardMetric> CreateBasicMetrics(IReadOnlyList<LogEntry> logEntries)
        {
            var totalEntries = logEntries?.Count ?? 0;
            var errorEntries = 0;
            var warningEntries = 0;
            var infoEntries = 0;

            if (logEntries != null)
            {
                foreach (var entry in logEntries)
                {
                    switch (entry.Level?.ToLowerInvariant())
                    {
                        case "error":
                            errorEntries++;
                            break;
                        case "warning":
                        case "warn":
                            warningEntries++;
                            break;
                        case "info":
                        case "information":
                            infoEntries++;
                            break;
                    }
                }
            }

            return new List<DashboardMetric>
            {
                new DashboardMetric
                {
                    Name = "Total Entries",
                    Value = totalEntries,
                    DisplayValue = totalEntries.ToString("N0"),
                    Unit = "entries",
                    Type = MetricType.Info,
                    IconKey = "FileTextIcon"
                },
                new DashboardMetric
                {
                    Name = "Errors",
                    Value = errorEntries,
                    DisplayValue = errorEntries.ToString("N0"),
                    Unit = "errors",
                    Type = errorEntries > 0 ? MetricType.Error : MetricType.Success,
                    IconKey = "AlertTriangleIcon"
                },
                new DashboardMetric
                {
                    Name = "Warnings",
                    Value = warningEntries,
                    DisplayValue = warningEntries.ToString("N0"),
                    Unit = "warnings",
                    Type = warningEntries > 0 ? MetricType.Warning : MetricType.Success,
                    IconKey = "AlertIcon"
                },
                new DashboardMetric
                {
                    Name = "Info Messages",
                    Value = infoEntries,
                    DisplayValue = infoEntries.ToString("N0"),
                    Unit = "messages",
                    Type = MetricType.Info,
                    IconKey = "InfoIcon"
                }
            };
        }

        /// <summary>
        /// Updates internal log entries and caches them
        /// </summary>
        protected virtual void UpdateLogEntries(IReadOnlyList<LogEntry> logEntries)
        {
            _logEntries = logEntries ?? new List<LogEntry>();
        }
    }
} 
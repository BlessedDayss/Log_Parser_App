using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Services.Dashboard
{
    /// <summary>
    /// Service for managing dashboard types and their strategies
    /// </summary>
    public class DashboardTypeService : IDashboardTypeService
    {
        private readonly IDashboardStrategyFactory _strategyFactory;
        private IDashboardStrategy? _currentStrategy;
        private DashboardType _currentDashboardType = DashboardType.Overview;
        private DashboardData? _currentDashboardData;

        public event EventHandler<DashboardTypeChangedEventArgs>? DashboardTypeChanged;

        public DashboardType CurrentDashboardType => _currentDashboardType;

        public IReadOnlyList<DashboardType> AvailableDashboardTypes => _strategyFactory.GetAvailableDashboardTypes();

        public IDashboardStrategy CurrentStrategy => _currentStrategy ?? _strategyFactory.CreateStrategy(_currentDashboardType);

        public DashboardTypeService(IDashboardStrategyFactory strategyFactory)
        {
            _strategyFactory = strategyFactory ?? throw new ArgumentNullException(nameof(strategyFactory));
            
            // Initialize with default strategy
            _currentStrategy = _strategyFactory.CreateStrategy(_currentDashboardType);
        }

        public async Task ChangeDashboardTypeAsync(DashboardType dashboardType)
        {
            if (_currentDashboardType == dashboardType)
                return;

            var previousType = _currentDashboardType;

            try
            {
                // Cleanup current strategy
                if (_currentStrategy != null)
                {
                    await _currentStrategy.CleanupAsync();
                }

                // Create new strategy
                var newStrategy = _strategyFactory.CreateStrategy(dashboardType);
                
                // Initialize new strategy with current context if available
                if (_currentStrategy != null)
                {
                    var context = CreateCurrentContext();
                    await newStrategy.InitializeAsync(context);
                }

                // Update current values
                _currentStrategy = newStrategy;
                _currentDashboardType = dashboardType;
                _currentDashboardData = null; // Reset data to force reload

                // Fire event
                DashboardTypeChanged?.Invoke(this, new DashboardTypeChangedEventArgs(previousType, dashboardType));
            }
            catch (Exception ex)
            {
                // Revert to previous type on error
                _currentDashboardType = previousType;
                throw new InvalidOperationException($"Failed to change dashboard type to {dashboardType}", ex);
            }
        }

        public IDashboardStrategy GetStrategy(DashboardType dashboardType)
        {
            return _strategyFactory.CreateStrategy(dashboardType);
        }

        public DashboardType DetermineBestDashboardType(DashboardContext context)
        {
            var bestStrategy = _strategyFactory.GetBestStrategy(context);
            return bestStrategy.DashboardType;
        }

        public async Task RefreshCurrentDashboardAsync()
        {
            try
            {
                if (_currentStrategy != null)
                {
                    _currentDashboardData = await _currentStrategy.RefreshDataAsync();
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw - dashboard should remain functional
                Console.WriteLine($"Failed to refresh dashboard: {ex.Message}");
                
                // Create error dashboard data
                _currentDashboardData = new DashboardData
                {
                    Title = _currentStrategy?.DisplayName ?? "Dashboard",
                    Subtitle = "Error loading dashboard data",
                    ErrorMessage = ex.Message,
                    LastUpdated = DateTime.UtcNow,
                    IsLoading = false
                };
            }
        }

        public bool IsDashboardTypeAvailable(DashboardType dashboardType)
        {
            return _strategyFactory.IsSupported(dashboardType);
        }

        /// <summary>
        /// Loads dashboard data with the current strategy
        /// </summary>
        /// <param name="logEntries">Log entries to analyze</param>
        /// <returns>Dashboard data</returns>
        public async Task<DashboardData> LoadDashboardDataAsync(IReadOnlyList<LogEntry> logEntries)
        {
            try
            {
                if (_currentStrategy == null)
                {
                    _currentStrategy = _strategyFactory.CreateStrategy(_currentDashboardType);
                }

                _currentDashboardData = await _currentStrategy.LoadDashboardDataAsync(logEntries);
                return _currentDashboardData;
            }
            catch (Exception ex)
            {
                // Return error state dashboard data
                return new DashboardData
                {
                    Title = _currentStrategy?.DisplayName ?? "Dashboard",
                    Subtitle = "Error loading dashboard data",
                    ErrorMessage = ex.Message,
                    LastUpdated = DateTime.UtcNow,
                    IsLoading = false
                };
            }
        }

        /// <summary>
        /// Initializes the service with context and determines best dashboard type
        /// </summary>
        /// <param name="context">Dashboard context</param>
        /// <returns>Task representing the async operation</returns>
        public async Task InitializeWithContextAsync(DashboardContext context)
        {
            try
            {
                // Determine best dashboard type
                var bestDashboardType = DetermineBestDashboardType(context);
                
                // Change to best dashboard type if different
                if (bestDashboardType != _currentDashboardType)
                {
                    await ChangeDashboardTypeAsync(bestDashboardType);
                }

                // Initialize current strategy with context
                if (_currentStrategy != null)
                {
                    await _currentStrategy.InitializeAsync(context);
                }
            }
            catch (Exception ex)
            {
                // Fallback to Overview dashboard on error
                Console.WriteLine($"Failed to initialize with context: {ex.Message}");
                
                if (_currentDashboardType != DashboardType.Overview)
                {
                    await ChangeDashboardTypeAsync(DashboardType.Overview);
                }
            }
        }

        /// <summary>
        /// Gets the current dashboard data without reloading
        /// </summary>
        /// <returns>Current dashboard data or null if not loaded</returns>
        public DashboardData? GetCurrentDashboardData()
        {
            return _currentDashboardData;
        }

        /// <summary>
        /// Gets dashboard info for all available types
        /// </summary>
        /// <returns>List of dashboard information</returns>
        public IReadOnlyList<DashboardInfo> GetAllDashboardInfo()
        {
            var dashboardInfos = new List<DashboardInfo>();

            foreach (var dashboardType in AvailableDashboardTypes)
            {
                try
                {
                    var strategy = _strategyFactory.CreateStrategy(dashboardType);
                    dashboardInfos.Add(new DashboardInfo
                    {
                        Type = dashboardType,
                        DisplayName = strategy.DisplayName,
                        Description = strategy.Description,
                        IconKey = strategy.IconKey,
                        IsAvailable = IsDashboardTypeAvailable(dashboardType),
                        IsCurrent = dashboardType == _currentDashboardType
                    });
                }
                catch (Exception)
                {
                    // Skip strategies that fail to create
                    continue;
                }
            }

            return dashboardInfos;
        }

        private DashboardContext CreateCurrentContext()
        {
            return new DashboardContext
            {
                LoadedFiles = new List<string>(),
                IsParsingActive = false,
                ParsedEntriesCount = 0,
                ErrorCount = 0,
                HasPerformanceData = false,
                PreferredDashboardType = _currentDashboardType,
                CreatedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Dashboard information model
    /// </summary>
    public class DashboardInfo
    {
        /// <summary>
        /// Dashboard type
        /// </summary>
        public DashboardType Type { get; set; }

        /// <summary>
        /// Display name
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Icon key
        /// </summary>
        public string IconKey { get; set; } = string.Empty;

        /// <summary>
        /// Whether this dashboard is available in current context
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Whether this is the currently active dashboard
        /// </summary>
        public bool IsCurrent { get; set; }
    }
} 
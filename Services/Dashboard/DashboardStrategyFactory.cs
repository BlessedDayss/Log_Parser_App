using System;
using System.Collections.Generic;
using System.Linq;

namespace Log_Parser_App.Services.Dashboard
{
    /// <summary>
    /// Factory for creating dashboard strategies
    /// </summary>
    public class DashboardStrategyFactory : IDashboardStrategyFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<DashboardType, Type> _strategyTypes;

        public DashboardStrategyFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _strategyTypes = new Dictionary<DashboardType, Type>();
            
            RegisterDefaultStrategies();
        }

        /// <summary>
        /// Creates a strategy instance for the specified dashboard type
        /// </summary>
        /// <param name="dashboardType">The dashboard type</param>
        /// <returns>Strategy instance</returns>
        public IDashboardStrategy CreateStrategy(DashboardType dashboardType)
        {
            if (!_strategyTypes.TryGetValue(dashboardType, out var strategyType))
            {
                throw new NotSupportedException($"Dashboard type '{dashboardType}' is not supported");
            }

            var strategy = (IDashboardStrategy?)_serviceProvider.GetService(strategyType);
            if (strategy == null)
            {
                // Fallback to Activator if not registered in DI
                strategy = (IDashboardStrategy)Activator.CreateInstance(strategyType)!;
            }

            return strategy;
        }

        /// <summary>
        /// Gets all available dashboard types
        /// </summary>
        /// <returns>List of available dashboard types</returns>
        public IReadOnlyList<DashboardType> GetAvailableDashboardTypes()
        {
            return _strategyTypes.Keys.ToList();
        }

        /// <summary>
        /// Checks if a dashboard type is supported
        /// </summary>
        /// <param name="dashboardType">The dashboard type to check</param>
        /// <returns>True if supported, false otherwise</returns>
        public bool IsSupported(DashboardType dashboardType)
        {
            return _strategyTypes.ContainsKey(dashboardType);
        }

        /// <summary>
        /// Registers a strategy type for a dashboard type
        /// </summary>
        /// <param name="dashboardType">The dashboard type</param>
        /// <param name="strategyType">The strategy implementation type</param>
        public void RegisterStrategy(DashboardType dashboardType, Type strategyType)
        {
            if (strategyType == null)
                throw new ArgumentNullException(nameof(strategyType));

            if (!typeof(IDashboardStrategy).IsAssignableFrom(strategyType))
                throw new ArgumentException($"Strategy type must implement {nameof(IDashboardStrategy)}", nameof(strategyType));

            _strategyTypes[dashboardType] = strategyType;
        }

        /// <summary>
        /// Registers a strategy type using generic parameter
        /// </summary>
        /// <typeparam name="TStrategy">The strategy implementation type</typeparam>
        /// <param name="dashboardType">The dashboard type</param>
        public void RegisterStrategy<TStrategy>(DashboardType dashboardType) 
            where TStrategy : class, IDashboardStrategy
        {
            RegisterStrategy(dashboardType, typeof(TStrategy));
        }

        /// <summary>
        /// Gets the best strategy for the given context
        /// </summary>
        /// <param name="context">The dashboard context</param>
        /// <returns>The best matching strategy</returns>
        public IDashboardStrategy GetBestStrategy(DashboardContext context)
        {
            var availableStrategies = new List<(IDashboardStrategy Strategy, int Priority)>();

            foreach (var dashboardType in _strategyTypes.Keys)
            {
                try
                {
                    var strategy = CreateStrategy(dashboardType);
                    if (strategy.CanHandle(context))
                    {
                        var priority = strategy.GetPriority(context);
                        availableStrategies.Add((strategy, priority));
                    }
                }
                catch (Exception)
                {
                    // Skip strategies that fail to create
                    continue;
                }
            }

            if (!availableStrategies.Any())
            {
                // Fallback to Overview strategy
                return CreateStrategy(DashboardType.Overview);
            }

            // Return the strategy with highest priority
            return availableStrategies
                .OrderByDescending(s => s.Priority)
                .First()
                .Strategy;
        }

        private void RegisterDefaultStrategies()
        {
            RegisterStrategy<OverviewDashboardStrategy>(DashboardType.Overview);
            RegisterStrategy<FileOptionsDashboardStrategy>(DashboardType.FileOptions);
            
            // TODO: Register other strategies as they are implemented
            // RegisterStrategy<PerformanceDashboardStrategy>(DashboardType.Performance);
            // RegisterStrategy<ErrorAnalysisDashboardStrategy>(DashboardType.ErrorAnalysis);
            // RegisterStrategy<RealTimeDashboardStrategy>(DashboardType.RealTime);
        }
    }

    /// <summary>
    /// Interface for dashboard strategy factory
    /// </summary>
    public interface IDashboardStrategyFactory
    {
        /// <summary>
        /// Creates a strategy instance for the specified dashboard type
        /// </summary>
        /// <param name="dashboardType">The dashboard type</param>
        /// <returns>Strategy instance</returns>
        IDashboardStrategy CreateStrategy(DashboardType dashboardType);

        /// <summary>
        /// Gets all available dashboard types
        /// </summary>
        /// <returns>List of available dashboard types</returns>
        IReadOnlyList<DashboardType> GetAvailableDashboardTypes();

        /// <summary>
        /// Checks if a dashboard type is supported
        /// </summary>
        /// <param name="dashboardType">The dashboard type to check</param>
        /// <returns>True if supported, false otherwise</returns>
        bool IsSupported(DashboardType dashboardType);

        /// <summary>
        /// Registers a strategy type for a dashboard type
        /// </summary>
        /// <param name="dashboardType">The dashboard type</param>
        /// <param name="strategyType">The strategy implementation type</param>
        void RegisterStrategy(DashboardType dashboardType, Type strategyType);

        /// <summary>
        /// Gets the best strategy for the given context
        /// </summary>
        /// <param name="context">The dashboard context</param>
        /// <returns>The best matching strategy</returns>
        IDashboardStrategy GetBestStrategy(DashboardContext context);
    }
} 
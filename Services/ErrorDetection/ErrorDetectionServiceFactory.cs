using System;
using System.Collections.Generic;
using System.Linq;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.ErrorDetection
{
    /// <summary>
    /// Factory for creating and managing error detection strategies
    /// Implements Factory pattern for strategy creation and caching
    /// </summary>
    public class ErrorDetectionServiceFactory : IErrorDetectionServiceFactory
    {
        private readonly ILogger<ErrorDetectionServiceFactory> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<LogFormatType, IErrorDetectionStrategy> _strategyCache;

        public ErrorDetectionServiceFactory(
            ILogger<ErrorDetectionServiceFactory> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _strategyCache = new Dictionary<LogFormatType, IErrorDetectionStrategy>();
        }

        /// <summary>
        /// Creates or retrieves cached error detection strategy for specified log type
        /// </summary>
        /// <param name="logFormatType">Type of log format</param>
        /// <returns>Error detection strategy instance</returns>
        public IErrorDetectionStrategy CreateStrategy(LogFormatType logFormatType)
        {
            try
            {
                // Check cache first
                if (_strategyCache.TryGetValue(logFormatType, out var cachedStrategy))
                {
                    _logger.LogDebug("Retrieved cached error detection strategy for {LogType}", logFormatType);
                    return cachedStrategy;
                }

                // Create new strategy
                var strategy = logFormatType switch
                {
                    LogFormatType.Standard => CreateStandardStrategy(),
                    LogFormatType.IIS => CreateIISStrategy(),
                    LogFormatType.RabbitMQ => CreateRabbitMQStrategy(),
                    _ => throw new NotSupportedException($"Log format type {logFormatType} is not supported for error detection")
                };

                // Cache the strategy
                _strategyCache[logFormatType] = strategy;
                
                _logger.LogInformation("Created and cached error detection strategy for {LogType}", logFormatType);
                return strategy;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating error detection strategy for {LogType}", logFormatType);
                throw;
            }
        }

        /// <summary>
        /// Gets all available error detection strategies
        /// </summary>
        /// <returns>Collection of all available strategies</returns>
        public IEnumerable<IErrorDetectionStrategy> GetAllStrategies()
        {
            var allLogTypes = Enum.GetValues<LogFormatType>();
            
            foreach (var logType in allLogTypes)
            {
                yield return CreateStrategy(logType);
            }
        }

        /// <summary>
        /// Checks if a strategy exists for the specified log type
        /// </summary>
        /// <param name="logFormatType">Type of log format</param>
        /// <returns>True if strategy is available</returns>
        public bool IsStrategyAvailable(LogFormatType logFormatType)
        {
            try
            {
                CreateStrategy(logFormatType);
                return true;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking strategy availability for {LogType}", logFormatType);
                return false;
            }
        }

        /// <summary>
        /// Clears the strategy cache - useful for testing or runtime strategy updates
        /// </summary>
        public void ClearCache()
        {
            _strategyCache.Clear();
            _logger.LogDebug("Error detection strategy cache cleared");
        }

        private IErrorDetectionStrategy CreateStandardStrategy()
        {
            var strategy = _serviceProvider.GetService(typeof(StandardLogErrorDetectionStrategy)) as IErrorDetectionStrategy;
            if (strategy == null)
            {
                throw new InvalidOperationException("StandardLogErrorDetectionStrategy is not registered in DI container");
            }
            return strategy;
        }

        private IErrorDetectionStrategy CreateIISStrategy()
        {
            var strategy = _serviceProvider.GetService(typeof(IISLogErrorDetectionStrategy)) as IErrorDetectionStrategy;
            if (strategy == null)
            {
                throw new InvalidOperationException("IISLogErrorDetectionStrategy is not registered in DI container");
            }
            return strategy;
        }

        private IErrorDetectionStrategy CreateRabbitMQStrategy()
        {
            var strategy = _serviceProvider.GetService(typeof(RabbitMQLogErrorDetectionStrategy)) as IErrorDetectionStrategy;
            if (strategy == null)
            {
                throw new InvalidOperationException("RabbitMQLogErrorDetectionStrategy is not registered in DI container");
            }
            return strategy;
        }
    }

    /// <summary>
    /// Interface for error detection service factory
    /// </summary>
    public interface IErrorDetectionServiceFactory
    {
        /// <summary>
        /// Creates error detection strategy for specified log type
        /// </summary>
        /// <param name="logFormatType">Type of log format</param>
        /// <returns>Error detection strategy instance</returns>
        IErrorDetectionStrategy CreateStrategy(LogFormatType logFormatType);

        /// <summary>
        /// Gets all available error detection strategies
        /// </summary>
        /// <returns>Collection of all available strategies</returns>
        IEnumerable<IErrorDetectionStrategy> GetAllStrategies();

        /// <summary>
        /// Checks if a strategy exists for the specified log type
        /// </summary>
        /// <param name="logFormatType">Type of log format</param>
        /// <returns>True if strategy is available</returns>
        bool IsStrategyAvailable(LogFormatType logFormatType);

        /// <summary>
        /// Clears the strategy cache
        /// </summary>
        void ClearCache();
    }
} 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Factories
{
    /// <summary>
    /// Factory for creating appropriate log type handlers
    /// Implements Factory pattern with automatic format detection
    /// </summary>
    public class LogTypeHandlerFactory : ILogTypeHandlerFactory
    {
        private readonly ILogger<LogTypeHandlerFactory> _logger;
        private readonly IEnumerable<ILogTypeHandler> _handlers;

        public LogTypeHandlerFactory(ILogger<LogTypeHandlerFactory> logger, IEnumerable<ILogTypeHandler> handlers)
        {
            _logger = logger;
            _handlers = handlers;
        }

        /// <summary>
        /// Create handler for specific log format type
        /// </summary>
        public ILogTypeHandler CreateHandler(LogFormatType logType)
        {
            try
            {
                _logger.LogDebug($"Creating handler for log type: {logType}");

                var handler = _handlers.FirstOrDefault(h => h.SupportedLogType == logType);
                if (handler == null)
                {
                    _logger.LogWarning($"No handler found for log type: {logType}");
                    throw new NotSupportedException($"No handler available for log type: {logType}");
                }

                _logger.LogDebug($"Handler created for log type: {logType} -> {handler.GetType().Name}");
                return handler;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating handler for log type: {logType}");
                throw;
            }
        }

        /// <summary>
        /// Automatically detect log format and create appropriate handler
        /// </summary>
        public async Task<ILogTypeHandler> CreateHandlerAsync(string filePath)
        {
            try
            {
                _logger.LogDebug($"Auto-detecting log format for file: {filePath}");

                if (string.IsNullOrEmpty(filePath))
                    throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

                if (!System.IO.File.Exists(filePath))
                    throw new System.IO.FileNotFoundException($"File not found: {filePath}");

                // Try each handler to see which one can handle the file
                foreach (var handler in _handlers)
                {
                    try
                    {
                        _logger.LogDebug($"Testing handler: {handler.GetType().Name} for file: {filePath}");

                        if (await handler.CanHandleAsync(filePath))
                        {
                            _logger.LogInformation($"Auto-detected log format: {handler.SupportedLogType} for file: {filePath}");
                            return handler;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Handler {handler.GetType().Name} failed to validate file: {filePath}");
                        // Continue to next handler
                    }
                }

                // If no specific handler can handle it, return standard handler as fallback
                var standardHandler = _handlers.FirstOrDefault(h => h.SupportedLogType == LogFormatType.Standard);
                if (standardHandler != null)
                {
                    _logger.LogWarning($"No specific handler found for file: {filePath}, using standard handler as fallback");
                    return standardHandler;
                }

                throw new NotSupportedException($"No suitable handler found for file: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error auto-detecting log format for file: {filePath}");
                throw;
            }
        }

        /// <summary>
        /// Get all available log format types
        /// </summary>
        public IEnumerable<LogFormatType> GetSupportedLogTypes()
        {
            try
            {
                var supportedTypes = _handlers.Select(h => h.SupportedLogType).Distinct().ToList();
                _logger.LogDebug($"Supported log types: {string.Join(", ", supportedTypes)}");
                return supportedTypes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supported log types");
                return Enumerable.Empty<LogFormatType>();
            }
        }

        /// <summary>
        /// Get handler for specific log format type if available
        /// </summary>
        public ILogTypeHandler? GetHandler(LogFormatType logType)
        {
            try
            {
                var handler = _handlers.FirstOrDefault(h => h.SupportedLogType == logType);
                if (handler == null)
                {
                    _logger.LogDebug($"No handler available for log type: {logType}");
                }
                return handler;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting handler for log type: {logType}");
                return null;
            }
        }

        /// <summary>
        /// Check if specific log format type is supported
        /// </summary>
        public bool IsLogTypeSupported(LogFormatType logType)
        {
            try
            {
                return _handlers.Any(h => h.SupportedLogType == logType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if log type is supported: {logType}");
                return false;
            }
        }

        /// <summary>
        /// Get processing options for specific log format type
        /// </summary>
        public LogProcessingOptions? GetProcessingOptions(LogFormatType logType)
        {
            try
            {
                var handler = GetHandler(logType);
                if (handler == null)
                {
                    _logger.LogWarning($"No handler found for log type: {logType}");
                    return null;
                }

                var options = handler.GetProcessingOptions();
                _logger.LogDebug($"Retrieved processing options for log type: {logType}");
                return options;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting processing options for log type: {logType}");
                return null;
            }
        }

        /// <summary>
        /// Validate multiple files and group by detected log format
        /// </summary>
        public async Task<Dictionary<LogFormatType, List<string>>> GroupFilesByLogTypeAsync(IEnumerable<string> filePaths)
        {
            var result = new Dictionary<LogFormatType, List<string>>();

            try
            {
                _logger.LogDebug($"Grouping {filePaths.Count()} files by log type");

                foreach (var filePath in filePaths)
                {
                    try
                    {
                        var handler = await CreateHandlerAsync(filePath);
                        var logType = handler.SupportedLogType;

                        if (!result.ContainsKey(logType))
                        {
                            result[logType] = new List<string>();
                        }

                        result[logType].Add(filePath);
                        _logger.LogDebug($"File {filePath} grouped as {logType}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Could not determine log type for file: {filePath}");
                        
                        // Add to standard type as fallback
                        if (!result.ContainsKey(LogFormatType.Standard))
                        {
                            result[LogFormatType.Standard] = new List<string>();
                        }
                        result[LogFormatType.Standard].Add(filePath);
                    }
                }

                _logger.LogInformation($"Grouped files by log type: {string.Join(", ", result.Select(kvp => $"{kvp.Key}: {kvp.Value.Count}"))}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error grouping files by log type");
                return result;
            }
        }

        /// <summary>
        /// Get recommended batch size for specific log format type
        /// </summary>
        public int GetRecommendedBatchSize(LogFormatType logType)
        {
            try
            {
                var options = GetProcessingOptions(logType);
                if (options != null)
                {
                    return options.RecommendedBatchSize;
                }

                // Default batch sizes by log type
                return logType switch
                {
                    LogFormatType.Standard => 1000,
                    LogFormatType.IIS => 5000,
                    LogFormatType.RabbitMQ => 2000,
                    _ => 1000
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting recommended batch size for log type: {logType}");
                return 1000; // Safe default
            }
        }

        /// <summary>
        /// Get maximum recommended file size for specific log format type
        /// </summary>
        public long GetMaxFileSize(LogFormatType logType)
        {
            try
            {
                var options = GetProcessingOptions(logType);
                if (options != null)
                {
                    return options.MaxFileSize;
                }

                // Default max file sizes by log type (in bytes)
                return logType switch
                {
                    LogFormatType.Standard => 500 * 1024 * 1024, // 500MB
                    LogFormatType.IIS => 1024 * 1024 * 1024, // 1GB
                    LogFormatType.RabbitMQ => 500 * 1024 * 1024, // 500MB
                    _ => 500 * 1024 * 1024 // 500MB default
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting max file size for log type: {logType}");
                return 500 * 1024 * 1024; // Safe default
            }
        }

        /// <summary>
        /// Validate if file size is acceptable for its detected log type
        /// </summary>
        public async Task<bool> ValidateFileSizeAsync(string filePath)
        {
            try
            {
                if (!System.IO.File.Exists(filePath))
                    return false;

                var fileInfo = new System.IO.FileInfo(filePath);
                var handler = await CreateHandlerAsync(filePath);
                var maxSize = GetMaxFileSize(handler.SupportedLogType);

                var isValid = fileInfo.Length <= maxSize;
                
                if (!isValid)
                {
                    _logger.LogWarning($"File {filePath} exceeds max size for {handler.SupportedLogType}: {fileInfo.Length} > {maxSize}");
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating file size for: {filePath}");
                return false;
            }
        }

        /// <summary>
        /// Get performance statistics for all registered handlers
        /// </summary>
        public HandlerPerformanceStats GetPerformanceStats()
        {
            try
            {
                var stats = new HandlerPerformanceStats
                {
                    TotalHandlers = _handlers.Count(),
                    HandlerTypes = _handlers.Select(h => new HandlerTypeInfo
                    {
                        LogType = h.SupportedLogType,
                        HandlerName = h.GetType().Name,
                        ProcessingOptions = h.GetProcessingOptions()
                    }).ToList(),
                    SupportedTypes = GetSupportedLogTypes().ToList()
                };

                _logger.LogDebug($"Performance stats retrieved: {stats.TotalHandlers} handlers, {stats.SupportedTypes.Count} types");
                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance stats");
                return new HandlerPerformanceStats
                {
                    TotalHandlers = 0,
                    HandlerTypes = new List<HandlerTypeInfo>(),
                    SupportedTypes = new List<LogFormatType>()
                };
            }
        }

        /// <summary>
        /// Register new log type handler
        /// </summary>
        public void RegisterHandler(ILogTypeHandler handler)
        {
            throw new NotSupportedException("Dynamic handler registration not supported in this implementation. Handlers are registered via DI.");
        }

        /// <summary>
        /// Unregister log type handler
        /// </summary>
        public bool UnregisterHandler(LogFormatType logType)
        {
            throw new NotSupportedException("Dynamic handler unregistration not supported in this implementation. Handlers are managed via DI.");
        }

        /// <summary>
        /// Check if handler for log type is available
        /// </summary>
        public bool IsHandlerAvailable(LogFormatType logType)
        {
            return IsLogTypeSupported(logType);
        }

        /// <summary>
        /// Get all registered handlers
        /// </summary>
        public IEnumerable<ILogTypeHandler> GetAllHandlers()
        {
            return _handlers;
        }

        /// <summary>
        /// Auto-detect log type from file path/content
        /// </summary>
        public async Task<LogFormatType> DetectLogType(string filePath, string? fileName = null)
        {
            try
            {
                var handler = await CreateHandlerAsync(filePath);
                return handler.SupportedLogType;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error detecting log type for file: {filePath}");
                return LogFormatType.Standard;
            }
        }

        /// <summary>
        /// Process multiple log files in batch
        /// </summary>
        public async Task<Dictionary<string, IEnumerable<LogEntry>>> ProcessBatchAsync(IEnumerable<string> filePaths)
        {
            var results = new Dictionary<string, IEnumerable<LogEntry>>();
            var tasks = filePaths.Select(async filePath =>
            {
                try
                {
                    var handler = await CreateHandlerAsync(filePath);
                    var entries = await handler.ParseLogFileAsync(filePath);
                    return new { FilePath = filePath, Entries = entries };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing file in batch: {filePath}");
                    return new { FilePath = filePath, Entries = Enumerable.Empty<LogEntry>() };
                }
            });

            var completed = await Task.WhenAll(tasks);
            foreach (var result in completed)
            {
                results[result.FilePath] = result.Entries;
            }

            return results;
        }

        /// <summary>
        /// Validate file can be processed by any registered handler
        /// </summary>
        public async Task<bool> CanProcessFileAsync(string filePath)
        {
            try
            {
                foreach (var handler in _handlers)
                {
                    if (await handler.CanHandleAsync(filePath))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if file can be processed: {filePath}");
                return false;
            }
        }

        /// <summary>
        /// Get handler that can process the given file
        /// </summary>
        public async Task<ILogTypeHandler?> GetHandlerForFileAsync(string filePath)
        {
            try
            {
                foreach (var handler in _handlers)
                {
                    if (await handler.CanHandleAsync(filePath))
                    {
                        return handler;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting handler for file: {filePath}");
                return null;
            }
        }

        /// <summary>
        /// Get performance statistics for all handlers
        /// </summary>
        public Dictionary<LogFormatType, Dictionary<string, double>> GetPerformanceStatistics()
        {
            var stats = new Dictionary<LogFormatType, Dictionary<string, double>>();
            foreach (var handler in _handlers)
            {
                stats[handler.SupportedLogType] = new Dictionary<string, double>
                {
                    ["SupportedLogType"] = (double)handler.SupportedLogType,
                    ["HandlerAvailable"] = 1.0
                };
            }
            return stats;
        }
    }

    #region Supporting Classes

    /// <summary>
    /// Performance statistics for log type handlers
    /// </summary>
    public class HandlerPerformanceStats
    {
        public int TotalHandlers { get; set; }
        public List<HandlerTypeInfo> HandlerTypes { get; set; } = new();
        public List<LogFormatType> SupportedTypes { get; set; } = new();
    }

    /// <summary>
    /// Information about a specific handler type
    /// </summary>
    public class HandlerTypeInfo
    {
        public LogFormatType LogType { get; set; }
        public string HandlerName { get; set; } = string.Empty;
        public LogProcessingOptions ProcessingOptions { get; set; } = new();
    }

    #endregion
} 
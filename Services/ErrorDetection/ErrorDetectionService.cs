using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.ErrorDetection
{
    /// <summary>
    /// Main error detection service that orchestrates different strategies
    /// Implements SOLID principles with dependency injection and Strategy pattern
    /// </summary>
    public class ErrorDetectionService : IErrorDetectionService
    {
        private readonly ILogger<ErrorDetectionService> _logger;
        private readonly IErrorDetectionServiceFactory _strategyFactory;

        public ErrorDetectionService(
            ILogger<ErrorDetectionService> logger,
            IErrorDetectionServiceFactory strategyFactory)
        {
            _logger = logger;
            _strategyFactory = strategyFactory;
        }

        /// <summary>
        /// Detects error entries from a collection of log entries based on the log format type
        /// </summary>
        /// <param name="logEntries">Collection of log entries to analyze</param>
        /// <param name="logFormatType">Type of log format (Standard, IIS, RabbitMQ)</param>
        /// <returns>Collection of entries that are considered errors</returns>
        public async Task<IEnumerable<LogEntry>> DetectErrorsAsync(IEnumerable<LogEntry> logEntries, LogFormatType logFormatType)
        {
            try
            {
                _logger.LogDebug("Detecting errors for {LogType} with {EntryCount} entries", logFormatType, logEntries.Count());
                
                var strategy = _strategyFactory.CreateStrategy(logFormatType);
                var errorEntries = await strategy.DetectErrorsAsync(logEntries);
                
                var errorList = errorEntries.ToList();
                _logger.LogInformation("Detected {ErrorCount} errors from {TotalCount} {LogType} entries", 
                    errorList.Count, logEntries.Count(), logFormatType);
                    
                return errorList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting errors for {LogType}", logFormatType);
                return Enumerable.Empty<LogEntry>();
            }
        }

        /// <summary>
        /// Detects error entries from IIS log entries
        /// </summary>
        /// <param name="iisLogEntries">Collection of IIS log entries to analyze</param>
        /// <returns>Collection of IIS entries that are considered errors</returns>
        public async Task<IEnumerable<IisLogEntry>> DetectIISErrorsAsync(IEnumerable<IisLogEntry> iisLogEntries)
        {
            try
            {
                _logger.LogDebug("Detecting IIS errors with {EntryCount} entries", iisLogEntries.Count());
                
                var strategy = _strategyFactory.CreateStrategy(LogFormatType.IIS) as IIISErrorDetectionStrategy;
                if (strategy == null)
                {
                    _logger.LogError("IIS error detection strategy not found or not implementing IIISErrorDetectionStrategy");
                    return Enumerable.Empty<IisLogEntry>();
                }
                
                var errorEntries = await strategy.DetectIISErrorsAsync(iisLogEntries);
                
                var errorList = errorEntries.ToList();
                _logger.LogInformation("Detected {ErrorCount} IIS errors from {TotalCount} entries", 
                    errorList.Count, iisLogEntries.Count());
                    
                return errorList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting IIS errors");
                return Enumerable.Empty<IisLogEntry>();
            }
        }

        /// <summary>
        /// Detects error entries from RabbitMQ log entries
        /// </summary>
        /// <param name="rabbitMqLogEntries">Collection of RabbitMQ log entries to analyze</param>
        /// <returns>Collection of RabbitMQ entries that are considered errors</returns>
        public async Task<IEnumerable<RabbitMqLogEntry>> DetectRabbitMQErrorsAsync(IEnumerable<RabbitMqLogEntry> rabbitMqLogEntries)
        {
            try
            {
                _logger.LogDebug("Detecting RabbitMQ errors with {EntryCount} entries", rabbitMqLogEntries.Count());
                
                var strategy = _strategyFactory.CreateStrategy(LogFormatType.RabbitMQ) as IRabbitMQErrorDetectionStrategy;
                if (strategy == null)
                {
                    _logger.LogError("RabbitMQ error detection strategy not found or not implementing IRabbitMQErrorDetectionStrategy");
                    return Enumerable.Empty<RabbitMqLogEntry>();
                }
                
                var errorEntries = await strategy.DetectRabbitMQErrorsAsync(rabbitMqLogEntries);
                
                var errorList = errorEntries.ToList();
                _logger.LogInformation("Detected {ErrorCount} RabbitMQ errors from {TotalCount} entries", 
                    errorList.Count, rabbitMqLogEntries.Count());
                    
                return errorList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting RabbitMQ errors");
                return Enumerable.Empty<RabbitMqLogEntry>();
            }
        }

        /// <summary>
        /// Checks if a single log entry is considered an error based on the log format type
        /// </summary>
        /// <param name="logEntry">Log entry to check</param>
        /// <param name="logFormatType">Type of log format</param>
        /// <returns>True if the entry is considered an error</returns>
        public bool IsError(LogEntry logEntry, LogFormatType logFormatType)
        {
            try
            {
                var strategy = _strategyFactory.CreateStrategy(logFormatType);
                return strategy.IsError(logEntry);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking if entry is error for {LogType}", logFormatType);
                return false;
            }
        }

        /// <summary>
        /// Checks if a single IIS log entry is considered an error
        /// </summary>
        /// <param name="iisLogEntry">IIS log entry to check</param>
        /// <returns>True if the entry is considered an error</returns>
        public bool IsIISError(IisLogEntry iisLogEntry)
        {
            try
            {
                var strategy = _strategyFactory.CreateStrategy(LogFormatType.IIS) as IIISErrorDetectionStrategy;
                return strategy?.IsIISError(iisLogEntry) ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking if IIS entry is error");
                return false;
            }
        }

        /// <summary>
        /// Checks if a single RabbitMQ log entry is considered an error
        /// </summary>
        /// <param name="rabbitMqLogEntry">RabbitMQ log entry to check</param>
        /// <returns>True if the entry is considered an error</returns>
        public bool IsRabbitMQError(RabbitMqLogEntry rabbitMqLogEntry)
        {
            try
            {
                var strategy = _strategyFactory.CreateStrategy(LogFormatType.RabbitMQ) as IRabbitMQErrorDetectionStrategy;
                return strategy?.IsRabbitMQError(rabbitMqLogEntry) ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking if RabbitMQ entry is error");
                return false;
            }
        }

        /// <summary>
        /// Gets the error detection strategy for a specific log format type
        /// </summary>
        /// <param name="logFormatType">Type of log format</param>
        /// <returns>Error detection strategy instance</returns>
        public IErrorDetectionStrategy GetStrategy(LogFormatType logFormatType)
        {
            try
            {
                return _strategyFactory.CreateStrategy(logFormatType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting strategy for {LogType}", logFormatType);
                throw;
            }
        }
    }
} 
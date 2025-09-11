using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services
{
    /// <summary>
    /// Repository implementation for IIS log data access
    /// Single Responsibility: Data access operations for IIS logs only
    /// Dependency Inversion: Depends on abstractions (IIISLogParserService)
    /// </summary>
    public class IISRepository : IIISRepository
    {
        private readonly IIISLogParserService _parserService;
        private readonly ILogger<IISRepository> _logger;

        public IISRepository(IIISLogParserService parserService, ILogger<IISRepository> logger)
        {
            _parserService = parserService ?? throw new ArgumentNullException(nameof(parserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Load IIS entries from multiple file paths asynchronously
        /// </summary>
        public async Task<IEnumerable<IisLogEntry>> LoadIISLogsAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default)
        {
            var allEntries = new List<IisLogEntry>();
            var semaphore = new SemaphoreSlim(Environment.ProcessorCount * 2);
            var tasks = new List<Task>();

            _logger.LogInformation("Starting to load {FileCount} IIS log files", filePaths.Count());

            foreach (var filePath in filePaths)
            {
                await semaphore.WaitAsync(cancellationToken);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var entries = await LoadIISLogAsync(filePath, cancellationToken);
                        lock (allEntries)
                        {
                            allEntries.AddRange(entries);
                        }
                        _logger.LogDebug("Successfully loaded {EntryCount} entries from {FilePath}", entries.Count(), filePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load IIS log from {FilePath}", filePath);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);
            
            // Sort by timestamp for consistent ordering
            var sortedEntries = allEntries.OrderBy(e => e.DateTime ?? DateTimeOffset.MinValue).ToList();
            
            _logger.LogInformation("Loaded total of {TotalEntries} IIS log entries from {FileCount} files", 
                sortedEntries.Count, filePaths.Count());

            return sortedEntries;
        }

        /// <summary>
        /// Load IIS entries from a single file path asynchronously
        /// </summary>
        public async Task<IEnumerable<IisLogEntry>> LoadIISLogAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"IIS log file not found: {filePath}");

            var entries = new List<IisLogEntry>();

            try
            {
                _logger.LogDebug("Loading IIS log from {FilePath}", filePath);

                await foreach (var entry in _parserService.ParseLogFileAsync(filePath, cancellationToken))
                {
                    entries.Add(entry);
                }

                _logger.LogDebug("Successfully loaded {EntryCount} entries from {FilePath}", entries.Count, filePath);
                return entries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading IIS log from {FilePath}", filePath);
                throw;
            }
        }

        /// <summary>
        /// Validate if files are IIS log format
        /// </summary>
        public async Task<Dictionary<string, bool>> ValidateIISFilesAsync(IEnumerable<string> filePaths)
        {
            var results = new Dictionary<string, bool>();
            var tasks = new List<Task>();

            foreach (var filePath in filePaths)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var isValid = await ValidateIISFileFormat(filePath);
                        lock (results)
                        {
                            results[filePath] = isValid;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error validating IIS file {FilePath}", filePath);
                        lock (results)
                        {
                            results[filePath] = false;
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            return results;
        }

        /// <summary>
        /// Get IIS log statistics for given entries
        /// </summary>
        public IISLogStatistics GetStatistics(IEnumerable<IisLogEntry> entries)
        {
            var entriesList = entries.ToList();
            
            if (!entriesList.Any())
                return new IISLogStatistics();

            var stats = new IISLogStatistics
            {
                TotalRequests = entriesList.Count,
                ErrorRequests = entriesList.Count(e => (e.HttpStatus ?? 0) >= 400),
                InfoRequests = entriesList.Count(e => (e.HttpStatus ?? 0) >= 200 && (e.HttpStatus ?? 0) < 300),
                RedirectRequests = entriesList.Count(e => (e.HttpStatus ?? 0) >= 300 && (e.HttpStatus ?? 0) < 400),
                FirstLogTime = entriesList.Where(e => e.DateTime.HasValue).Min(e => e.DateTime),
                LastLogTime = entriesList.Where(e => e.DateTime.HasValue).Max(e => e.DateTime),
                StatusCodeDistribution = entriesList
                    .Where(e => e.HttpStatus.HasValue)
                    .GroupBy(e => e.HttpStatus!.Value)
                    .ToDictionary(g => g.Key, g => g.Count()),
                MethodDistribution = entriesList
                    .Where(e => !string.IsNullOrEmpty(e.Method))
                    .GroupBy(e => e.Method!)
                    .ToDictionary(g => g.Key, g => g.Count()),
                IPAddressDistribution = entriesList
                    .Where(e => !string.IsNullOrEmpty(e.ClientIPAddress))
                    .GroupBy(e => e.ClientIPAddress!)
                    .ToDictionary(g => g.Key, g => g.Count()),
                TotalBytesTransferred = entriesList.Where(e => e.BytesSent.HasValue).Sum(e => e.BytesSent!.Value),
                AverageResponseTime = entriesList.Where(e => e.TimeTaken.HasValue).Any() ? 
                    entriesList.Where(e => e.TimeTaken.HasValue).Average(e => e.TimeTaken!.Value) : 0
            };

            stats.ErrorRate = stats.TotalRequests > 0 ? (double)stats.ErrorRequests / stats.TotalRequests * 100 : 0;
            
            if (stats.FirstLogTime.HasValue && stats.LastLogTime.HasValue)
            {
                stats.LogDuration = stats.LastLogTime.Value - stats.FirstLogTime.Value;
            }

            _logger.LogDebug("Generated statistics for {TotalRequests} IIS requests with {ErrorRate}% error rate", 
                stats.TotalRequests, stats.ErrorRate.ToString("F2"));

            return stats;
        }

        /// <summary>
        /// Filter IIS entries based on criteria
        /// </summary>
        public IEnumerable<IisLogEntry> FilterEntries(IEnumerable<IisLogEntry> entries, IISFilterCriteria criteria)
        {
            if (criteria?.Criteria == null || !criteria.Criteria.Any())
                return entries;

            var filteredEntries = entries.AsQueryable();

            foreach (var criterion in criteria.Criteria)
            {
                filteredEntries = ApplySingleCriterion(filteredEntries, criterion);
            }

            var result = filteredEntries.ToList();
            _logger.LogDebug("Filtered {OriginalCount} entries to {FilteredCount} using {CriteriaCount} criteria", 
                entries.Count(), result.Count, criteria.Criteria.Count);

            return result;
        }

        #region Private Methods

        /// <summary>
        /// Validate if a single file is in IIS log format
        /// </summary>
        private async Task<bool> ValidateIISFileFormat(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                using var reader = new StreamReader(filePath);
                var headerLines = new List<string>();
                
                // Read first 20 lines to check for IIS headers
                for (int i = 0; i < 20 && !reader.EndOfStream; i++)
                {
                    var line = await reader.ReadLineAsync();
                    if (!string.IsNullOrEmpty(line))
                        headerLines.Add(line);
                }

                // Check for IIS log indicators
                return headerLines.Any(line =>
                    line.StartsWith("#Software: Microsoft Internet Information Services") ||
                    line.StartsWith("#Version:") ||
                    line.StartsWith("#Fields:") ||
                    (line.Contains("GET") || line.Contains("POST")) && 
                    System.Text.RegularExpressions.Regex.IsMatch(line, @"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}")
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating IIS file format for {FilePath}", filePath);
                return false;
            }
        }

        /// <summary>
        /// Apply a single filter criterion to the query
        /// </summary>
        private IQueryable<IisLogEntry> ApplySingleCriterion(IQueryable<IisLogEntry> entries, IISFilterCriterion criterion)
        {
            if (criterion?.SelectedField == null || string.IsNullOrEmpty(criterion.SelectedOperator))
                return entries;

            var filterValue = criterion.Value?.ToLowerInvariant() ?? string.Empty;

            return criterion.SelectedField switch
            {
                IISLogField.ClientIP => ApplyStringFilter(entries, e => e.ClientIPAddress, criterion.SelectedOperator, filterValue),
                IISLogField.Method => ApplyStringFilter(entries, e => e.Method, criterion.SelectedOperator, filterValue),
                IISLogField.UriStem => ApplyStringFilter(entries, e => e.UriStem, criterion.SelectedOperator, filterValue),
                IISLogField.UriQuery => ApplyStringFilter(entries, e => e.UriQuery, criterion.SelectedOperator, filterValue),
                IISLogField.UserName => ApplyStringFilter(entries, e => e.UserName, criterion.SelectedOperator, filterValue),
                IISLogField.UserAgent => ApplyStringFilter(entries, e => e.UserAgent, criterion.SelectedOperator, filterValue),
                IISLogField.HttpStatus => ApplyNumericFilter(entries, e => e.HttpStatus, criterion.SelectedOperator, criterion.Value),
                IISLogField.TimeTaken => ApplyNumericFilter(entries, e => e.TimeTaken, criterion.SelectedOperator, criterion.Value),
                IISLogField.Port => ApplyNumericFilter(entries, e => e.ServerPort, criterion.SelectedOperator, criterion.Value),
                _ => entries
            };
        }

        /// <summary>
        /// Apply string-based filtering
        /// </summary>
        private IQueryable<IisLogEntry> ApplyStringFilter(IQueryable<IisLogEntry> entries, 
            System.Linq.Expressions.Expression<Func<IisLogEntry, string?>> selector, 
            string operation, string value)
        {
            return operation.ToLowerInvariant() switch
            {
                "equals" => entries.Where(e => selector.Compile()(e) != null && selector.Compile()(e)!.ToLowerInvariant() == value),
                "notequals" => entries.Where(e => selector.Compile()(e) == null || selector.Compile()(e)!.ToLowerInvariant() != value),
                "contains" => entries.Where(e => selector.Compile()(e) != null && selector.Compile()(e)!.ToLowerInvariant().Contains(value)),
                "notcontains" => entries.Where(e => selector.Compile()(e) == null || !selector.Compile()(e)!.ToLowerInvariant().Contains(value)),
                _ => entries
            };
        }

        /// <summary>
        /// Apply numeric-based filtering
        /// </summary>
        private IQueryable<IisLogEntry> ApplyNumericFilter(IQueryable<IisLogEntry> entries,
            System.Linq.Expressions.Expression<Func<IisLogEntry, int?>> selector,
            string operation, string? value)
        {
            if (!int.TryParse(value, out var numericValue))
                return entries;

            return operation.ToLowerInvariant() switch
            {
                "equals" => entries.Where(e => selector.Compile()(e) == numericValue),
                "notequals" => entries.Where(e => selector.Compile()(e) != numericValue),
                "greaterthan" => entries.Where(e => selector.Compile()(e) > numericValue),
                "lessthan" => entries.Where(e => selector.Compile()(e) < numericValue),
                _ => entries
            };
        }

        #endregion
    }
} 
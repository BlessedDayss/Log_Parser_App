using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services
{
    /// <summary>
    /// High-performance IIS analytics service implementing Hybrid Memory-Stream Architecture
    /// Optimized for processing 4.7M+ records with minimal memory footprint and UI responsiveness
    /// </summary>
    public class IISAnalyticsService : IIISAnalyticsService
    {
        private readonly ILogger<IISAnalyticsService> _logger;
        private const int BatchSize = 50000; // Process in 50K record chunks for optimal memory usage
        private const int TopResultsCount = 3; // TOP 3 results for each metric type

        public IISAnalyticsService(ILogger<IISAnalyticsService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Process IIS analytics with batched streaming approach for optimal performance
        /// </summary>
        public async Task<IISAnalyticsResult> ProcessAnalyticsAsync(
            IEnumerable<IisLogEntry> logEntries,
            IProgress<AnalyticsProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var entriesArray = logEntries.ToArray();
            var totalRecords = entriesArray.Length;

            _logger.LogInformation("Starting IIS analytics processing for {RecordCount} records", totalRecords);

            try
            {
                // Initialize concurrent collections for thread-safe aggregation
                var statusCodes = new ConcurrentDictionary<int, int>();
                var longestRequests = new ConcurrentBag<IisLogEntry>();
                var httpMethods = new ConcurrentDictionary<string, int>();
                var userActivity = new ConcurrentDictionary<string, int>();

                // Process in batches using Task.Run to avoid UI thread blocking
                await Task.Run(() => ProcessBatchesInParallel(
                    entriesArray, 
                    statusCodes, 
                    longestRequests, 
                    httpMethods, 
                    userActivity, 
                    progress, 
                    totalRecords, 
                    cancellationToken), cancellationToken).ConfigureAwait(false);

                // Generate final analytics results
                var result = await GenerateAnalyticsResultAsync(
                    statusCodes, 
                    longestRequests, 
                    httpMethods, 
                    userActivity, 
                    totalRecords, 
                    cancellationToken).ConfigureAwait(false);

                result.TotalRecordsProcessed = totalRecords;
                result.ProcessingTime = stopwatch.Elapsed;

                _logger.LogInformation("IIS analytics processing completed in {Duration}ms for {RecordCount} records", 
                    stopwatch.ElapsedMilliseconds, totalRecords);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("IIS analytics processing cancelled after {Duration}ms", stopwatch.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing IIS analytics for {RecordCount} records", totalRecords);
                throw;
            }
        }

        /// <summary>
        /// Process data in parallel batches with memory optimization
        /// </summary>
        private void ProcessBatchesInParallel(
            IisLogEntry[] entries,
            ConcurrentDictionary<int, int> statusCodes,
            ConcurrentBag<IisLogEntry> longestRequests,
            ConcurrentDictionary<string, int> httpMethods,
            ConcurrentDictionary<string, int> userActivity,
            IProgress<AnalyticsProgress>? progress,
            int totalRecords,
            CancellationToken cancellationToken)
        {
            var processed = 0;
            var batchCount = (entries.Length + BatchSize - 1) / BatchSize;

            // Process batches in parallel with optimal CPU utilization
            Parallel.For(0, batchCount, new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, batchIndex =>
            {
                var startIndex = batchIndex * BatchSize;
                var endIndex = Math.Min(startIndex + BatchSize, entries.Length);
                var batchSpan = entries.AsSpan(startIndex, endIndex - startIndex);

                ProcessBatch(batchSpan, statusCodes, longestRequests, httpMethods, userActivity);

                // Update progress with thread-safe increment
                var currentProcessed = Interlocked.Add(ref processed, batchSpan.Length);
                progress?.Report(new AnalyticsProgress
                {
                    ProcessedRecords = currentProcessed,
                    TotalRecords = totalRecords,
                    CurrentOperation = $"Processing batch {batchIndex + 1}/{batchCount}"
                });
            });
        }

        /// <summary>
        /// Process single batch with zero-allocation using Span<T>
        /// </summary>
        private void ProcessBatch(
            Span<IisLogEntry> batch,
            ConcurrentDictionary<int, int> statusCodes,
            ConcurrentBag<IisLogEntry> longestRequests,
            ConcurrentDictionary<string, int> httpMethods,
            ConcurrentDictionary<string, int> userActivity)
        {
            foreach (ref readonly var entry in batch)
            {
                // Status code aggregation
                if (entry.HttpStatus.HasValue)
                {
                    statusCodes.AddOrUpdate(entry.HttpStatus.Value, 1, (key, value) => value + 1);
                }

                // Collect potential longest requests (we'll filter TOP 3 later)
                if (entry.TimeTaken.HasValue && entry.TimeTaken.Value > 100) // Only requests > 100ms
                {
                    longestRequests.Add(entry);
                }

                // HTTP method aggregation
                if (!string.IsNullOrEmpty(entry.Method))
                {
                    httpMethods.AddOrUpdate(entry.Method, 1, (key, value) => value + 1);
                }

                // User activity aggregation
                if (!string.IsNullOrEmpty(entry.UserName) && entry.UserName != "-")
                {
                    var cleanUsername = entry.UserName.Replace("+", " ");
                    userActivity.AddOrUpdate(cleanUsername, 1, (key, value) => value + 1);
                }
            }
        }

        /// <summary>
        /// Generate final analytics results with TOP 3 filtering
        /// </summary>
        private async Task<IISAnalyticsResult> GenerateAnalyticsResultAsync(
            ConcurrentDictionary<int, int> statusCodes,
            ConcurrentBag<IisLogEntry> longestRequests,
            ConcurrentDictionary<string, int> httpMethods,
            ConcurrentDictionary<string, int> userActivity,
            int totalRecords,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var result = new IISAnalyticsResult();

                // Generate TOP 3 status codes
                result.TopStatusCodes = GenerateTopStatusCodes(statusCodes, totalRecords);

                // Generate TOP 3 longest requests
                result.LongestRequests = GenerateLongestRequests(longestRequests);

                // Generate HTTP methods distribution
                result.HttpMethods = GenerateHttpMethodsDistribution(httpMethods, totalRecords);

                // Generate TOP 3 users
                result.TopUsers = GenerateTopUsers(userActivity, totalRecords);

                return result;
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Generate TOP 3 HTTP status codes with color themes
        /// </summary>
        private IISStatusAnalysis[] GenerateTopStatusCodes(ConcurrentDictionary<int, int> statusCodes, int totalRecords)
        {
            return statusCodes
                .OrderByDescending(kvp => kvp.Value)
                .Take(TopResultsCount)
                .Select(kvp => new IISStatusAnalysis
                {
                    StatusCode = kvp.Key,
                    Count = kvp.Value,
                    Percentage = (double)kvp.Value / totalRecords * 100,
                    StatusDescription = GetStatusDescription(kvp.Key),
                    ColorTheme = GetStatusColorTheme(kvp.Key)
                })
                .ToArray();
        }

        /// <summary>
        /// Generate TOP 3 longest requests by time-taken
        /// </summary>
        private IISLongestRequest[] GenerateLongestRequests(ConcurrentBag<IisLogEntry> longestRequests)
        {
            return longestRequests
                .Where(entry => entry.TimeTaken.HasValue)
                .OrderByDescending(entry => entry.TimeTaken!.Value)
                .Take(TopResultsCount)
                .Select(entry => new IISLongestRequest
                {
                    UriStem = TruncateUri(entry.UriStem ?? string.Empty),
                    TimeTaken = entry.TimeTaken!.Value,
                    FormattedTime = FormatTimeTaken(entry.TimeTaken!.Value),
                    Method = entry.Method ?? string.Empty,
                    StatusCode = entry.HttpStatus.GetValueOrDefault(),
                    Timestamp = entry.DateTime?.DateTime ?? DateTime.MinValue
                })
                .ToArray();
        }

        /// <summary>
        /// Generate HTTP methods distribution with percentages
        /// </summary>
        private IISMethodDistribution[] GenerateHttpMethodsDistribution(ConcurrentDictionary<string, int> httpMethods, int totalRecords)
        {
            return httpMethods
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => new IISMethodDistribution
                {
                    Method = kvp.Key,
                    Count = kvp.Value,
                    Percentage = (double)kvp.Value / totalRecords * 100,
                    FormattedDisplay = $"{kvp.Key} ({(double)kvp.Value / totalRecords * 100:F1}%)"
                })
                .ToArray();
        }

        /// <summary>
        /// Generate TOP 3 users by request count
        /// </summary>
        private IISUserActivity[] GenerateTopUsers(ConcurrentDictionary<string, int> userActivity, int totalRecords)
        {
            return userActivity
                .OrderByDescending(kvp => kvp.Value)
                .Take(TopResultsCount)
                .Select(kvp => new IISUserActivity
                {
                    Username = kvp.Key,
                    RequestCount = kvp.Value,
                    DisplayName = TruncateUsername(kvp.Key),
                    Percentage = (double)kvp.Value / totalRecords * 100,
                    LastActivity = DateTime.Now // This could be enhanced to track actual last activity
                })
                .ToArray();
        }

        #region Individual Analytics Methods

        public async Task<IISStatusAnalysis[]> GetTopStatusCodesAsync(IEnumerable<IisLogEntry> logEntries, CancellationToken cancellationToken = default)
        {
            var result = await ProcessAnalyticsAsync(logEntries, null, cancellationToken).ConfigureAwait(false);
            return result.TopStatusCodes;
        }

        public async Task<IISLongestRequest[]> GetLongestRequestsAsync(IEnumerable<IisLogEntry> logEntries, CancellationToken cancellationToken = default)
        {
            var result = await ProcessAnalyticsAsync(logEntries, null, cancellationToken).ConfigureAwait(false);
            return result.LongestRequests;
        }

        public async Task<IISMethodDistribution[]> GetHttpMethodsDistributionAsync(IEnumerable<IisLogEntry> logEntries, CancellationToken cancellationToken = default)
        {
            var result = await ProcessAnalyticsAsync(logEntries, null, cancellationToken).ConfigureAwait(false);
            return result.HttpMethods;
        }

        public async Task<IISUserActivity[]> GetTopUsersAsync(IEnumerable<IisLogEntry> logEntries, CancellationToken cancellationToken = default)
        {
            var result = await ProcessAnalyticsAsync(logEntries, null, cancellationToken).ConfigureAwait(false);
            return result.TopUsers;
        }

        #endregion

        #region Helper Methods

        private static string GetStatusDescription(int statusCode)
        {
            return statusCode switch
            {
                200 => "OK",
                302 => "Found",
                404 => "Not Found",
                500 => "Internal Server Error",
                _ => statusCode.ToString()
            };
        }

        private static string GetStatusColorTheme(int statusCode)
        {
            return statusCode switch
            {
                >= 500 => "red",      // 5xx errors
                >= 400 => "yellow",   // 4xx client errors
                >= 300 => "orange",   // 3xx redirects
                >= 200 => "green",    // 2xx success
                _ => "gray"
            };
        }

        private static string FormatTimeTaken(int milliseconds)
        {
            return milliseconds >= 1000 
                ? $"{milliseconds / 1000.0:F1}s" 
                : $"{milliseconds}ms";
        }

        private static string TruncateUri(string uri)
        {
            const int maxLength = 40;
            return uri.Length > maxLength ? uri.Substring(0, maxLength) + "..." : uri;
        }

        private static string TruncateUsername(string username)
        {
            const int maxLength = 20;
            return username.Length > maxLength ? username.Substring(0, maxLength) + "..." : username;
        }

        #endregion
    }
} 
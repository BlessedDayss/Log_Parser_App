namespace Log_Parser_App.Services.Analytics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Log_Parser_App.Interfaces;
    using Log_Parser_App.Models;
    using Log_Parser_App.Models.Analytics;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Implementation of RabbitMQ analytics service for dashboard calculations
    /// </summary>
    public class RabbitMQAnalyticsService : IRabbitMQAnalyticsService
    {
        private readonly ILogger<RabbitMQAnalyticsService> _logger;

        /// <summary>
        /// Initializes a new instance of the RabbitMQAnalyticsService
        /// </summary>
        /// <param name="logger">Logger for analytics operations</param>
        public RabbitMQAnalyticsService(ILogger<RabbitMQAnalyticsService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<ConsumerStatusInfo[]> GetActiveConsumersAsync(
            IEnumerable<RabbitMqLogEntry> entries, 
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var activeWindow = TimeSpan.FromMinutes(30);
                    var recentEntries = entries.Where(e => e.Timestamp.HasValue && e.Timestamp.Value.DateTime > now - activeWindow).ToArray();

                    if (!recentEntries.Any())
                    {
                        _logger.LogDebug("No recent entries found for consumer analysis");
                        return Array.Empty<ConsumerStatusInfo>();
                    }

                    var consumerGroups = recentEntries
                        .GroupBy(e => e.EffectiveNode ?? "Unknown")
                        .Select(group => new
                        {
                            Consumer = group.Key,
                            TotalMessages = group.Count(),
                            ErrorMessages = group.Count(e => IsErrorLevel(e.Level)),
                            LastActivity = group.Max(e => e.Timestamp?.DateTime ?? DateTime.MinValue),
                            ErrorRate = group.Count(e => IsErrorLevel(e.Level)) / (double)group.Count()
                        })
                        .Where(c => c.Consumer != "Unknown") // Filter out unknown consumers
                        .ToArray();

                    var results = consumerGroups.Select(c => new ConsumerStatusInfo
                    {
                        ConsumerName = c.Consumer,
                        Status = ClassifyConsumerStatus(c.ErrorRate, c.LastActivity, now),
                        MessageCount = c.TotalMessages,
                        ErrorRate = c.ErrorRate,
                        LastActivity = c.LastActivity
                    }).OrderByDescending(c => c.MessageCount).ToArray();

                    _logger.LogDebug("Analyzed {ConsumerCount} consumers in time window", results.Length);
                    return results;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error analyzing consumer status");
                    return Array.Empty<ConsumerStatusInfo>();
                }
            }, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<CriticalErrorInfo[]> GetRecentCriticalErrorsAsync(
            IEnumerable<RabbitMqLogEntry> entries, 
            int count = 5, 
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var errorEntries = entries
                        .Where(e => IsErrorLevel(e.Level))
                        .OrderByDescending(e => e.Timestamp)
                        .Take(count)
                        .ToArray();

                    if (!errorEntries.Any())
                    {
                        _logger.LogDebug("No critical errors found");
                        return Array.Empty<CriticalErrorInfo>();
                    }

                    // Check if all errors are the same type
                    var errorMessages = errorEntries.Select(e => e.EffectiveMessage ?? "Unknown error").Distinct().ToArray();
                    var isAllErrorsSame = errorMessages.Length == 1;

                    if (isAllErrorsSame)
                    {
                        // If all errors are the same, show only one entry with special message
                        var firstEntry = errorEntries.First();
                        var results = new[] { new CriticalErrorInfo
                        {
                            Timestamp = firstEntry.Timestamp?.DateTime ?? DateTime.MinValue,
                            UserName = firstEntry.EffectiveUserName ?? string.Empty,
                            ErrorMessage = "ALL ERRORS THE SAME, SEE ERROR MESSAGE",
                            StackTrace = firstEntry.EffectiveStackTrace ?? string.Empty,
                            ProcessUID = firstEntry.EffectiveProcessUID ?? string.Empty,
                            Node = firstEntry.EffectiveNode ?? string.Empty,
                            IsGroupedError = true
                        }};
                        
                        _logger.LogDebug("All {ErrorCount} errors are the same type, showing single grouped entry", errorEntries.Length);
                        return results;
                    }
                    else
                    {
                        // Show all different errors normally
                        var results = errorEntries.Select(entry => new CriticalErrorInfo
                        {
                            Timestamp = entry.Timestamp?.DateTime ?? DateTime.MinValue,
                            UserName = entry.EffectiveUserName ?? string.Empty,
                            ErrorMessage = entry.EffectiveMessage ?? "Unknown error",
                            StackTrace = entry.EffectiveStackTrace ?? string.Empty,
                            ProcessUID = entry.EffectiveProcessUID ?? string.Empty,
                            Node = entry.EffectiveNode ?? string.Empty,
                            IsGroupedError = false
                                                 }).ToArray();

                        _logger.LogDebug("Retrieved {ErrorCount} recent critical errors with different types", results.Length);
                        return results;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving critical errors");
                    return Array.Empty<CriticalErrorInfo>();
                }
            }, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<AccountActivityInfo[]> GetAccountActivityAnalysisAsync(
            IEnumerable<RabbitMqLogEntry> entries, 
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var authEntries = entries
                        .Where(e => IsAuthenticationRelated(e.EffectiveMessage))
                        .Where(e => !string.IsNullOrEmpty(e.EffectiveUserName))
                        .ToArray();

                    if (!authEntries.Any())
                    {
                        _logger.LogDebug("No authentication-related entries found");
                        return Array.Empty<AccountActivityInfo>();
                    }

                    var userActivity = authEntries
                        .GroupBy(e => e.EffectiveUserName)
                        .Select(group => new
                        {
                            UserName = group.Key!,
                            TotalAttempts = group.Count(),
                            FailedAttempts = group.Count(e => IsErrorLevel(e.Level)),
                            LastActivity = group.Max(e => e.Timestamp?.DateTime ?? DateTime.MinValue),
                            FailureRate = group.Count(e => IsErrorLevel(e.Level)) / (double)group.Count()
                        })
                        .Where(u => u.FailureRate > 0.1) // Only show users with >10% failure rate
                        .OrderByDescending(u => u.FailureRate)
                        .ThenByDescending(u => u.TotalAttempts)
                        .Take(10)
                        .ToArray();

                    var results = userActivity.Select(u => new AccountActivityInfo
                    {
                        UserName = u.UserName,
                        TotalAttempts = u.TotalAttempts,
                        FailedAttempts = u.FailedAttempts,
                        FailureRate = u.FailureRate,
                        LastActivity = u.LastActivity,
                        RiskLevel = ClassifyRiskLevel(u.FailureRate, u.TotalAttempts)
                    }).ToArray();

                    _logger.LogDebug("Analyzed {UserCount} users with authentication issues", results.Length);
                    return results;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error analyzing account activity");
                    return Array.Empty<AccountActivityInfo>();
                }
            }, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<WarningTimelineInfo[]> GetSystemWarningsTimelineAsync(
            IEnumerable<RabbitMqLogEntry> entries, 
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var warningEntries = entries
                        .Where(e => IsWarningLevel(e.Level))
                        .OrderBy(e => e.Timestamp)
                        .ToArray();

                    if (!warningEntries.Any())
                    {
                        _logger.LogDebug("No warning entries found for timeline");
                        return Array.Empty<WarningTimelineInfo>();
                    }

                    var timeRange = warningEntries.Max(e => e.Timestamp?.DateTime ?? DateTime.MinValue) - warningEntries.Min(e => e.Timestamp?.DateTime ?? DateTime.MaxValue);
                    var bucketSize = DetermineBucketSize(timeRange);

                    var buckets = warningEntries
                        .Where(e => e.Timestamp.HasValue)
                        .GroupBy(e => new DateTime(
                            (e.Timestamp!.Value.DateTime.Ticks / bucketSize.Ticks) * bucketSize.Ticks))
                        .Select(bucket => new
                        {
                            TimeStamp = bucket.Key,
                            WarningCount = bucket.Count(),
                            TopWarnings = bucket
                                .GroupBy(e => e.EffectiveMessage ?? "Unknown warning")
                                .OrderByDescending(g => g.Count())
                                .Take(3)
                                .Select(g => new WarningItem
                                {
                                    Message = g.Key,
                                    Count = g.Count(),
                                    Percentage = g.Count() / (double)bucket.Count()
                                }).ToArray()
                        })
                        .OrderBy(b => b.TimeStamp)
                        .ToArray();

                    // Calculate relative heights for visualization
                    var maxCount = buckets.Any() ? buckets.Max(b => b.WarningCount) : 1;
                    var results = buckets.Select(b => new WarningTimelineInfo
                    {
                        TimeStamp = b.TimeStamp,
                        WarningCount = b.WarningCount,
                        TopWarnings = b.TopWarnings,
                        RelativeHeight = maxCount > 0 ? b.WarningCount / (double)maxCount : 0
                    }).ToArray();

                    _logger.LogDebug("Created timeline with {BucketCount} time buckets", results.Length);
                    return results;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating warnings timeline");
                    return Array.Empty<WarningTimelineInfo>();
                }
            }, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<AnomalyInsightInfo> GetAnomaliesInsightAsync(
            IEnumerable<RabbitMqLogEntry> entries, 
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var insights = new List<string>();
                    var anomalies = new List<AnomalyDetail>();

                    // 1. Error Spike Detection
                    var errorSpikes = DetectErrorSpikes(entries);
                    if (errorSpikes.Any())
                    {
                        insights.Add($"Detected {errorSpikes.Length} error spikes in recent data");
                        anomalies.AddRange(errorSpikes);
                    }

                    // 2. ProcessUID Clustering Analysis
                    var processUIDClusters = AnalyzeProcessUIDClustering(entries);
                    if (processUIDClusters.Any())
                    {
                        insights.Add($"Found {processUIDClusters.Length} ProcessUID clusters with unusual error patterns");
                        anomalies.AddRange(processUIDClusters);
                    }

                    // 3. User Behavior Anomalies
                    var userAnomalies = DetectUserBehaviorAnomalies(entries);
                    if (userAnomalies.Any())
                    {
                        insights.Add($"Identified {userAnomalies.Length} users with anomalous activity patterns");
                        anomalies.AddRange(userAnomalies);
                    }

                    var overallSeverity = anomalies.Any() 
                        ? anomalies.Max(a => a.Severity) 
                        : AnomalySeverity.Low;

                    var result = new AnomalyInsightInfo
                    {
                        Summary = insights.Any() ? string.Join(". ", insights) : "No significant anomalies detected",
                        AnomalyCount = anomalies.Count,
                        TopAnomalies = anomalies.OrderByDescending(a => a.Severity).Take(5).ToArray(),
                        RecommendedActions = GenerateRecommendations(anomalies),
                        OverallSeverity = overallSeverity,
                        AnalysisTimestamp = DateTime.UtcNow
                    };

                    _logger.LogDebug("Completed anomaly analysis: {AnomalyCount} anomalies, severity: {Severity}", 
                        result.AnomalyCount, result.OverallSeverity);
                    
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error performing anomaly analysis");
                    return new AnomalyInsightInfo
                    {
                        Summary = "Error occurred during anomaly analysis",
                        OverallSeverity = AnomalySeverity.Low,
                        AnalysisTimestamp = DateTime.UtcNow
                    };
                }
            }, cancellationToken);
        }

        #region Private Helper Methods

        private static bool IsErrorLevel(string? level)
        {
            if (string.IsNullOrEmpty(level)) return false;
            return level.Equals("ERROR", StringComparison.OrdinalIgnoreCase) ||
                   level.Equals("FATAL", StringComparison.OrdinalIgnoreCase) ||
                   level.Equals("CRITICAL", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWarningLevel(string? level)
        {
            if (string.IsNullOrEmpty(level)) return false;
            return level.Equals("WARNING", StringComparison.OrdinalIgnoreCase) ||
                   level.Equals("WARN", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAuthenticationRelated(string? message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            
            var authKeywords = new[] { "auth", "login", "password", "token", "credential", "unauthorized", "forbidden" };
            return authKeywords.Any(keyword => 
                message.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private static ConsumerStatus ClassifyConsumerStatus(double errorRate, DateTime lastActivity, DateTime now)
        {
            var timeSinceActivity = now - lastActivity;
            
            if (timeSinceActivity > TimeSpan.FromHours(1))
                return ConsumerStatus.Inactive;
            
            if (errorRate > 0.2) // 20% error threshold
                return ConsumerStatus.Error;
                
            return ConsumerStatus.Active;
        }

        private static RiskLevel ClassifyRiskLevel(double failureRate, int totalAttempts)
        {
            if (failureRate > 0.8 && totalAttempts > 10) return RiskLevel.High;
            if (failureRate > 0.5 && totalAttempts > 5) return RiskLevel.Medium;
            return RiskLevel.Low;
        }

        private static TimeSpan DetermineBucketSize(TimeSpan totalRange)
        {
            if (totalRange.TotalDays > 7) return TimeSpan.FromDays(1);
            if (totalRange.TotalHours > 24) return TimeSpan.FromHours(1);
            if (totalRange.TotalMinutes > 60) return TimeSpan.FromMinutes(10);
            return TimeSpan.FromMinutes(1);
        }

        private AnomalyDetail[] DetectErrorSpikes(IEnumerable<RabbitMqLogEntry> entries)
        {
            var hourlyErrorCounts = entries
                .Where(e => IsErrorLevel(e.Level) && e.Timestamp.HasValue)
                .GroupBy(e => new DateTime(e.Timestamp!.Value.DateTime.Year, e.Timestamp.Value.DateTime.Month, e.Timestamp.Value.DateTime.Day, e.Timestamp.Value.DateTime.Hour, 0, 0))
                .Select(g => new { Hour = g.Key, Count = g.Count() })
                .ToArray();

            if (hourlyErrorCounts.Length < 3) return Array.Empty<AnomalyDetail>();

            var avgErrors = hourlyErrorCounts.Average(h => h.Count);
            var stdDev = CalculateStandardDeviation(hourlyErrorCounts.Select(h => h.Count));
            var threshold = avgErrors + (2 * stdDev);

            return hourlyErrorCounts
                .Where(h => h.Count > threshold)
                .Select(h => new AnomalyDetail
                {
                    Type = AnomalyType.ErrorSpike,
                    Timestamp = h.Hour,
                    Value = h.Count,
                    Threshold = threshold,
                    Severity = CalculateSeverity(h.Count, threshold),
                    Description = $"Error spike: {h.Count} errors at {h.Hour:HH:mm} (threshold: {threshold:F1})"
                }).ToArray();
        }

        private AnomalyDetail[] AnalyzeProcessUIDClustering(IEnumerable<RabbitMqLogEntry> entries)
        {
            var processUIDErrors = entries
                .Where(e => IsErrorLevel(e.Level) && !string.IsNullOrEmpty(e.EffectiveProcessUID))
                .GroupBy(e => e.EffectiveProcessUID)
                .Where(g => g.Count() >= 3) // At least 3 errors from same ProcessUID
                .Select(g => new AnomalyDetail
                {
                    Type = AnomalyType.ProcessUIDClustering,
                    Timestamp = g.Max(e => e.Timestamp?.DateTime ?? DateTime.MinValue),
                    Value = g.Count(),
                    Threshold = 3,
                    Severity = g.Count() > 10 ? AnomalySeverity.High : AnomalySeverity.Medium,
                    Description = $"ProcessUID {g.Key} generated {g.Count()} errors",
                    Context = g.Key!
                }).ToArray();

            return processUIDErrors;
        }

        private AnomalyDetail[] DetectUserBehaviorAnomalies(IEnumerable<RabbitMqLogEntry> entries)
        {
            var userErrorCounts = entries
                .Where(e => IsErrorLevel(e.Level) && !string.IsNullOrEmpty(e.EffectiveUserName))
                .GroupBy(e => e.EffectiveUserName)
                .Where(g => g.Count() >= 5) // At least 5 errors per user
                .Select(g => new AnomalyDetail
                {
                    Type = AnomalyType.UserBehaviorAnomaly,
                    Timestamp = g.Max(e => e.Timestamp?.DateTime ?? DateTime.MinValue),
                    Value = g.Count(),
                    Threshold = 5,
                    Severity = g.Count() > 20 ? AnomalySeverity.High : AnomalySeverity.Medium,
                    Description = $"User {g.Key} has {g.Count()} errors",
                    Context = g.Key!
                }).ToArray();

            return userErrorCounts;
        }

        private static double CalculateStandardDeviation(IEnumerable<int> values)
        {
            var valuesArray = values.ToArray();
            if (valuesArray.Length < 2) return 0;

            var average = valuesArray.Average();
            var sumOfSquaresOfDifferences = valuesArray.Select(val => (val - average) * (val - average)).Sum();
            return Math.Sqrt(sumOfSquaresOfDifferences / valuesArray.Length);
        }

        private static AnomalySeverity CalculateSeverity(double value, double threshold)
        {
            var ratio = threshold > 0 ? value / threshold : 1;
            if (ratio > 3) return AnomalySeverity.Critical;
            if (ratio > 2) return AnomalySeverity.High;
            if (ratio > 1.5) return AnomalySeverity.Medium;
            return AnomalySeverity.Low;
        }

        private static string GenerateRecommendations(List<AnomalyDetail> anomalies)
        {
            if (!anomalies.Any()) return string.Empty;

            var recommendations = new List<string>();

            if (anomalies.Any(a => a.Type == AnomalyType.ErrorSpike))
                recommendations.Add("Investigate recent system changes during error spike periods");

            if (anomalies.Any(a => a.Type == AnomalyType.ProcessUIDClustering))
                recommendations.Add("Review processes with clustered errors for potential service issues");

            if (anomalies.Any(a => a.Type == AnomalyType.UserBehaviorAnomaly))
                recommendations.Add("Check user accounts with high error rates for authentication issues");

            return recommendations.Any() ? string.Join(". ", recommendations) : string.Empty;
        }

        #endregion
    }
} 
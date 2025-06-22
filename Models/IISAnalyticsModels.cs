using System;

namespace Log_Parser_App.Models
{
    /// <summary>
    /// Represents analytics result for IIS logs with TOP 3 aggregated metrics
    /// </summary>
    public class IISAnalyticsResult
    {
        public IISStatusAnalysis[] TopStatusCodes { get; set; } = Array.Empty<IISStatusAnalysis>();
        public IISLongestRequest[] LongestRequests { get; set; } = Array.Empty<IISLongestRequest>();
        public IISMethodDistribution[] HttpMethods { get; set; } = Array.Empty<IISMethodDistribution>();
        public IISUserActivity[] TopUsers { get; set; } = Array.Empty<IISUserActivity>();
        public int TotalRecordsProcessed { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    /// <summary>
    /// Represents analytics progress information for real-time UI updates
    /// </summary>
    public class AnalyticsProgress
    {
        public int PercentageCompleted { get; set; }
        public string OperationStatus { get; set; } = string.Empty;
        public int ProcessedRecords { get; set; }
        public int TotalRecords { get; set; }
        public string CurrentOperation { get; set; } = string.Empty;
        public TimeSpan ElapsedTime { get; set; }
        public double PercentComplete => TotalRecords > 0 ? (double)ProcessedRecords / TotalRecords * 100 : 0;
    }

    /// <summary>
    /// Represents HTTP status code analysis with usage statistics
    /// </summary>
    public class IISStatusAnalysis
    {
        public int StatusCode { get; set; }
        public int Count { get; set; }
        public double Percentage { get; set; }
        public string StatusDescription { get; set; } = string.Empty;
        public string ColorTheme { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents the longest HTTP requests by execution time
    /// </summary>
    public class IISLongestRequest
    {
        public string UriStem { get; set; } = string.Empty;
        public int TimeTaken { get; set; }
        public string FormattedTime { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Represents HTTP method distribution statistics
    /// </summary>
    public class IISMethodDistribution
    {
        public string Method { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
        public string FormattedDisplay { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents user activity analysis for IIS logs
    /// </summary>
    public class IISUserActivity
    {
        public string Username { get; set; } = string.Empty;
        public int RequestCount { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public double Percentage { get; set; }
        public DateTime LastActivity { get; set; }
    }
} 
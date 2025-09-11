using System;

namespace Log_Parser_App.Models
{
    /// <summary>
    /// Statistics for object pooling performance
    /// </summary>
    public class PoolStatistics
    {
        public long TotalGets { get; set; }
        public long TotalReturns { get; set; }
        public long PoolHits { get; set; }
        public long PoolMisses { get; set; }
        public int CurrentPoolSize { get; set; }
        public int MaxPoolSize { get; set; }
        public long TotalInstancesCreated { get; set; }
        public long MemorySavedBytes { get; set; }
        
        public double HitRatio => TotalGets > 0 ? (double)PoolHits / TotalGets : 0.0;
    }

    /// <summary>
    /// Statistics for caching performance
    /// </summary>
    public class CacheStatistics
    {
        public long HitCount { get; set; }
        public long MissCount { get; set; }
        public int CachedItemsCount { get; set; }
        public long TotalMemoryUsage { get; set; }
        public DateTime LastAccess { get; set; }
        public TimeSpan AverageAccessTime { get; set; }
        
        public double HitRatio => (HitCount + MissCount) > 0 ? (double)HitCount / (HitCount + MissCount) : 0.0;
    }

    /// <summary>
    /// Progress information for log parsing operations
    /// </summary>
    public class LogParsingProgress
    {
        public long ProcessedLines { get; set; }
        public long TotalLines { get; set; }
        public long BytesProcessed { get; set; }
        public long TotalBytes { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public string CurrentOperation { get; set; } = string.Empty;
        
        public double PercentageComplete => TotalLines > 0 ? (double)ProcessedLines / TotalLines * 100 : 0.0;
        public double BytesPercentageComplete => TotalBytes > 0 ? (double)BytesProcessed / TotalBytes * 100 : 0.0;
        public double LinesPerSecond => ElapsedTime.TotalSeconds > 0 ? ProcessedLines / ElapsedTime.TotalSeconds : 0.0;
    }

    /// <summary>
    /// Statistics for batch processing performance
    /// </summary>
    public class BatchStatistics
    {
        public long TotalBatches { get; set; }
        public long TotalItems { get; set; }
        public int CurrentBatchSize { get; set; }
        public int OptimalBatchSize { get; set; }
        public TimeSpan AverageProcessingTime { get; set; }
        public TimeSpan TotalProcessingTime { get; set; }
        public long MemoryUsage { get; set; }
        
        public double ItemsPerSecond => TotalProcessingTime.TotalSeconds > 0 ? TotalItems / TotalProcessingTime.TotalSeconds : 0.0;
        public double AverageItemsPerBatch => TotalBatches > 0 ? (double)TotalItems / TotalBatches : 0.0;
    }

    /// <summary>
    /// Information about background processing operations
    /// </summary>
    public class BackgroundOperationInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public BackgroundOperationStatus Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? Duration => EndTime?.Subtract(StartTime);
        public string? ErrorMessage { get; set; }
        public double ProgressPercentage { get; set; }
        public string CurrentStep { get; set; } = string.Empty;
    }

    /// <summary>
    /// Status of background operations
    /// </summary>
    public enum BackgroundOperationStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled
    }

    /// <summary>
    /// Event arguments for background operation progress
    /// </summary>
    public class BackgroundOperationProgressEventArgs : EventArgs
    {
        public string OperationId { get; set; } = string.Empty;
        public double ProgressPercentage { get; set; }
        public string CurrentStep { get; set; } = string.Empty;
        public TimeSpan ElapsedTime { get; set; }
    }

    /// <summary>
    /// Event arguments for background operation completion
    /// </summary>
    public class BackgroundOperationCompletedEventArgs : EventArgs
    {
        public string OperationId { get; set; } = string.Empty;
        public BackgroundOperationStatus Status { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
        public object? Result { get; set; }
    }
} 
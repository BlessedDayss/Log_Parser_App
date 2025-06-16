using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Interfaces;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services
{
    /// <summary>
    /// Implementation of batch processor for async enumerable data
    /// Optimizes memory usage by processing data in configurable chunks
    /// </summary>
    /// <typeparam name="T">Type of items to process</typeparam>
    public class BatchProcessor<T> : IBatchProcessor<T>
    {
        private readonly ILogger<BatchProcessor<T>> _logger;
        private const int DefaultBatchSize = 1000;
        private const int MinBatchSize = 10;
        private const int MaxBatchSize = 10000;

        public BatchProcessor(ILogger<BatchProcessor<T>> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Process async enumerable source in batches
        /// </summary>
        public async IAsyncEnumerable<IEnumerable<T>> ProcessInBatchesAsync(
            IAsyncEnumerable<T> source, 
            int batchSize, 
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (batchSize <= 0)
                batchSize = DefaultBatchSize;

            batchSize = Math.Clamp(batchSize, MinBatchSize, MaxBatchSize);

            _logger.LogDebug("Starting batch processing with batch size: {BatchSize}", batchSize);

            var batch = new List<T>(batchSize);
            var processedCount = 0;

            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                batch.Add(item);
                processedCount++;

                if (batch.Count >= batchSize)
                {
                    _logger.LogTrace("Yielding batch of {Count} items (total processed: {Total})", 
                        batch.Count, processedCount);
                    
                    yield return batch.ToList(); // Create copy to avoid modification
                    batch.Clear();
                }
            }

            // Yield remaining items if any
            if (batch.Count > 0)
            {
                _logger.LogTrace("Yielding final batch of {Count} items (total processed: {Total})", 
                    batch.Count, processedCount);
                
                yield return batch.ToList();
            }

            _logger.LogDebug("Batch processing completed. Total items processed: {Total}", processedCount);
        }

        /// <summary>
        /// Get optimal batch size based on available memory and item type
        /// </summary>
        public int GetOptimalBatchSize(int estimatedItemSize, int availableMemoryMB = 100)
        {
            if (estimatedItemSize <= 0)
                estimatedItemSize = 1024; // Default 1KB per item

            if (availableMemoryMB <= 0)
                availableMemoryMB = 100; // Default 100MB

            // Calculate batch size to use ~10% of available memory
            var targetMemoryBytes = availableMemoryMB * 1024 * 1024 * 0.1;
            var calculatedBatchSize = (int)(targetMemoryBytes / estimatedItemSize);

            var optimalSize = Math.Clamp(calculatedBatchSize, MinBatchSize, MaxBatchSize);

            _logger.LogDebug("Calculated optimal batch size: {BatchSize} (item size: {ItemSize}B, memory: {Memory}MB)", 
                optimalSize, estimatedItemSize, availableMemoryMB);

            return optimalSize;
        }

        /// <summary>
        /// Process with adaptive batch sizing based on performance metrics
        /// </summary>
        public async IAsyncEnumerable<IEnumerable<T>> ProcessWithAdaptiveBatchingAsync(
            IAsyncEnumerable<T> source,
            int initialBatchSize = DefaultBatchSize,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var currentBatchSize = Math.Clamp(initialBatchSize, MinBatchSize, MaxBatchSize);
            var batch = new List<T>(currentBatchSize);
            var processedCount = 0;
            var batchCount = 0;
            var performanceMetrics = new List<BatchPerformanceMetric>();

            _logger.LogDebug("Starting adaptive batch processing with initial batch size: {BatchSize}", currentBatchSize);

            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                var batchStartTime = DateTime.UtcNow;
                batch.Add(item);
                processedCount++;

                if (batch.Count >= currentBatchSize)
                {
                    var batchEndTime = DateTime.UtcNow;
                    var processingTime = batchEndTime - batchStartTime;
                    
                    performanceMetrics.Add(new BatchPerformanceMetric
                    {
                        BatchSize = batch.Count,
                        ProcessingTime = processingTime,
                        ItemsPerSecond = batch.Count / Math.Max(processingTime.TotalSeconds, 0.001)
                    });

                    _logger.LogTrace("Yielding adaptive batch of {Count} items (total processed: {Total}, time: {Time}ms)", 
                        batch.Count, processedCount, processingTime.TotalMilliseconds);
                    
                    yield return batch.ToList();
                    batch.Clear();
                    batchCount++;

                    // Adjust batch size every 5 batches based on performance
                    if (batchCount % 5 == 0 && performanceMetrics.Count >= 3)
                    {
                        currentBatchSize = CalculateOptimalBatchSize(performanceMetrics);
                        batch.Capacity = currentBatchSize;
                        
                        _logger.LogDebug("Adjusted batch size to: {BatchSize} based on performance metrics", currentBatchSize);
                    }
                }
            }

            // Yield remaining items if any
            if (batch.Count > 0)
            {
                _logger.LogTrace("Yielding final adaptive batch of {Count} items (total processed: {Total})", 
                    batch.Count, processedCount);
                
                yield return batch.ToList();
            }

            _logger.LogDebug("Adaptive batch processing completed. Total items: {Total}, batches: {Batches}, final batch size: {BatchSize}", 
                processedCount, batchCount + 1, currentBatchSize);
        }

        private int CalculateOptimalBatchSize(List<BatchPerformanceMetric> metrics)
        {
            if (metrics.Count < 3)
                return DefaultBatchSize;

            // Take last 3 metrics for trend analysis
            var recentMetrics = metrics.TakeLast(3).ToList();
            var avgItemsPerSecond = recentMetrics.Average(m => m.ItemsPerSecond);
            var avgBatchSize = recentMetrics.Average(m => m.BatchSize);

            // If performance is good (>1000 items/sec), try increasing batch size
            if (avgItemsPerSecond > 1000 && avgBatchSize < MaxBatchSize)
            {
                return Math.Min((int)(avgBatchSize * 1.2), MaxBatchSize);
            }
            
            // If performance is poor (<100 items/sec), try decreasing batch size
            if (avgItemsPerSecond < 100 && avgBatchSize > MinBatchSize)
            {
                return Math.Max((int)(avgBatchSize * 0.8), MinBatchSize);
            }

            // Otherwise keep current size
            return (int)avgBatchSize;
        }

        private class BatchPerformanceMetric
        {
            public int BatchSize { get; set; }
            public TimeSpan ProcessingTime { get; set; }
            public double ItemsPerSecond { get; set; }
        }
    }
} 
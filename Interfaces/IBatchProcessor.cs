using System.Collections.Generic;
using System.Threading;

namespace Log_Parser_App.Interfaces
{
    /// <summary>
    /// Interface for batch processing of async enumerable data
    /// Optimizes memory usage by processing data in configurable chunks
    /// </summary>
    /// <typeparam name="T">Type of items to process</typeparam>
    public interface IBatchProcessor<T>
    {
        /// <summary>
        /// Process async enumerable source in batches
        /// </summary>
        /// <param name="source">Source async enumerable</param>
        /// <param name="batchSize">Size of each batch</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Async enumerable of batches</returns>
        IAsyncEnumerable<IEnumerable<T>> ProcessInBatchesAsync(
            IAsyncEnumerable<T> source, 
            int batchSize, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get optimal batch size based on available memory and item type
        /// </summary>
        /// <param name="estimatedItemSize">Estimated size of each item in bytes</param>
        /// <param name="availableMemoryMB">Available memory in MB</param>
        /// <returns>Recommended batch size</returns>
        int GetOptimalBatchSize(int estimatedItemSize, int availableMemoryMB = 100);

        /// <summary>
        /// Process with adaptive batch sizing based on performance metrics
        /// </summary>
        /// <param name="source">Source async enumerable</param>
        /// <param name="initialBatchSize">Initial batch size</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Async enumerable of batches with adaptive sizing</returns>
        IAsyncEnumerable<IEnumerable<T>> ProcessWithAdaptiveBatchingAsync(
            IAsyncEnumerable<T> source,
            int initialBatchSize = 1000,
            CancellationToken cancellationToken = default);
    }
}

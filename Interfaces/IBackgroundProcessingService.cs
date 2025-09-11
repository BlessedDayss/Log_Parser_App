using System;
using System.Threading;
using System.Threading.Tasks;

namespace Log_Parser_App.Interfaces
{
    /// <summary>
    /// Service for executing heavy operations in background threads
    /// Keeps UI responsive during long-running tasks
    /// </summary>
    public interface IBackgroundProcessingService
    {
        /// <summary>
        /// Execute operation in background with progress reporting
        /// </summary>
        /// <typeparam name="T">Return type of operation</typeparam>
        /// <param name="operation">Operation to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task with operation result</returns>
        Task<T> ProcessAsync<T>(
            Func<CancellationToken, IProgress<ProcessingProgress>, Task<T>> operation,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute operation in background without return value
        /// </summary>
        /// <param name="operation">Operation to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing operation completion</returns>
        Task ProcessAsync(
            Func<CancellationToken, IProgress<ProcessingProgress>, Task> operation,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancel all currently running operations
        /// </summary>
        /// <returns>Task representing cancellation completion</returns>
        Task CancelAllOperationsAsync();

        /// <summary>
        /// Get count of currently running operations
        /// </summary>
        int ActiveOperationsCount { get; }

        /// <summary>
        /// Event fired when operation starts
        /// </summary>
        event EventHandler<ProcessingProgress>? OperationStarted;

        /// <summary>
        /// Event fired when operation completes
        /// </summary>
        event EventHandler<ProcessingProgress>? OperationCompleted;

        /// <summary>
        /// Event fired when operation progress updates
        /// </summary>
        event EventHandler<ProcessingProgress>? ProgressUpdated;
    }

    /// <summary>
    /// Progress information for background processing operations
    /// </summary>
    public class ProcessingProgress
    {
        public int ProcessedItems { get; set; }
        public int TotalItems { get; set; }
        public double PercentageComplete => TotalItems > 0 ? (double)ProcessedItems / TotalItems * 100 : 0;
        public string CurrentOperation { get; set; } = string.Empty;
        public TimeSpan ElapsedTime { get; set; }
        public string OperationId { get; set; } = Guid.NewGuid().ToString();
        public bool IsCompleted { get; set; }
        public bool IsCancelled { get; set; }
        public Exception? Error { get; set; }
    }
}

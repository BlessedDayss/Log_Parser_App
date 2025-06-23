using System.Collections.Generic;
using System.Threading;

namespace Log_Parser_App.Interfaces
{
    /// <summary>
    /// Base abstraction for all filter components in the Composite pattern.
    /// Supports both leaf filters (individual strategies) and composite filters (logical operations).
    /// </summary>
    /// <typeparam name="T">Type of log entry to filter</typeparam>
    public interface IFilterExpression<T>
    {
        /// <summary>
        /// Evaluates the filter expression against a source of log entries.
        /// Uses lazy evaluation with IAsyncEnumerable for memory efficiency.
        /// </summary>
        /// <param name="source">Source collection of log entries</param>
        /// <param name="cancellationToken">Cancellation token for async operations</param>
        /// <returns>Filtered log entries matching the expression criteria</returns>
        IAsyncEnumerable<T> EvaluateAsync(IAsyncEnumerable<T> source, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets a human-readable description of the filter expression.
        /// Used for debugging and UI display purposes.
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// Estimates the selectivity of this filter (0.0 = very selective, 1.0 = not selective).
        /// Used for query optimization and execution order planning.
        /// </summary>
        double EstimatedSelectivity { get; }
    }
} 
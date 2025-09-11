using System.Collections.Generic;
using System.Threading;

namespace Log_Parser_App.Interfaces
{
    /// <summary>
    /// Generic strategy interface for type-specific filtering operations.
    /// Implements the Strategy pattern for extensible filter implementations.
    /// </summary>
    /// <typeparam name="T">Type of log entry to filter</typeparam>
    public interface IFilterStrategy<T>
    {
        /// <summary>
        /// Gets the name of the field this strategy filters on.
        /// Used for strategy selection and validation.
        /// </summary>
        string FieldName { get; }
        
        /// <summary>
        /// Gets the operator this strategy implements (e.g., "equals", "contains", "between").
        /// Used for strategy selection and validation.
        /// </summary>
        string Operator { get; }
        
        /// <summary>
        /// Applies the filter strategy to a source of log entries.
        /// Implements lazy evaluation for memory efficiency.
        /// </summary>
        /// <param name="source">Source collection of log entries</param>
        /// <param name="value">Filter value to compare against</param>
        /// <param name="cancellationToken">Cancellation token for async operations</param>
        /// <returns>Filtered log entries matching the strategy criteria</returns>
        IAsyncEnumerable<T> ApplyAsync(IAsyncEnumerable<T> source, object value, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Validates that the provided value is compatible with this strategy.
        /// Performs type checking and format validation.
        /// </summary>
        /// <param name="value">Value to validate</param>
        /// <returns>True if value is valid for this strategy</returns>
        bool IsValidValue(object value);
        
        /// <summary>
        /// Estimates the selectivity of this filter strategy (0.0 = very selective, 1.0 = not selective).
        /// Used for query optimization and execution order planning.
        /// </summary>
        /// <param name="value">Filter value to estimate selectivity for</param>
        /// <returns>Estimated selectivity ratio</returns>
        double EstimateSelectivity(object value);
    }
} 
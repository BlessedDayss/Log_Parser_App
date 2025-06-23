using System.Collections.Generic;
using System.Threading;
using Log_Parser_App.Interfaces;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.Filtering
{
    /// <summary>
    /// Leaf node in the Composite pattern for filter expressions.
    /// Wraps individual filter strategies and provides IFilterExpression interface.
    /// </summary>
    /// <typeparam name="T">Type of log entry to filter</typeparam>
    public class FilterLeaf<T> : IFilterExpression<T>
    {
        private readonly IFilterStrategy<T> _strategy;
        private readonly object _value;
        private readonly ILogger<FilterLeaf<T>>? _logger;

        /// <summary>
        /// Initializes a new instance of FilterLeaf with a strategy and value.
        /// </summary>
        /// <param name="strategy">Filter strategy to wrap</param>
        /// <param name="value">Value to pass to the strategy</param>
        /// <param name="logger">Optional logger for debugging</param>
        public FilterLeaf(IFilterStrategy<T> strategy, object value, ILogger<FilterLeaf<T>>? logger = null)
        {
            _strategy = strategy ?? throw new System.ArgumentNullException(nameof(strategy));
            _value = value ?? throw new System.ArgumentNullException(nameof(value));
            _logger = logger;
            
            // Validate value compatibility at construction time
            if (!_strategy.IsValidValue(_value))
            {
                throw new System.ArgumentException($"Value '{_value}' is not valid for strategy '{_strategy.FieldName} {_strategy.Operator}'", nameof(value));
            }
        }

        /// <inheritdoc />
        public IAsyncEnumerable<T> EvaluateAsync(IAsyncEnumerable<T> source, CancellationToken cancellationToken = default)
        {
            _logger?.LogDebug("Evaluating filter leaf: {Description}", Description);
            return _strategy.ApplyAsync(source, _value, cancellationToken);
        }

        /// <inheritdoc />
        public string Description => $"{_strategy.FieldName} {_strategy.Operator} {_value}";

        /// <inheritdoc />
        public double EstimatedSelectivity => _strategy.EstimateSelectivity(_value);

        /// <summary>
        /// Gets the underlying filter strategy.
        /// </summary>
        public IFilterStrategy<T> Strategy => _strategy;

        /// <summary>
        /// Gets the filter value.
        /// </summary>
        public object Value => _value;

        /// <summary>
        /// Creates a string representation of the filter leaf for debugging.
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString() => Description;
    }
} 
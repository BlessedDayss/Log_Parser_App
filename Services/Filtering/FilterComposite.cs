using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.Filtering
{
    /// <summary>
    /// Composite node in the Composite pattern for filter expressions.
    /// Implements logical operations (AND, OR, NOT) on child filter expressions.
    /// Uses lazy enumerable chain with smart ordering for optimal performance.
    /// </summary>
    /// <typeparam name="T">Type of log entry to filter</typeparam>
    public class FilterComposite<T> : IFilterExpression<T>
    {
        private readonly LogicalOperator _operator;
        private readonly List<IFilterExpression<T>> _children;
        private readonly ILogger<FilterComposite<T>>? _logger;

        /// <summary>
        /// Initializes a new instance of FilterComposite with a logical operator.
        /// </summary>
        /// <param name="logicalOperator">Logical operator for combining child expressions</param>
        /// <param name="logger">Optional logger for debugging</param>
        public FilterComposite(LogicalOperator logicalOperator, ILogger<FilterComposite<T>>? logger = null)
        {
            _operator = logicalOperator;
            _children = new List<IFilterExpression<T>>();
            _logger = logger;
        }

        /// <summary>
        /// Adds a child filter expression to this composite.
        /// </summary>
        /// <param name="child">Child filter expression to add</param>
        public void AddChild(IFilterExpression<T> child)
        {
            if (child == null) throw new System.ArgumentNullException(nameof(child));
            _children.Add(child);
        }

        /// <summary>
        /// Removes a child filter expression from this composite.
        /// </summary>
        /// <param name="child">Child filter expression to remove</param>
        /// <returns>True if child was found and removed</returns>
        public bool RemoveChild(IFilterExpression<T> child)
        {
            return _children.Remove(child);
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<T> EvaluateAsync(IAsyncEnumerable<T> source, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger?.LogDebug("Evaluating filter composite: {Description} with {ChildCount} children", Description, _children.Count);

            if (!_children.Any())
            {
                _logger?.LogWarning("Filter composite has no children, returning all items");
                await foreach (var item in source.WithCancellation(cancellationToken))
                {
                    yield return item;
                }
                yield break;
            }

            switch (_operator)
            {
                case LogicalOperator.And:
                    await foreach (var item in EvaluateAndAsync(source, cancellationToken))
                    {
                        yield return item;
                    }
                    break;

                case LogicalOperator.Or:
                    await foreach (var item in EvaluateOrAsync(source, cancellationToken))
                    {
                        yield return item;
                    }
                    break;

                case LogicalOperator.Not:
                    await foreach (var item in EvaluateNotAsync(source, cancellationToken))
                    {
                        yield return item;
                    }
                    break;

                default:
                    throw new System.InvalidOperationException($"Unsupported logical operator: {_operator}");
            }
        }

        /// <summary>
        /// Evaluates AND operation using sequential chaining for short-circuit evaluation.
        /// </summary>
        private async IAsyncEnumerable<T> EvaluateAndAsync(IAsyncEnumerable<T> source, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Optimize execution order by selectivity (most selective first)
            var orderedChildren = _children.OrderBy(c => c.EstimatedSelectivity).ToList();
            
            _logger?.LogDebug("AND operation with {Count} children, ordered by selectivity", orderedChildren.Count);

            var current = source;
            foreach (var child in orderedChildren)
            {
                current = child.EvaluateAsync(current, cancellationToken);
            }

            await foreach (var item in current.WithCancellation(cancellationToken))
            {
                yield return item;
            }
        }

        /// <summary>
        /// Evaluates OR operation using union with duplicate handling.
        /// </summary>
        private async IAsyncEnumerable<T> EvaluateOrAsync(IAsyncEnumerable<T> source, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger?.LogDebug("OR operation with {Count} children", _children.Count);

            var seen = new HashSet<T>();
            
            foreach (var child in _children)
            {
                await foreach (var item in child.EvaluateAsync(source, cancellationToken))
                {
                    if (seen.Add(item))
                    {
                        yield return item;
                    }
                }
            }
        }

        /// <summary>
        /// Evaluates NOT operation using set difference.
        /// </summary>
        private async IAsyncEnumerable<T> EvaluateNotAsync(IAsyncEnumerable<T> source, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_children.Count != 1)
            {
                throw new System.InvalidOperationException("NOT operator requires exactly one child expression");
            }

            _logger?.LogDebug("NOT operation with single child");

            // Materialize the filtered set to compute complement
            var filteredItems = new HashSet<T>();
            await foreach (var item in _children[0].EvaluateAsync(source, cancellationToken))
            {
                filteredItems.Add(item);
            }

            // Return items from source that are not in filtered set
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                if (!filteredItems.Contains(item))
                {
                    yield return item;
                }
            }
        }

        /// <inheritdoc />
        public string Description
        {
            get
            {
                if (!_children.Any())
                    return $"{_operator}()";

                if (_operator == LogicalOperator.Not && _children.Count == 1)
                    return $"NOT ({_children[0].Description})";

                var childDescriptions = _children.Select(c => c.Description);
                return $"({string.Join($" {_operator.ToString().ToUpperInvariant()} ", childDescriptions)})";
            }
        }

        /// <inheritdoc />
        public double EstimatedSelectivity
        {
            get
            {
                if (!_children.Any()) return 1.0;

                return _operator switch
                {
                    LogicalOperator.And => _children.Select(c => c.EstimatedSelectivity).Aggregate((a, b) => a * b),
                    LogicalOperator.Or => 1.0 - _children.Select(c => 1.0 - c.EstimatedSelectivity).Aggregate((a, b) => a * b),
                    LogicalOperator.Not => _children.Count == 1 ? 1.0 - _children[0].EstimatedSelectivity : 0.5,
                    _ => 0.5
                };
            }
        }

        /// <summary>
        /// Gets the logical operator used by this composite.
        /// </summary>
        public LogicalOperator Operator => _operator;

        /// <summary>
        /// Gets the child filter expressions.
        /// </summary>
        public IReadOnlyList<IFilterExpression<T>> Children => _children.AsReadOnly();

        /// <summary>
        /// Creates a string representation of the filter composite for debugging.
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString() => Description;
    }
} 
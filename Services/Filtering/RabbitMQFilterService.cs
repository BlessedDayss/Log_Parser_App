using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Log_Parser_App.Services.Filtering.Strategies;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.Filtering
{
    /// <summary>
    /// Dedicated service for RabbitMQ message filtering operations.
    /// Follows Single Responsibility Principle by isolating RabbitMQ-specific filtering logic.
    /// Implements the Strategy pattern with composite filter expressions.
    /// </summary>
    public class RabbitMQFilterService : IRabbitMQFilterService
    {
        private readonly ILogger<RabbitMQFilterService> _logger;
        private readonly Dictionary<string, Func<string, IFilterStrategy<RabbitMqLogEntry>>> _strategyFactories;
        private readonly HashSet<string> _availableFields;
        private readonly Dictionary<string, HashSet<string>> _fieldOperators;

        /// <summary>
        /// Initializes a new instance of RabbitMQFilterService.
        /// </summary>
        /// <param name="logger">Logger for debugging and monitoring</param>
        public RabbitMQFilterService(ILogger<RabbitMQFilterService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Initialize strategy factories for each supported field
            _strategyFactories = new Dictionary<string, Func<string, IFilterStrategy<RabbitMqLogEntry>>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Timestamp"] = op => new TimestampFilterStrategy(op, _logger as ILogger<TimestampFilterStrategy>),
                ["Level"] = op => new LevelFilterStrategy(op, _logger as ILogger<LevelFilterStrategy>),
                ["Message"] = op => new MessageFilterStrategy(op, _logger as ILogger<MessageFilterStrategy>),
                ["Node"] = op => new NodeFilterStrategy(op, _logger as ILogger<NodeFilterStrategy>),
                ["ProcessUID"] = op => new ProcessUIDFilterStrategy(op, _logger as ILogger<ProcessUIDFilterStrategy>),
                ["Username"] = op => new UsernameFilterStrategy(op, _logger as ILogger<UsernameFilterStrategy>)
            };

            // Define available fields for RabbitMQ log entries
            _availableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Timestamp", "Level", "Message", "Node", "ProcessUID", "Username"
            };

            // Define supported operators for each field type
            _fieldOperators = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Timestamp"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "equals", "notequals", "greaterthan", "lessthan", 
                    "greaterthanorequal", "lessthanorequal", "between"
                },
                ["Level"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "equals", "notequals", "contains", "notcontains", 
                    "startswith", "endswith", "in", "notin"
                },
                ["Message"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "equals", "notequals", "contains", "notcontains", 
                    "startswith", "endswith", "regex"
                },
                ["Node"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "equals", "notequals", "contains", "notcontains", 
                    "startswith", "endswith", "in", "notin"
                },
                ["ProcessUID"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "equals", "notequals", "contains", "startswith", "endswith"
                },
                ["Username"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "equals", "notequals", "contains", "startswith", "endswith", "in", "notin"
                }
            };
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RabbitMqLogEntry>> ApplyFilterAsync(
            IEnumerable<RabbitMqLogEntry> logEntries,
            IFilterExpression<RabbitMqLogEntry> filterExpression,
            CancellationToken cancellationToken = default)
        {
            if (logEntries == null) throw new ArgumentNullException(nameof(logEntries));
            if (filterExpression == null) throw new ArgumentNullException(nameof(filterExpression));

            _logger.LogDebug("Applying filter expression: {Description}", filterExpression.Description);

            // Convert IEnumerable to IAsyncEnumerable manually
            var source = ConvertToAsyncEnumerable(logEntries);
            var results = new List<RabbitMqLogEntry>();

            await foreach (var item in filterExpression.EvaluateAsync(source, cancellationToken))
            {
                results.Add(item);
            }

            _logger.LogDebug("Filter applied. Input: {InputCount}, Output: {OutputCount}", 
                logEntries.Count(), results.Count);

            return results;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RabbitMqLogEntry>> ApplySimpleFiltersAsync(
            IEnumerable<RabbitMqLogEntry> logEntries,
            IEnumerable<FilterCriterion> criteria,
            CancellationToken cancellationToken = default)
        {
            if (logEntries == null) throw new ArgumentNullException(nameof(logEntries));
            if (criteria == null) throw new ArgumentNullException(nameof(criteria));

            var criteriaList = criteria.ToList();
            if (!criteriaList.Any())
            {
                _logger.LogDebug("No filter criteria provided, returning all entries");
                return logEntries;
            }

            _logger.LogDebug("Applying {Count} simple filter criteria", criteriaList.Count);

            // Validate criteria first
            var validation = ValidateFilterCriteria(criteriaList);
            if (!validation.IsValid)
            {
                var errors = string.Join(", ", validation.Errors);
                throw new ArgumentException($"Invalid filter criteria: {errors}");
            }

            // Build filter expression from criteria
            var filterExpression = BuildFilterExpression(criteriaList);
            
            return await ApplyFilterAsync(logEntries, filterExpression, cancellationToken);
        }

        /// <inheritdoc />
        public ValidationResult ValidateFilterCriteria(IEnumerable<FilterCriterion> criteria)
        {
            var result = new ValidationResult { IsValid = true };
            
            foreach (var criterion in criteria)
            {
                // Validate field name - use Field property for compatibility
                if (string.IsNullOrWhiteSpace(criterion.Field))
                {
                    result.Errors.Add("Field name cannot be empty");
                    result.IsValid = false;
                    continue;
                }

                if (!_availableFields.Contains(criterion.Field))
                {
                    result.Errors.Add($"Unknown field: {criterion.Field}");
                    result.IsValid = false;
                    continue;
                }

                // Validate operator
                if (string.IsNullOrWhiteSpace(criterion.Operator))
                {
                    result.Errors.Add($"Operator cannot be empty for field {criterion.Field}");
                    result.IsValid = false;
                    continue;
                }

                if (!_fieldOperators.TryGetValue(criterion.Field, out var validOperators) ||
                    !validOperators.Contains(criterion.Operator))
                {
                    result.Errors.Add($"Invalid operator '{criterion.Operator}' for field {criterion.Field}");
                    result.IsValid = false;
                    continue;
                }

                // Validate value using strategy
                if (_strategyFactories.TryGetValue(criterion.Field, out var factory))
                {
                    try
                    {
                        var strategy = factory(criterion.Operator);
                        if (!strategy.IsValidValue(criterion.Value))
                        {
                            result.Errors.Add($"Invalid value '{criterion.Value}' for {criterion.Field} {criterion.Operator}");
                            result.IsValid = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Error validating {criterion.Field} {criterion.Operator}: {ex.Message}");
                        result.IsValid = false;
                    }
                }
                else
                {
                    result.Warnings.Add($"No strategy factory available for field {criterion.Field}, skipping validation");
                }
            }

            return result;
        }

        /// <inheritdoc />
        public IEnumerable<string> GetAvailableFields()
        {
            return _availableFields.ToList();
        }

        /// <inheritdoc />
        public IEnumerable<string> GetAvailableOperators(string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
                return Enumerable.Empty<string>();

            return _fieldOperators.TryGetValue(fieldName, out var operators) 
                ? operators.ToList() 
                : Enumerable.Empty<string>();
        }

        /// <summary>
        /// Converts IEnumerable to IAsyncEnumerable manually.
        /// </summary>
        private async IAsyncEnumerable<RabbitMqLogEntry> ConvertToAsyncEnumerable(IEnumerable<RabbitMqLogEntry> source)
        {
            await Task.Yield(); // Make it truly async
            
            foreach (var item in source)
            {
                yield return item;
            }
        }

        /// <summary>
        /// Builds a filter expression from simple filter criteria.
        /// Combines multiple criteria using AND logic with smart ordering optimization.
        /// </summary>
        /// <param name="criteria">Filter criteria to combine</param>
        /// <returns>Composite filter expression</returns>
        private IFilterExpression<RabbitMqLogEntry> BuildFilterExpression(IList<FilterCriterion> criteria)
        {
            if (criteria.Count == 1)
            {
                // Single criterion - create a leaf filter
                var criterion = criteria[0];
                var strategy = _strategyFactories[criterion.Field!](criterion.Operator!);
                return new FilterLeaf<RabbitMqLogEntry>(strategy, criterion.Value);
            }

            // Multiple criteria - create an AND composite with smart ordering
            var composite = new FilterComposite<RabbitMqLogEntry>(LogicalOperator.And);
            
            // Apply smart ordering: order criteria by estimated selectivity (most selective first)
            var orderedCriteria = OptimizeFilterOrder(criteria);
            
            foreach (var criterion in orderedCriteria)
            {
                var strategy = _strategyFactories[criterion.Field!](criterion.Operator!);
                var leaf = new FilterLeaf<RabbitMqLogEntry>(strategy, criterion.Value);
                composite.AddChild(leaf);
            }

            _logger.LogDebug("Built filter expression with {Count} criteria (smart ordered)", criteria.Count);
            return composite;
        }

        /// <summary>
        /// Optimizes filter order by estimating selectivity and computational cost.
        /// Orders criteria from most selective (fastest elimination) to least selective.
        /// </summary>
        /// <param name="criteria">Original filter criteria</param>
        /// <returns>Optimally ordered criteria</returns>
        private IEnumerable<FilterCriterion> OptimizeFilterOrder(IList<FilterCriterion> criteria)
        {
            var criteriaWithScore = new List<(FilterCriterion Criterion, double Score)>();

            foreach (var criterion in criteria)
            {
                if (_strategyFactories.TryGetValue(criterion.Field!, out var factory))
                {
                    try
                    {
                        var strategy = factory(criterion.Operator!);
                        var selectivity = strategy.EstimateSelectivity(criterion.Value);
                        var computationalCost = EstimateComputationalCost(criterion);
                        
                        // Score = selectivity (lower is better) + computational_cost_factor
                        // Lower score = higher priority (executed first)
                        var score = selectivity + (computationalCost * 0.1); // Weight computational cost at 10%
                        
                        criteriaWithScore.Add((criterion, score));
                        
                        _logger.LogTrace("Criterion {Field} {Operator}: selectivity={Selectivity:F2}, cost={Cost:F2}, score={Score:F2}",
                            criterion.Field, criterion.Operator, selectivity, computationalCost, score);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error estimating selectivity for {Field} {Operator}, using default order",
                            criterion.Field, criterion.Operator);
                        criteriaWithScore.Add((criterion, 0.5)); // Default moderate selectivity
                    }
                }
                else
                {
                    _logger.LogWarning("No strategy factory for field {Field}, using default order", criterion.Field);
                    criteriaWithScore.Add((criterion, 0.5)); // Default moderate selectivity
                }
            }

            // Order by score (ascending - lower score = higher selectivity = execute first)
            return criteriaWithScore.OrderBy(x => x.Score).Select(x => x.Criterion);
        }

        /// <summary>
        /// Estimates computational cost of a filter criterion.
        /// Used to balance selectivity with execution time.
        /// </summary>
        /// <param name="criterion">Filter criterion to evaluate</param>
        /// <returns>Computational cost estimate (0.0 = fast, 1.0 = slow)</returns>
        private double EstimateComputationalCost(FilterCriterion criterion)
        {
            // Estimate computational cost based on field type and operator
            var fieldCost = criterion.Field?.ToLowerInvariant() switch
            {
                "timestamp" => 0.2,    // DateTime comparisons are relatively fast
                "level" => 0.1,        // String enum comparisons are very fast
                "message" => 0.6,      // String operations on potentially large text
                "node" => 0.3,         // String operations on short identifiers
                "processuid" => 0.3,   // String operations on short identifiers
                "username" => 0.3,     // String operations on short identifiers
                _ => 0.4               // Default cost
            };

            var operatorCost = criterion.Operator?.ToLowerInvariant() switch
            {
                "equals" => 0.1,       // Simple equality is fast
                "notequals" => 0.1,    // Simple inequality is fast
                "contains" => 0.4,     // String searching is moderate
                "notcontains" => 0.4,  // String searching is moderate
                "startswith" => 0.2,   // Prefix matching is relatively fast
                "endswith" => 0.3,     // Suffix matching is slightly slower
                "regex" => 0.8,        // Regex is expensive
                "in" => 0.3,           // Array lookup is moderate
                "notin" => 0.3,        // Array lookup is moderate
                "between" => 0.2,      // Range check is fast
                "greaterthan" => 0.1,  // Simple comparison is fast
                "lessthan" => 0.1,     // Simple comparison is fast
                _ => 0.3               // Default cost
            };

            return Math.Min(1.0, fieldCost + operatorCost); // Cap at 1.0
        }
    }
} 
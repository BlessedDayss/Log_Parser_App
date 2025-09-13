using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Log_Parser_App.Services.Filtering.Interfaces;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.Filtering
{
    public class RabbitMQFilterService : IRabbitMQFilterService
    {
        private readonly ILogger<RabbitMQFilterService> _logger;
        private readonly IFilterStrategyFactory<RabbitMqLogEntry> _strategyFactory;
        private readonly IFieldMetadataProvider _fieldMetadata;

        public RabbitMQFilterService(
            ILogger<RabbitMQFilterService> logger,
            IFilterStrategyFactory<RabbitMqLogEntry> strategyFactory,
            IFieldMetadataProvider fieldMetadata)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _strategyFactory = strategyFactory ?? throw new ArgumentNullException(nameof(strategyFactory));
            _fieldMetadata = fieldMetadata ?? throw new ArgumentNullException(nameof(fieldMetadata));
        }

        public async Task<IEnumerable<RabbitMqLogEntry>> ApplyFilterAsync(
            IEnumerable<RabbitMqLogEntry> logEntries,
            IFilterExpression<RabbitMqLogEntry> filterExpression,
            CancellationToken cancellationToken = default)
        {
            if (logEntries == null)
                throw new ArgumentNullException(nameof(logEntries));
            if (filterExpression == null)
                throw new ArgumentNullException(nameof(filterExpression));

            var results = new List<RabbitMqLogEntry>();
            var source = ConvertToAsyncEnumerable(logEntries);

            await foreach (var item in filterExpression.EvaluateAsync(source, cancellationToken))
            {
                results.Add(item);
            }

            _logger.LogDebug("Filter applied. Input: {InputCount}, Output: {OutputCount}",
                logEntries.Count(), results.Count);

            return results;
        }

        public async Task<IEnumerable<RabbitMqLogEntry>> ApplySimpleFiltersAsync(
            IEnumerable<RabbitMqLogEntry> logEntries,
            IEnumerable<FilterCriterion> criteria,
            CancellationToken cancellationToken = default)
        {
            if (logEntries == null)
                throw new ArgumentNullException(nameof(logEntries));
            if (criteria == null)
                throw new ArgumentNullException(nameof(criteria));

            var criteriaList = criteria.ToList();
            if (!criteriaList.Any())
            {
                _logger.LogDebug("No filter criteria provided, returning all entries");
                return logEntries;
            }

            var validation = ValidateFilterCriteria(criteriaList);
            if (!validation.IsValid)
            {
                var errors = string.Join(", ", validation.Errors);
                throw new ArgumentException($"Invalid filter criteria: {errors}");
            }

            var filterExpression = BuildFilterExpression(criteriaList);
            return await ApplyFilterAsync(logEntries, filterExpression, cancellationToken);
        }

        public ValidationResult ValidateFilterCriteria(IEnumerable<FilterCriterion> criteria)
        {
            var result = new ValidationResult { IsValid = true };

            foreach (var criterion in criteria)
            {
                if (string.IsNullOrWhiteSpace(criterion.Field))
                {
                    result.Errors.Add("Field name cannot be empty");
                    result.IsValid = false;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(criterion.Operator))
                {
                    result.Errors.Add($"Operator cannot be empty for field {criterion.Field}");
                    result.IsValid = false;
                    continue;
                }

                if (!_fieldMetadata.IsFieldSupported(criterion.Field))
                {
                    result.Errors.Add($"Unknown field: {criterion.Field}");
                    result.IsValid = false;
                    continue;
                }

                if (!_fieldMetadata.IsOperatorSupported(criterion.Field, criterion.Operator))
                {
                    result.Errors.Add($"Invalid operator '{criterion.Operator}' for field {criterion.Field}");
                    result.IsValid = false;
                    continue;
                }

                try
                {
                    var strategy = _strategyFactory.CreateStrategy(criterion.Field, criterion.Operator);
                    if (criterion.Value == null || !strategy.IsValidValue(criterion.Value))
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

            return result;
        }

        public IEnumerable<string> GetAvailableFields()
        {
            return _fieldMetadata.GetAvailableFields();
        }

        public IEnumerable<string> GetAvailableOperators(string fieldName)
        {
            return _fieldMetadata.GetAvailableOperators(fieldName);
        }

        private async IAsyncEnumerable<RabbitMqLogEntry> ConvertToAsyncEnumerable(IEnumerable<RabbitMqLogEntry> source)
        {
            await Task.Yield();

            foreach (var item in source)
            {
                yield return item;
            }
        }

        private IFilterExpression<RabbitMqLogEntry> BuildFilterExpression(IList<FilterCriterion> criteria)
        {
            if (criteria.Count == 1)
            {
                var criterion = criteria[0];
                var strategy = _strategyFactory.CreateStrategy(criterion.Field!, criterion.Operator!);
                return new FilterLeaf<RabbitMqLogEntry>(strategy, criterion.Value ?? string.Empty);
            }

            var composite = new FilterComposite<RabbitMqLogEntry>(LogicalOperator.And);
            var orderedCriteria = OptimizeFilterOrder(criteria);

            foreach (var criterion in orderedCriteria)
            {
                var strategy = _strategyFactory.CreateStrategy(criterion.Field!, criterion.Operator!);
                var leaf = new FilterLeaf<RabbitMqLogEntry>(strategy, criterion.Value ?? string.Empty);
                composite.AddChild(leaf);
            }

            return composite;
        }

        private IEnumerable<FilterCriterion> OptimizeFilterOrder(IList<FilterCriterion> criteria)
        {
            var criteriaWithScore = new List<(FilterCriterion Criterion, double Score)>();

            foreach (var criterion in criteria)
            {
                try
                {
                    var strategy = _strategyFactory.CreateStrategy(criterion.Field!, criterion.Operator!);
                    var selectivity = criterion.Value != null ? strategy.EstimateSelectivity(criterion.Value) : 0.5;
                    var cost = EstimateComputationalCost(criterion);
                    var score = selectivity + (cost * 0.1);

                    criteriaWithScore.Add((criterion, score));
                }
                catch
                {
                    criteriaWithScore.Add((criterion, 0.5));
                }
            }

            return criteriaWithScore.OrderBy(x => x.Score).Select(x => x.Criterion);
        }

        private double EstimateComputationalCost(FilterCriterion criterion)
        {
            var fieldCost = criterion.Field?.ToLowerInvariant() switch
            {
                "timestamp" => 0.2,
                "level" => 0.1,
                "message" => 0.6,
                "node" => 0.3,
                "processuid" => 0.3,
                "username" => 0.3,
                _ => 0.4
            };

            var operatorCost = criterion.Operator?.ToLowerInvariant() switch
            {
                "equals" => 0.1,
                "notequals" => 0.1,
                "contains" => 0.4,
                "notcontains" => 0.4,
                "startswith" => 0.2,
                "endswith" => 0.3,
                "regex" => 0.8,
                "in" => 0.3,
                "notin" => 0.3,
                "between" => 0.2,
                "greaterthan" => 0.1,
                "lessthan" => 0.1,
                _ => 0.3
            };

            return Math.Min(1.0, fieldCost + operatorCost);
        }
    }
} 
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Interfaces
{
    /// <summary>
    /// Dedicated service interface for RabbitMQ message filtering operations.
    /// Follows Single Responsibility Principle by isolating RabbitMQ-specific filtering logic.
    /// </summary>
    public interface IRabbitMQFilterService
    {
        /// <summary>
        /// Applies filter expression to RabbitMQ log entries asynchronously.
        /// Uses lazy evaluation for memory efficiency with large datasets.
        /// </summary>
        /// <param name="logEntries">Source RabbitMQ log entries to filter</param>
        /// <param name="filterExpression">Filter expression to apply</param>
        /// <param name="cancellationToken">Cancellation token for async operations</param>
        /// <returns>Filtered RabbitMQ log entries matching the criteria</returns>
        Task<IEnumerable<RabbitMqLogEntry>> ApplyFilterAsync(
            IEnumerable<RabbitMqLogEntry> logEntries,
            IFilterExpression<RabbitMqLogEntry> filterExpression,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Applies simple filter criteria to RabbitMQ log entries.
        /// Convenience method for basic filtering scenarios.
        /// </summary>
        /// <param name="logEntries">Source RabbitMQ log entries to filter</param>
        /// <param name="criteria">Simple filter criteria to apply</param>
        /// <param name="cancellationToken">Cancellation token for async operations</param>
        /// <returns>Filtered RabbitMQ log entries matching the criteria</returns>
        Task<IEnumerable<RabbitMqLogEntry>> ApplySimpleFiltersAsync(
            IEnumerable<RabbitMqLogEntry> logEntries,
            IEnumerable<FilterCriterion> criteria,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Validates filter criteria for RabbitMQ log entries.
        /// Checks field compatibility and operator validity.
        /// </summary>
        /// <param name="criteria">Filter criteria to validate</param>
        /// <returns>Validation result with error details if invalid</returns>
        ValidationResult ValidateFilterCriteria(IEnumerable<FilterCriterion> criteria);
        
        /// <summary>
        /// Gets available filter fields for RabbitMQ log entries.
        /// Returns field names that can be used in filter criteria.
        /// </summary>
        /// <returns>Collection of available field names</returns>
        IEnumerable<string> GetAvailableFields();
        
        /// <summary>
        /// Gets available operators for a specific field in RabbitMQ log entries.
        /// Returns operators compatible with the field type.
        /// </summary>
        /// <param name="fieldName">Name of the field to get operators for</param>
        /// <returns>Collection of available operators</returns>
        IEnumerable<string> GetAvailableOperators(string fieldName);
    }
    

} 
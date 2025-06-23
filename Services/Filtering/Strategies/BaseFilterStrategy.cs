using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.Filtering.Strategies
{
    /// <summary>
    /// Base class for all filter strategies providing common functionality.
    /// Implements common validation and selectivity estimation patterns.
    /// </summary>
    /// <typeparam name="T">Type of log entry to filter</typeparam>
    public abstract class BaseFilterStrategy<T> : IFilterStrategy<T>
    {
        protected readonly ILogger? _logger;

        /// <summary>
        /// Initializes base filter strategy with optional logging.
        /// </summary>
        /// <param name="logger">Optional logger for debugging</param>
        protected BaseFilterStrategy(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public abstract string FieldName { get; }

        /// <inheritdoc />
        public abstract string Operator { get; }

        /// <inheritdoc />
        public async IAsyncEnumerable<T> ApplyAsync(
            IAsyncEnumerable<T> source, 
            object value, 
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!IsValidValue(value))
            {
                _logger?.LogWarning("Invalid value '{Value}' for strategy {Strategy}", value, $"{FieldName} {Operator}");
                throw new ArgumentException($"Invalid value '{value}' for strategy {FieldName} {Operator}", nameof(value));
            }

            _logger?.LogDebug("Applying filter strategy: {Strategy} with value '{Value}'", $"{FieldName} {Operator}", value);

            await foreach (var item in source)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (Matches(item, value))
                {
                    yield return item;
                }
            }
        }

        /// <inheritdoc />
        public abstract bool IsValidValue(object value);

        /// <inheritdoc />
        public virtual double EstimateSelectivity(object value)
        {
            // Default implementation provides conservative estimates
            // Override in specific strategies for better optimization
            return Operator.ToLowerInvariant() switch
            {
                "equals" => 0.1,           // Exact matches are typically selective
                "notequals" => 0.9,        // Not equals typically matches most items
                "contains" => 0.3,         // Contains is moderately selective
                "notcontains" => 0.7,      // Not contains matches most items
                "startswith" => 0.2,       // Prefix matches are fairly selective
                "endswith" => 0.2,         // Suffix matches are fairly selective
                "greaterthan" => 0.5,      // Range comparisons vary widely
                "lessthan" => 0.5,         // Range comparisons vary widely
                "greaterthanorequal" => 0.5,
                "lessthanorequal" => 0.5,
                "between" => 0.3,          // Range filters are moderately selective
                "regex" => 0.4,            // Regex selectivity varies widely
                _ => 0.5                   // Unknown operators get neutral estimate
            };
        }

        /// <summary>
        /// Determines if a log entry matches the filter criteria.
        /// Must be implemented by concrete strategies.
        /// </summary>
        /// <param name="item">Log entry to test</param>
        /// <param name="value">Filter value to compare against</param>
        /// <returns>True if the item matches the filter criteria</returns>
        protected abstract bool Matches(T item, object value);

        /// <summary>
        /// Helper method to safely extract field value from log entry.
        /// Returns null if field is not found or extraction fails.
        /// </summary>
        /// <param name="item">Log entry to extract field from</param>
        /// <param name="fieldName">Name of field to extract</param>
        /// <returns>Field value or null if not found</returns>
        protected virtual object? GetFieldValue(T item, string fieldName)
        {
            if (item == null) return null;

            try
            {
                var property = typeof(T).GetProperty(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                return property?.GetValue(item);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to extract field '{FieldName}' from item of type {Type}", fieldName, typeof(T).Name);
                return null;
            }
        }

        /// <summary>
        /// Helper method for safe string comparison with null handling.
        /// </summary>
        /// <param name="value1">First value to compare</param>
        /// <param name="value2">Second value to compare</param>
        /// <param name="comparisonType">Type of string comparison</param>
        /// <returns>True if strings are equal according to comparison type</returns>
        protected static bool SafeStringEquals(object? value1, object? value2, StringComparison comparisonType = StringComparison.OrdinalIgnoreCase)
        {
            var str1 = value1?.ToString();
            var str2 = value2?.ToString();
            
            if (str1 == null && str2 == null) return true;
            if (str1 == null || str2 == null) return false;
            
            return string.Equals(str1, str2, comparisonType);
        }

        /// <summary>
        /// Helper method for safe string contains check with null handling.
        /// </summary>
        /// <param name="source">Source string to search in</param>
        /// <param name="value">Value to search for</param>
        /// <param name="comparisonType">Type of string comparison</param>
        /// <returns>True if source contains value</returns>
        protected static bool SafeStringContains(object? source, object? value, StringComparison comparisonType = StringComparison.OrdinalIgnoreCase)
        {
            var sourceStr = source?.ToString();
            var valueStr = value?.ToString();
            
            if (sourceStr == null || valueStr == null) return false;
            
            return sourceStr.Contains(valueStr, comparisonType);
        }

        /// <summary>
        /// Helper method for safe numeric comparison with type conversion.
        /// </summary>
        /// <param name="value1">First value to compare</param>
        /// <param name="value2">Second value to compare</param>
        /// <returns>Comparison result, or null if conversion fails</returns>
        protected static int? SafeNumericComparison(object? value1, object? value2)
        {
            try
            {
                if (value1 == null || value2 == null) return null;

                var decimal1 = Convert.ToDecimal(value1);
                var decimal2 = Convert.ToDecimal(value2);

                return decimal1.CompareTo(decimal2);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Creates a string representation of the strategy for debugging.
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString() => $"{FieldName} {Operator}";
    }
} 
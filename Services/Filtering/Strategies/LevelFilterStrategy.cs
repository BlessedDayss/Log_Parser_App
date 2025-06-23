using System;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.Filtering.Strategies
{
    /// <summary>
    /// Filter strategy for RabbitMQ log entry level field.
    /// Supports string-based level comparisons (ERROR, WARN, INFO, DEBUG, etc.).
    /// </summary>
    public class LevelFilterStrategy : BaseFilterStrategy<RabbitMqLogEntry>
    {
        /// <summary>
        /// Initializes a new instance of LevelFilterStrategy.
        /// </summary>
        /// <param name="operator">Comparison operator to use</param>
        /// <param name="logger">Optional logger for debugging</param>
        public LevelFilterStrategy(string @operator, ILogger<LevelFilterStrategy>? logger = null) 
            : base(logger)
        {
            Operator = @operator ?? throw new ArgumentNullException(nameof(@operator));
        }

        /// <inheritdoc />
        public override string FieldName => "Level";

        /// <inheritdoc />
        public override string Operator { get; }

        /// <inheritdoc />
        public override bool IsValidValue(object value)
        {
            if (value == null) return false;

            // Support string values for level names
            if (value is string) return true;

            // Support arrays for multiple level selection
            if (value is string[] stringArray)
            {
                return stringArray.Length > 0;
            }
            
            if (value is object[] objectArray)
            {
                return objectArray.Length > 0;
            }

            return false;
        }

        /// <inheritdoc />
        public override double EstimateSelectivity(object value)
        {
            // Level filtering selectivity depends on the level being filtered
            if (value is string levelStr)
            {
                return levelStr.ToLowerInvariant() switch
                {
                    "error" or "fatal" or "critical" => 0.05,  // Error levels are rare
                    "warn" or "warning" => 0.15,               // Warnings are moderately common
                    "info" or "information" => 0.5,            // Info is very common
                    "debug" => 0.3,                            // Debug varies by configuration
                    "trace" => 0.1,                            // Trace is usually rare
                    _ => 0.25                                  // Unknown levels get moderate estimate
                };
            }

            return Operator.ToLowerInvariant() switch
            {
                "equals" => 0.2,           // Level equals is moderately selective
                "notequals" => 0.8,        // Not equals matches most levels
                "contains" => 0.3,         // Contains for partial level matching
                _ => 0.5                   // Default for level operations
            };
        }

        /// <inheritdoc />
        protected override bool Matches(RabbitMqLogEntry item, object value)
        {
            var itemLevel = item?.EffectiveLevel ?? item?.Level;
            if (string.IsNullOrEmpty(itemLevel)) return false;

            return Operator.ToLowerInvariant() switch
            {
                "equals" => MatchesEquals(itemLevel, value),
                "notequals" => !MatchesEquals(itemLevel, value),
                "contains" => MatchesContains(itemLevel, value),
                "notcontains" => !MatchesContains(itemLevel, value),
                "startswith" => MatchesStartsWith(itemLevel, value),
                "endswith" => MatchesEndsWith(itemLevel, value),
                "in" => MatchesIn(itemLevel, value),
                "notin" => !MatchesIn(itemLevel, value),
                _ => false
            };
        }

        private bool MatchesEquals(string itemLevel, object value)
        {
            return SafeStringEquals(itemLevel, value, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesContains(string itemLevel, object value)
        {
            return SafeStringContains(itemLevel, value, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesStartsWith(string itemLevel, object value)
        {
            var valueStr = value?.ToString();
            if (valueStr == null) return false;
            
            return itemLevel.StartsWith(valueStr, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesEndsWith(string itemLevel, object value)
        {
            var valueStr = value?.ToString();
            if (valueStr == null) return false;
            
            return itemLevel.EndsWith(valueStr, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesIn(string itemLevel, object value)
        {
            if (value is string singleValue)
            {
                return MatchesEquals(itemLevel, singleValue);
            }

            if (value is string[] stringArray)
            {
                foreach (var level in stringArray)
                {
                    if (MatchesEquals(itemLevel, level))
                        return true;
                }
            }

            if (value is object[] objectArray)
            {
                foreach (var level in objectArray)
                {
                    if (MatchesEquals(itemLevel, level))
                        return true;
                }
            }

            return false;
        }
    }
} 
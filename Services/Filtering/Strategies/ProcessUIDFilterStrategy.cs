using System;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.Filtering.Strategies
{
    /// <summary>
    /// Filter strategy for RabbitMQ log entry ProcessUID field.
    /// Supports filtering by process unique identifiers.
    /// </summary>
    public class ProcessUIDFilterStrategy : BaseFilterStrategy<RabbitMqLogEntry>
    {
        /// <summary>
        /// Initializes a new instance of ProcessUIDFilterStrategy.
        /// </summary>
        /// <param name="operator">Comparison operator to use</param>
        /// <param name="logger">Optional logger for debugging</param>
        public ProcessUIDFilterStrategy(string @operator, ILogger<ProcessUIDFilterStrategy>? logger = null) 
            : base(logger)
        {
            Operator = @operator ?? throw new ArgumentNullException(nameof(@operator));
        }

        /// <inheritdoc />
        public override string FieldName => "ProcessUID";

        /// <inheritdoc />
        public override string Operator { get; }

        /// <inheritdoc />
        public override bool IsValidValue(object value)
        {
            if (value == null) return false;

            // Support string values for ProcessUID
            if (value is string stringValue)
            {
                // ProcessUID should not be empty for meaningful filtering
                return !string.IsNullOrWhiteSpace(stringValue);
            }

            return false;
        }

        /// <inheritdoc />
        public override double EstimateSelectivity(object value)
        {
            // ProcessUID filtering is typically very selective since UIDs are unique
            if (value is string processUidStr)
            {
                return Operator.ToLowerInvariant() switch
                {
                    "equals" => 0.05,          // Exact ProcessUID matches are very selective
                    "notequals" => 0.95,       // Not equals matches most ProcessUIDs
                    "contains" => processUidStr.Length switch
                    {
                        <= 4 => 0.4,    // Short UID parts may match several processes
                        <= 10 => 0.2,   // Medium UID parts are quite selective
                        _ => 0.05       // Long UID parts are very selective
                    },
                    "startswith" => processUidStr.Length switch
                    {
                        <= 4 => 0.3,    // Short prefixes may match several UIDs
                        <= 8 => 0.1,    // Medium prefixes are very selective
                        _ => 0.05       // Long prefixes are extremely selective
                    },
                    "endswith" => processUidStr.Length switch
                    {
                        <= 4 => 0.3,    // Short suffixes may match several UIDs
                        <= 8 => 0.1,    // Medium suffixes are very selective  
                        _ => 0.05       // Long suffixes are extremely selective
                    },
                    _ => 0.1                   // Default for ProcessUID operations is selective
                };
            }

            return base.EstimateSelectivity(value);
        }

        /// <inheritdoc />
        protected override bool Matches(RabbitMqLogEntry item, object value)
        {
            var itemProcessUID = item?.EffectiveProcessUID;
            if (string.IsNullOrEmpty(itemProcessUID)) return false;

            return Operator.ToLowerInvariant() switch
            {
                "equals" => MatchesEquals(itemProcessUID, value),
                "notequals" => !MatchesEquals(itemProcessUID, value),
                "contains" => MatchesContains(itemProcessUID, value),
                "startswith" => MatchesStartsWith(itemProcessUID, value),
                "endswith" => MatchesEndsWith(itemProcessUID, value),
                _ => false
            };
        }

        private bool MatchesEquals(string itemProcessUID, object value)
        {
            return SafeStringEquals(itemProcessUID, value, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesContains(string itemProcessUID, object value)
        {
            return SafeStringContains(itemProcessUID, value, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesStartsWith(string itemProcessUID, object value)
        {
            var valueStr = value?.ToString();
            if (valueStr == null) return false;
            
            return itemProcessUID.StartsWith(valueStr, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesEndsWith(string itemProcessUID, object value)
        {
            var valueStr = value?.ToString();
            if (valueStr == null) return false;
            
            return itemProcessUID.EndsWith(valueStr, StringComparison.OrdinalIgnoreCase);
        }
    }
} 
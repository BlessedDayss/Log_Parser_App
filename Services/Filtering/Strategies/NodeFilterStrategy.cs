using System;
using System.Linq;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.Filtering.Strategies
{
    /// <summary>
    /// Filter strategy for RabbitMQ log entry node field.
    /// Supports filtering by node/host names with various string operations.
    /// </summary>
    public class NodeFilterStrategy : BaseFilterStrategy<RabbitMqLogEntry>
    {
        /// <summary>
        /// Initializes a new instance of NodeFilterStrategy.
        /// </summary>
        /// <param name="operator">Comparison operator to use</param>
        /// <param name="logger">Optional logger for debugging</param>
        public NodeFilterStrategy(string @operator, ILogger<NodeFilterStrategy>? logger = null) 
            : base(logger)
        {
            Operator = @operator ?? throw new ArgumentNullException(nameof(@operator));
        }

        /// <inheritdoc />
        public override string FieldName => "Node";

        /// <inheritdoc />
        public override string Operator { get; }

        /// <inheritdoc />
        public override bool IsValidValue(object value)
        {
            if (value == null) return false;

            // Support string values for node names
            if (value is string) return true;

            // Support arrays for multiple node selection
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
            // Node filtering selectivity depends on the environment
            if (value is string nodeStr)
            {
                return Operator.ToLowerInvariant() switch
                {
                    "equals" => 0.3,           // Single node selection is moderately selective
                    "notequals" => 0.7,        // Not equals matches most nodes
                    "contains" => nodeStr.Length switch
                    {
                        <= 3 => 0.6,    // Short node name parts match many
                        <= 8 => 0.4,    // Medium node name parts are moderately selective  
                        _ => 0.2        // Long node name parts are very selective
                    },
                    "notcontains" => nodeStr.Length switch
                    {
                        <= 3 => 0.4,    // Not containing short strings is selective
                        <= 8 => 0.6,    // Not containing medium strings matches more
                        _ => 0.8        // Not containing long strings matches most
                    },
                    "startswith" => 0.3,       // Node name prefixes are moderately selective
                    "endswith" => 0.3,         // Node name suffixes are moderately selective  
                    "in" => 0.5,               // Multiple node selection varies
                    "notin" => 0.5,            // Multiple node exclusion varies
                    _ => 0.4
                };
            }

            return base.EstimateSelectivity(value);
        }

        /// <inheritdoc />
        protected override bool Matches(RabbitMqLogEntry item, object value)
        {
            var itemNode = item?.EffectiveNode;
            if (string.IsNullOrEmpty(itemNode)) return false;

            return Operator.ToLowerInvariant() switch
            {
                "equals" => MatchesEquals(itemNode, value),
                "notequals" => !MatchesEquals(itemNode, value),
                "contains" => MatchesContains(itemNode, value),
                "notcontains" => !MatchesContains(itemNode, value),
                "startswith" => MatchesStartsWith(itemNode, value),
                "endswith" => MatchesEndsWith(itemNode, value),
                "in" => MatchesIn(itemNode, value),
                "notin" => !MatchesIn(itemNode, value),
                _ => false
            };
        }

        private bool MatchesEquals(string itemNode, object value)
        {
            return SafeStringEquals(itemNode, value, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesContains(string itemNode, object value)
        {
            return SafeStringContains(itemNode, value, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesStartsWith(string itemNode, object value)
        {
            var valueStr = value?.ToString();
            if (valueStr == null) return false;
            
            return itemNode.StartsWith(valueStr, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesEndsWith(string itemNode, object value)
        {
            var valueStr = value?.ToString();
            if (valueStr == null) return false;
            
            return itemNode.EndsWith(valueStr, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesIn(string itemNode, object value)
        {
            if (value is string singleValue)
            {
                return MatchesEquals(itemNode, singleValue);
            }

            if (value is string[] stringArray)
            {
                return stringArray.Any(node => MatchesEquals(itemNode, node));
            }

            if (value is object[] objectArray)
            {
                return objectArray.Any(node => MatchesEquals(itemNode, node));
            }

            return false;
        }
    }
} 
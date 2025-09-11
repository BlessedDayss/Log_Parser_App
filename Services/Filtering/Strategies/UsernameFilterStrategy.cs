using System;
using System.Linq;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.Filtering.Strategies
{
    /// <summary>
    /// Filter strategy for RabbitMQ log entry Username field.
    /// Supports filtering by user names with various string operations.
    /// </summary>
    public class UsernameFilterStrategy : BaseFilterStrategy<RabbitMqLogEntry>
    {
        /// <summary>
        /// Initializes a new instance of UsernameFilterStrategy.
        /// </summary>
        /// <param name="operator">Comparison operator to use</param>
        /// <param name="logger">Optional logger for debugging</param>
        public UsernameFilterStrategy(string @operator, ILogger<UsernameFilterStrategy>? logger = null) 
            : base(logger)
        {
            Operator = @operator ?? throw new ArgumentNullException(nameof(@operator));
        }

        /// <inheritdoc />
        public override string FieldName => "Username";

        /// <inheritdoc />
        public override string Operator { get; }

        /// <inheritdoc />
        public override bool IsValidValue(object value)
        {
            if (value == null) return false;

            // Support string values for usernames
            if (value is string) return true;

            // Support arrays for multiple username selection
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
            // Username filtering selectivity depends on user diversity
            if (value is string usernameStr)
            {
                return Operator.ToLowerInvariant() switch
                {
                    "equals" => 0.2,           // Single username is quite selective
                    "notequals" => 0.8,        // Not equals matches most usernames
                    "contains" => usernameStr.Length switch
                    {
                        <= 3 => 0.5,    // Short username parts match several users
                        <= 8 => 0.3,    // Medium username parts are moderately selective  
                        _ => 0.15       // Long username parts are very selective
                    },
                    "notcontains" => usernameStr.Length switch
                    {
                        <= 3 => 0.5,    // Not containing short strings 
                        <= 8 => 0.7,    // Not containing medium strings matches more
                        _ => 0.85       // Not containing long strings matches most
                    },
                    "startswith" => usernameStr.Length switch
                    {
                        <= 3 => 0.4,    // Short prefixes may match several usernames
                        <= 6 => 0.2,    // Medium prefixes are quite selective
                        _ => 0.1        // Long prefixes are very selective
                    },
                    "endswith" => usernameStr.Length switch
                    {
                        <= 3 => 0.4,    // Short suffixes may match several usernames
                        <= 6 => 0.2,    // Medium suffixes are quite selective  
                        _ => 0.1        // Long suffixes are very selective
                    },
                    "in" => 0.4,               // Multiple username selection varies
                    "notin" => 0.6,            // Multiple username exclusion varies
                    _ => 0.3
                };
            }

            return base.EstimateSelectivity(value);
        }

        /// <inheritdoc />
        protected override bool Matches(RabbitMqLogEntry item, object value)
        {
            var itemUsername = item?.EffectiveUserName;
            if (string.IsNullOrEmpty(itemUsername)) return false;

            return Operator.ToLowerInvariant() switch
            {
                "equals" => MatchesEquals(itemUsername, value),
                "notequals" => !MatchesEquals(itemUsername, value),
                "contains" => MatchesContains(itemUsername, value),
                "notcontains" => !MatchesContains(itemUsername, value),
                "startswith" => MatchesStartsWith(itemUsername, value),
                "endswith" => MatchesEndsWith(itemUsername, value),
                "in" => MatchesIn(itemUsername, value),
                "notin" => !MatchesIn(itemUsername, value),
                _ => false
            };
        }

        private bool MatchesEquals(string itemUsername, object value)
        {
            return SafeStringEquals(itemUsername, value, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesContains(string itemUsername, object value)
        {
            return SafeStringContains(itemUsername, value, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesStartsWith(string itemUsername, object value)
        {
            var valueStr = value?.ToString();
            if (valueStr == null) return false;
            
            return itemUsername.StartsWith(valueStr, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesEndsWith(string itemUsername, object value)
        {
            var valueStr = value?.ToString();
            if (valueStr == null) return false;
            
            return itemUsername.EndsWith(valueStr, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesIn(string itemUsername, object value)
        {
            if (value is string singleValue)
            {
                return MatchesEquals(itemUsername, singleValue);
            }

            if (value is string[] stringArray)
            {
                return stringArray.Any(username => MatchesEquals(itemUsername, username));
            }

            if (value is object[] objectArray)
            {
                return objectArray.Any(username => MatchesEquals(itemUsername, username));
            }

            return false;
        }
    }
} 
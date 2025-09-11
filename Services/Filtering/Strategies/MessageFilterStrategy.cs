using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.Filtering.Strategies
{
    /// <summary>
    /// Filter strategy for RabbitMQ log entry message field.
    /// Supports string-based message filtering including regex patterns.
    /// </summary>
    public class MessageFilterStrategy : BaseFilterStrategy<RabbitMqLogEntry>
    {
        private readonly Dictionary<string, Regex> _regexCache = new();

        /// <summary>
        /// Initializes a new instance of MessageFilterStrategy.
        /// </summary>
        /// <param name="operator">Comparison operator to use</param>
        /// <param name="logger">Optional logger for debugging</param>
        public MessageFilterStrategy(string @operator, ILogger<MessageFilterStrategy>? logger = null) 
            : base(logger)
        {
            Operator = @operator ?? throw new ArgumentNullException(nameof(@operator));
        }

        /// <inheritdoc />
        public override string FieldName => "Message";

        /// <inheritdoc />
        public override string Operator { get; }

        /// <inheritdoc />
        public override bool IsValidValue(object value)
        {
            if (value == null) return false;

            // Support string values for message content
            if (value is string stringValue)
            {
                // For regex operator, validate regex pattern
                if (Operator.Equals("regex", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        new Regex(stringValue, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                        return true;
                    }
                    catch (ArgumentException)
                    {
                        return false;
                    }
                }
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public override double EstimateSelectivity(object value)
        {
            if (value is string messageStr)
            {
                // Estimate selectivity based on message content and operator
                var messageLength = messageStr.Length;
                
                return Operator.ToLowerInvariant() switch
                {
                    "equals" => messageLength > 20 ? 0.05 : 0.15,     // Long exact messages are very selective
                    "notequals" => messageLength > 20 ? 0.95 : 0.85,  // Not equals of long messages matches most
                    "contains" => messageLength switch
                    {
                        <= 3 => 0.7,    // Short substrings match many messages
                        <= 10 => 0.4,   // Medium substrings are moderately selective
                        _ => 0.15       // Long substrings are very selective
                    },
                    "notcontains" => messageLength switch
                    {
                        <= 3 => 0.3,    // Not containing short strings is selective
                        <= 10 => 0.6,   // Not containing medium strings matches more
                        _ => 0.85       // Not containing long strings matches most
                    },
                    "startswith" => messageLength <= 5 ? 0.3 : 0.1,   // Prefix matching varies by length
                    "endswith" => messageLength <= 5 ? 0.3 : 0.1,     // Suffix matching varies by length
                    "regex" => 0.25,                                   // Regex patterns have moderate selectivity
                    _ => 0.4
                };
            }

            return base.EstimateSelectivity(value);
        }

        /// <inheritdoc />
        protected override bool Matches(RabbitMqLogEntry item, object value)
        {
            var itemMessage = item?.EffectiveMessage;
            if (string.IsNullOrEmpty(itemMessage)) return false;

            return Operator.ToLowerInvariant() switch
            {
                "equals" => MatchesEquals(itemMessage, value),
                "notequals" => !MatchesEquals(itemMessage, value),
                "contains" => MatchesContains(itemMessage, value),
                "notcontains" => !MatchesContains(itemMessage, value),
                "startswith" => MatchesStartsWith(itemMessage, value),
                "endswith" => MatchesEndsWith(itemMessage, value),
                "regex" => MatchesRegex(itemMessage, value),
                _ => false
            };
        }

        private bool MatchesEquals(string itemMessage, object value)
        {
            return SafeStringEquals(itemMessage, value, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesContains(string itemMessage, object value)
        {
            return SafeStringContains(itemMessage, value, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesStartsWith(string itemMessage, object value)
        {
            var valueStr = value?.ToString();
            if (valueStr == null) return false;
            
            return itemMessage.StartsWith(valueStr, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesEndsWith(string itemMessage, object value)
        {
            var valueStr = value?.ToString();
            if (valueStr == null) return false;
            
            return itemMessage.EndsWith(valueStr, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesRegex(string itemMessage, object value)
        {
            var pattern = value?.ToString();
            if (string.IsNullOrEmpty(pattern)) return false;

            try
            {
                // Use cached regex for performance
                if (!_regexCache.TryGetValue(pattern, out var regex))
                {
                    regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    _regexCache[pattern] = regex;
                    
                    // Limit cache size to prevent memory issues
                    if (_regexCache.Count > 50)
                    {
                        var oldestKey = _regexCache.Keys.First();
                        _regexCache.Remove(oldestKey);
                    }
                }

                return regex.IsMatch(itemMessage);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error executing regex pattern '{Pattern}' against message", pattern);
                return false;
            }
        }
    }
} 
using System;
using System.Globalization;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.Filtering.Strategies
{
    /// <summary>
    /// Filter strategy for RabbitMQ log entry timestamp field.
    /// Supports date/time comparisons with various operators.
    /// </summary>
    public class TimestampFilterStrategy : BaseFilterStrategy<RabbitMqLogEntry>
    {
        /// <summary>
        /// Initializes a new instance of TimestampFilterStrategy.
        /// </summary>
        /// <param name="operator">Comparison operator to use</param>
        /// <param name="logger">Optional logger for debugging</param>
        public TimestampFilterStrategy(string @operator, ILogger<TimestampFilterStrategy>? logger = null) 
            : base(logger)
        {
            Operator = @operator ?? throw new ArgumentNullException(nameof(@operator));
        }

        /// <inheritdoc />
        public override string FieldName => "Timestamp";

        /// <inheritdoc />
        public override string Operator { get; }

        /// <inheritdoc />
        public override bool IsValidValue(object value)
        {
            if (value == null) return false;

            // Support DateTime and DateTimeOffset objects directly
            if (value is DateTime || value is DateTimeOffset) return true;

            // Support string representations of dates
            if (value is string str)
            {
                return DateTime.TryParse(str, out _) || 
                       DateTime.TryParseExact(str, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
                       DateTime.TryParseExact(str, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
            }

            // Support arrays for Between operator
            if (Operator.Equals("between", StringComparison.OrdinalIgnoreCase) && value is object[] array)
            {
                return array.Length == 2 && IsValidValue(array[0]) && IsValidValue(array[1]);
            }

            return false;
        }

        /// <inheritdoc />
        public override double EstimateSelectivity(object value)
        {
            return Operator.ToLowerInvariant() switch
            {
                "equals" => 0.05,          // Exact timestamp matches are very selective
                "notequals" => 0.95,       // Not equals matches most timestamps
                "greaterthan" => 0.5,      // Time ranges vary widely
                "lessthan" => 0.5,
                "greaterthanorequal" => 0.5,
                "lessthanorequal" => 0.5,
                "between" => 0.2,          // Date ranges are typically selective
                _ => 0.3                   // Default for timestamp operations
            };
        }

        /// <inheritdoc />
        protected override bool Matches(RabbitMqLogEntry item, object value)
        {
            if (item?.Timestamp == null) return false;

            var itemTimestamp = item.Timestamp.Value;

            return Operator.ToLowerInvariant() switch
            {
                "equals" => MatchesEquals(itemTimestamp, value),
                "notequals" => !MatchesEquals(itemTimestamp, value),
                "greaterthan" => MatchesGreaterThan(itemTimestamp, value),
                "lessthan" => MatchesLessThan(itemTimestamp, value),
                "greaterthanorequal" => MatchesGreaterThanOrEqual(itemTimestamp, value),
                "lessthanorequal" => MatchesLessThanOrEqual(itemTimestamp, value),
                "between" => MatchesBetween(itemTimestamp, value),
                _ => false
            };
        }

        private bool MatchesEquals(DateTimeOffset itemTimestamp, object value)
        {
            var targetTimestamp = ConvertToDateTimeOffset(value);
            return targetTimestamp.HasValue && itemTimestamp == targetTimestamp.Value;
        }

        private bool MatchesGreaterThan(DateTimeOffset itemTimestamp, object value)
        {
            var targetTimestamp = ConvertToDateTimeOffset(value);
            return targetTimestamp.HasValue && itemTimestamp > targetTimestamp.Value;
        }

        private bool MatchesLessThan(DateTimeOffset itemTimestamp, object value)
        {
            var targetTimestamp = ConvertToDateTimeOffset(value);
            return targetTimestamp.HasValue && itemTimestamp < targetTimestamp.Value;
        }

        private bool MatchesGreaterThanOrEqual(DateTimeOffset itemTimestamp, object value)
        {
            var targetTimestamp = ConvertToDateTimeOffset(value);
            return targetTimestamp.HasValue && itemTimestamp >= targetTimestamp.Value;
        }

        private bool MatchesLessThanOrEqual(DateTimeOffset itemTimestamp, object value)
        {
            var targetTimestamp = ConvertToDateTimeOffset(value);
            return targetTimestamp.HasValue && itemTimestamp <= targetTimestamp.Value;
        }

        private bool MatchesBetween(DateTimeOffset itemTimestamp, object value)
        {
            if (value is not object[] array || array.Length != 2)
                return false;

            var startTimestamp = ConvertToDateTimeOffset(array[0]);
            var endTimestamp = ConvertToDateTimeOffset(array[1]);

            return startTimestamp.HasValue && endTimestamp.HasValue &&
                   itemTimestamp >= startTimestamp.Value && itemTimestamp <= endTimestamp.Value;
        }

        private DateTimeOffset? ConvertToDateTimeOffset(object value)
        {
            try
            {
                if (value is DateTimeOffset dto) return dto;
                if (value is DateTime dt) return new DateTimeOffset(dt);
                if (value is string str)
                {
                    if (DateTimeOffset.TryParse(str, out var parsed)) return parsed;
                    if (DateTime.TryParse(str, out var dtParsed)) return new DateTimeOffset(dtParsed);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to convert value '{Value}' to DateTimeOffset", value);
                return null;
            }
        }
    }
} 
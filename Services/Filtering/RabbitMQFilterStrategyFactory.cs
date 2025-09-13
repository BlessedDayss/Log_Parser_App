using System;
using System.Collections.Generic;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Log_Parser_App.Services.Filtering.Interfaces;
using Log_Parser_App.Services.Filtering.Strategies;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.Filtering;

public class RabbitMQFilterStrategyFactory : IFilterStrategyFactory<RabbitMqLogEntry>
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<string, HashSet<string>> _fieldOperators;

    public RabbitMQFilterStrategyFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

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

        public IFilterStrategy<RabbitMqLogEntry> CreateStrategy(string fieldName, string operatorName)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
                throw new ArgumentException("Field name cannot be null or empty", nameof(fieldName));
            if (string.IsNullOrWhiteSpace(operatorName))
                throw new ArgumentException("Operator name cannot be null or empty", nameof(operatorName));

            return fieldName.ToLowerInvariant() switch
            {
                "timestamp" => new TimestampFilterStrategy(operatorName, _loggerFactory.CreateLogger<TimestampFilterStrategy>()),
                "level" => new LevelFilterStrategy(operatorName, _loggerFactory.CreateLogger<LevelFilterStrategy>()),
                "message" => new MessageFilterStrategy(operatorName, _loggerFactory.CreateLogger<MessageFilterStrategy>()),
                "node" => new NodeFilterStrategy(operatorName, _loggerFactory.CreateLogger<NodeFilterStrategy>()),
                "processuid" => new ProcessUIDFilterStrategy(operatorName, _loggerFactory.CreateLogger<ProcessUIDFilterStrategy>()),
                "username" => new UsernameFilterStrategy(operatorName, _loggerFactory.CreateLogger<UsernameFilterStrategy>()),
                _ => throw new ArgumentException($"Unsupported field: {fieldName}", nameof(fieldName))
            };
        }

    public bool IsFieldSupported(string fieldName)
    {
        return !string.IsNullOrWhiteSpace(fieldName) &&
               _fieldOperators.ContainsKey(fieldName);
    }

    public bool IsOperatorSupported(string fieldName, string operatorName)
    {
        if (string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(operatorName))
            return false;

        return _fieldOperators.TryGetValue(fieldName, out var operators) &&
               operators.Contains(operatorName);
    }
}

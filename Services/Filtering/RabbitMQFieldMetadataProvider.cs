using System;
using System.Collections.Generic;
using System.Linq;
using Log_Parser_App.Services.Filtering.Interfaces;

namespace Log_Parser_App.Services.Filtering;

public class RabbitMQFieldMetadataProvider : IFieldMetadataProvider
{
    private readonly HashSet<string> _availableFields;
    private readonly Dictionary<string, HashSet<string>> _fieldOperators;

    public RabbitMQFieldMetadataProvider()
    {
        _availableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Timestamp", "Level", "Message", "Node", "ProcessUID", "Username"
        };

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

    public IEnumerable<string> GetAvailableFields()
    {
        return _availableFields.ToList();
    }

    public IEnumerable<string> GetAvailableOperators(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            return Enumerable.Empty<string>();

        return _fieldOperators.TryGetValue(fieldName, out var operators)
            ? operators.ToList()
            : Enumerable.Empty<string>();
    }

    public bool IsFieldSupported(string fieldName)
    {
        return !string.IsNullOrWhiteSpace(fieldName) &&
               _availableFields.Contains(fieldName);
    }

    public bool IsOperatorSupported(string fieldName, string operatorName)
    {
        if (string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(operatorName))
            return false;

        return _fieldOperators.TryGetValue(fieldName, out var operators) &&
               operators.Contains(operatorName);
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services;

/// <summary>
/// Service responsible for log filtering operations
/// Extracted from MainViewModel to follow Single Responsibility Principle
/// </summary>
public class FilterService : IFilterService
{
    private readonly ILogger<FilterService> _logger;
    private readonly ObservableCollection<FilterCriterion> _currentFilters;

    #region Events

    // Events removed as they were not being used

    #endregion

    // Master list of available fields for filtering
    private readonly Dictionary<LogFormatType, List<string>> _availableFieldsByLogType = new()
    {
        { LogFormatType.Standard, new List<string> { "Timestamp", "Level", "Message", "Source", "RawData", "CorrelationId", "ErrorType" } },
        { LogFormatType.IIS, new List<string> { "Timestamp", "Level", "Message", "IPAddress", "Method", "URI", "StatusCode", "BytesSent", "TimeTaken" } },
        { LogFormatType.RabbitMQ, new List<string> { "Timestamp", "Level", "Message", "Node", "Username", "ProcessUID" } }
    };

    // Operators available for each field type
    private readonly Dictionary<Type, List<string>> _operatorsByType = new()
    {
        { typeof(DateTime), new List<string> { "Equals", "Before", "After", "Between" } },
        { typeof(string), new List<string> { "Contains", "Equals", "StartsWith", "EndsWith", "Regex", "Not Contains" } },
        { typeof(int), new List<string> { "Equals", "GreaterThan", "LessThan", "Between" } },
        { typeof(double), new List<string> { "Equals", "GreaterThan", "LessThan", "Between" } }
    };

    public FilterService(ILogger<FilterService> logger)
    {
        _logger = logger;
        _currentFilters = new ObservableCollection<FilterCriterion>();
    }

    /// <summary>
    /// Apply filters to log entries collection
    /// </summary>
    public async Task<IEnumerable<LogEntry>> ApplyFiltersAsync(IEnumerable<LogEntry> logEntries, IEnumerable<FilterCriterion> filterCriteria)
    {
        return await Task.Run(() =>
        {
            try
            {
                _logger.LogDebug($"Applying {filterCriteria.Count()} filter criteria to {logEntries.Count()} log entries");

                if (!filterCriteria.Any())
                {
                    _logger.LogDebug("No filter criteria defined, returning all entries");
                    return logEntries;
                }

                IEnumerable<LogEntry> result = logEntries;

                foreach (var criterion in filterCriteria.Where(c => c.IsEnabled))
                {
                    result = ApplySingleFilterCriterion(result, criterion);
                    _logger.LogDebug($"Applied filter '{criterion.Field} {criterion.Operator} {criterion.Value}', remaining entries: {result.Count()}");
                }

                var filteredEntries = result.ToList();
                _logger.LogInformation($"Filtering complete: {logEntries.Count()} -> {filteredEntries.Count} entries");

                return filteredEntries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying filters");
                return logEntries; // Return original entries on error
            }
        });
    }

    /// <summary>
    /// Apply IIS-specific filters to log entries
    /// </summary>
    public async Task<IEnumerable<LogEntry>> ApplyIISFiltersAsync(IEnumerable<LogEntry> logEntries, IEnumerable<IISFilterCriterion> iisFilterCriteria)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Convert IIS filter criteria to standard filter criteria
                var standardCriteria = iisFilterCriteria.Select(ConvertIISToStandardCriterion);
                return ApplyFiltersAsync(logEntries, standardCriteria).Result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying IIS filters");
                return logEntries;
            }
        });
    }

    /// <summary>
    /// Validate filter criteria for correctness
    /// </summary>
    public bool ValidateFilterCriterion(FilterCriterion criterion)
    {
        try
        {
            if (criterion == null)
                return false;

            if (string.IsNullOrEmpty(criterion.Field) || string.IsNullOrEmpty(criterion.Operator))
                return false;

            // Check if operator is valid for the field type
            var fieldType = GetFieldType(criterion.Field);
            var availableOperators = GetAvailableOperators(fieldType);

            return availableOperators.Contains(criterion.Operator);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Error validating filter criterion: {criterion?.Field} {criterion?.Operator} {criterion?.Value}");
            return false;
        }
    }

    /// <summary>
    /// Validate IIS filter criteria for correctness
    /// </summary>
    public bool ValidateIISFilterCriterion(IISFilterCriterion criterion)
    {
        try
        {
            if (criterion == null)
                return false;

            // Convert to standard criterion and validate
            var standardCriterion = ConvertIISToStandardCriterion(criterion);
            return ValidateFilterCriterion(standardCriterion);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating IIS filter criterion");
            return false;
        }
    }

    /// <summary>
    /// Get available filter fields for specific log type
    /// </summary>
    public IEnumerable<string> GetAvailableFilterFields(LogFormatType logType)
    {
        return _availableFieldsByLogType.ContainsKey(logType) 
            ? _availableFieldsByLogType[logType] 
            : _availableFieldsByLogType[LogFormatType.Standard];
    }

    /// <summary>
    /// Get available filter operators for field type
    /// </summary>
    public IEnumerable<string> GetAvailableOperators(Type fieldType)
    {
        return _operatorsByType.ContainsKey(fieldType) 
            ? _operatorsByType[fieldType] 
            : _operatorsByType[typeof(string)];
    }

    /// <summary>
    /// Clear all applied filters
    /// </summary>
    public void ClearFilters()
    {
        try
        {
            _logger.LogDebug("Clearing all filters");
            _currentFilters.Clear();
            _logger.LogInformation("All filters have been cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing filters");
            throw;
        }
    }

    /// <summary>
    /// Export current filter configuration
    /// </summary>
    public string ExportFilterConfiguration()
    {
        try
        {
            // Simple JSON-like export
            var filters = _currentFilters.Select(f => new
            {
                Field = f.Field,
                Operator = f.Operator,
                Value = f.Value,
                IsEnabled = f.IsEnabled
            });

            return System.Text.Json.JsonSerializer.Serialize(filters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting filter configuration");
            return string.Empty;
        }
    }

    /// <summary>
    /// Import filter configuration from serialized data
    /// </summary>
    public bool ImportFilterConfiguration(string configuration)
    {
        try
        {
            if (string.IsNullOrEmpty(configuration))
                return false;

            var filters = System.Text.Json.JsonSerializer.Deserialize<FilterCriterion[]>(configuration);
            if (filters == null)
                return false;

            _currentFilters.Clear();
            foreach (var filter in filters)
            {
                _currentFilters.Add(filter);
            }

            _logger.LogInformation($"Imported {filters.Length} filter criteria");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing filter configuration");
            return false;
        }
    }

    /// <summary>
    /// Apply filters specifically to RabbitMQ log entries
    /// </summary>
    public async Task<IEnumerable<RabbitMqLogEntry>> ApplyRabbitMQFiltersAsync(IEnumerable<RabbitMqLogEntry> rabbitMqEntries, IEnumerable<FilterCriterion> filterCriteria)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (rabbitMqEntries == null)
                    return Enumerable.Empty<RabbitMqLogEntry>();

                var criteria = filterCriteria?.Where(c => c.IsEnabled).ToList() ?? new List<FilterCriterion>();
                if (!criteria.Any())
                    return rabbitMqEntries;

                var filteredEntries = rabbitMqEntries;

                foreach (var criterion in criteria)
                {
                    filteredEntries = ApplySingleRabbitMQFilterCriterion(filteredEntries, criterion);
                }

                _logger.LogDebug("Applied {FilterCount} RabbitMQ filters to {EntryCount} entries, result: {ResultCount} entries",
                    criteria.Count, rabbitMqEntries.Count(), filteredEntries.Count());

                return filteredEntries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying RabbitMQ filters");
                return rabbitMqEntries; // Return unfiltered entries on error
            }
        });
    }

    #region Private Helper Methods

    /// <summary>
    /// Apply a single filter criterion to log entries
    /// </summary>
    private IEnumerable<LogEntry> ApplySingleFilterCriterion(IEnumerable<LogEntry> entries, FilterCriterion criterion)
    {
        try
        {
            if (string.IsNullOrEmpty(criterion.Field) || string.IsNullOrEmpty(criterion.Operator))
                return entries;

            return criterion.Field.ToLower() switch
            {
                "timestamp" => ApplyTimestampFilter(entries, criterion),
                "level" => ApplyLevelFilter(entries, criterion),
                "message" => ApplyMessageFilter(entries, criterion),
                "source" => ApplySourceFilter(entries, criterion),
                "rawdata" => ApplyRawDataFilter(entries, criterion),
                "correlationid" => ApplyCorrelationIdFilter(entries, criterion),
                "errortype" => ApplyErrorTypeFilter(entries, criterion),
                // IIS specific fields
                "ipaddress" => ApplyIPAddressFilter(entries, criterion),
                "method" => ApplyMethodFilter(entries, criterion),
                "uri" => ApplyURIFilter(entries, criterion),
                "statuscode" => ApplyStatusCodeFilter(entries, criterion),
                // RabbitMQ specific fields
                "node" => ApplyNodeFilter(entries, criterion),
                "username" => ApplyUsernameFilter(entries, criterion),
                "processuid" => ApplyProcessUIDFilter(entries, criterion),
                _ => entries
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error applying filter criterion: {criterion.Field} {criterion.Operator} {criterion.Value}");
            return entries; // Return unfiltered entries on error
        }
    }

    private IEnumerable<LogEntry> ApplyTimestampFilter(IEnumerable<LogEntry> entries, FilterCriterion criterion)
    {
        if (!DateTime.TryParse(criterion.Value, out var filterDate))
            return entries;

        return criterion.Operator switch
        {
            "Equals" => entries.Where(e => e.Timestamp.Date == filterDate.Date),
            "Before" => entries.Where(e => e.Timestamp < filterDate),
            "After" => entries.Where(e => e.Timestamp > filterDate),
            _ => entries
        };
    }

    private IEnumerable<LogEntry> ApplyLevelFilter(IEnumerable<LogEntry> entries, FilterCriterion criterion)
    {
        return criterion.Operator switch
        {
            "Equals" => entries.Where(e => string.Equals(e.Level, criterion.Value, StringComparison.OrdinalIgnoreCase)),
            "Not Equals" => entries.Where(e => !string.Equals(e.Level, criterion.Value, StringComparison.OrdinalIgnoreCase)),
            _ => entries
        };
    }

    private IEnumerable<LogEntry> ApplyMessageFilter(IEnumerable<LogEntry> entries, FilterCriterion criterion)
    {
        var value = criterion.Value ?? string.Empty;
        return criterion.Operator switch
        {
            "Contains" => entries.Where(e => e.Message.Contains(value, StringComparison.OrdinalIgnoreCase)),
            "Not Contains" => entries.Where(e => !e.Message.Contains(value, StringComparison.OrdinalIgnoreCase)),
            "Equals" => entries.Where(e => string.Equals(e.Message, value, StringComparison.OrdinalIgnoreCase)),
            "StartsWith" => entries.Where(e => e.Message.StartsWith(value, StringComparison.OrdinalIgnoreCase)),
            "EndsWith" => entries.Where(e => e.Message.EndsWith(value, StringComparison.OrdinalIgnoreCase)),
            "Regex" => ApplyRegexFilter(entries, e => e.Message, value),
            _ => entries
        };
    }

    private IEnumerable<LogEntry> ApplySourceFilter(IEnumerable<LogEntry> entries, FilterCriterion criterion)
    {
        var value = criterion.Value ?? string.Empty;
        return criterion.Operator switch
        {
            "Contains" => entries.Where(e => (e.Source ?? "").Contains(value, StringComparison.OrdinalIgnoreCase)),
            "Not Contains" => entries.Where(e => !(e.Source ?? "").Contains(value, StringComparison.OrdinalIgnoreCase)),
            "Equals" => entries.Where(e => string.Equals(e.Source, value, StringComparison.OrdinalIgnoreCase)),
            "StartsWith" => entries.Where(e => (e.Source ?? "").StartsWith(value, StringComparison.OrdinalIgnoreCase)),
            "EndsWith" => entries.Where(e => (e.Source ?? "").EndsWith(value, StringComparison.OrdinalIgnoreCase)),
            _ => entries
        };
    }

    private IEnumerable<LogEntry> ApplyRawDataFilter(IEnumerable<LogEntry> entries, FilterCriterion criterion)
    {
        var value = criterion.Value ?? string.Empty;
        return criterion.Operator switch
        {
            "Contains" => entries.Where(e => (e.RawData ?? "").Contains(value, StringComparison.OrdinalIgnoreCase)),
            "Not Contains" => entries.Where(e => !(e.RawData ?? "").Contains(value, StringComparison.OrdinalIgnoreCase)),
            "Equals" => entries.Where(e => string.Equals(e.RawData, value, StringComparison.OrdinalIgnoreCase)),
            "StartsWith" => entries.Where(e => (e.RawData ?? "").StartsWith(value, StringComparison.OrdinalIgnoreCase)),
            "EndsWith" => entries.Where(e => (e.RawData ?? "").EndsWith(value, StringComparison.OrdinalIgnoreCase)),
            "Regex" => ApplyRegexFilter(entries, e => e.RawData ?? "", value),
            _ => entries
        };
    }

    private IEnumerable<LogEntry> ApplyCorrelationIdFilter(IEnumerable<LogEntry> entries, FilterCriterion criterion)
    {
        var value = criterion.Value ?? string.Empty;
        return criterion.Operator switch
        {
            "Equals" => entries.Where(e => string.Equals(e.CorrelationId, value, StringComparison.OrdinalIgnoreCase)),
            "Not Equals" => entries.Where(e => !string.Equals(e.CorrelationId, value, StringComparison.OrdinalIgnoreCase)),
            "Contains" => entries.Where(e => (e.CorrelationId ?? "").Contains(value, StringComparison.OrdinalIgnoreCase)),
            _ => entries
        };
    }

    private IEnumerable<LogEntry> ApplyErrorTypeFilter(IEnumerable<LogEntry> entries, FilterCriterion criterion)
    {
        var value = criterion.Value ?? string.Empty;
        return criterion.Operator switch
        {
            "Equals" => entries.Where(e => string.Equals(e.ErrorType, value, StringComparison.OrdinalIgnoreCase)),
            "Not Equals" => entries.Where(e => !string.Equals(e.ErrorType, value, StringComparison.OrdinalIgnoreCase)),
            "Contains" => entries.Where(e => (e.ErrorType ?? "").Contains(value, StringComparison.OrdinalIgnoreCase)),
            _ => entries
        };
    }

    // IIS-specific filter methods
    private IEnumerable<LogEntry> ApplyIPAddressFilter(IEnumerable<LogEntry> entries, FilterCriterion criterion)
    {
        // Assuming IIS entries have IP address in Source field
        return ApplySourceFilter(entries, criterion);
    }

    private IEnumerable<LogEntry> ApplyMethodFilter(IEnumerable<LogEntry> entries, FilterCriterion criterion)
    {
        // For IIS logs, method might be in the message or a dedicated field
        return ApplyMessageFilter(entries, criterion);
    }

    private IEnumerable<LogEntry> ApplyURIFilter(IEnumerable<LogEntry> entries, FilterCriterion criterion)
    {
        return ApplyMessageFilter(entries, criterion);
    }

    private IEnumerable<LogEntry> ApplyStatusCodeFilter(IEnumerable<LogEntry> entries, FilterCriterion criterion)
    {
        return ApplyMessageFilter(entries, criterion);
    }

    // RabbitMQ-specific filter methods
    private IEnumerable<LogEntry> ApplyNodeFilter(IEnumerable<LogEntry> entries, FilterCriterion criterion)
    {
        return ApplySourceFilter(entries, criterion);
    }

    private IEnumerable<LogEntry> ApplyUsernameFilter(IEnumerable<LogEntry> entries, FilterCriterion criterion)
    {
        // Username filtering based on RawData that contains RabbitMQ JSON with parsed fields
        var value = criterion.Value ?? string.Empty;
        return criterion.Operator switch
        {
            "Contains" => entries.Where(e => (e.RawData ?? "").Contains($"\"UserName\":\"{value}\"", StringComparison.OrdinalIgnoreCase) ||
                                           (e.RawData ?? "").Contains($"\"user\":\"{value}\"", StringComparison.OrdinalIgnoreCase)),
            "Equals" => entries.Where(e => (e.RawData ?? "").Contains($"\"UserName\":\"{value}\"", StringComparison.OrdinalIgnoreCase) ||
                                         (e.RawData ?? "").Contains($"\"user\":\"{value}\"", StringComparison.OrdinalIgnoreCase)),
            _ => entries
        };
    }

    private IEnumerable<LogEntry> ApplyProcessUIDFilter(IEnumerable<LogEntry> entries, FilterCriterion criterion)
    {
        // ProcessUID filtering based on RawData that contains RabbitMQ JSON with parsed fields  
        var value = criterion.Value ?? string.Empty;
        return criterion.Operator switch
        {
            "Contains" => entries.Where(e => (e.RawData ?? "").Contains($"\"ProcessUID\":\"{value}\"", StringComparison.OrdinalIgnoreCase) ||
                                           (e.RawData ?? "").Contains($"\"processUId\":\"{value}\"", StringComparison.OrdinalIgnoreCase)),
            "Equals" => entries.Where(e => (e.RawData ?? "").Contains($"\"ProcessUID\":\"{value}\"", StringComparison.OrdinalIgnoreCase) ||
                                         (e.RawData ?? "").Contains($"\"processUId\":\"{value}\"", StringComparison.OrdinalIgnoreCase)),
            _ => entries
        };
    }

    private IEnumerable<LogEntry> ApplyRegexFilter(IEnumerable<LogEntry> entries, Func<LogEntry, string> fieldSelector, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return entries;

        try
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            return entries.Where(e => regex.IsMatch(fieldSelector(e)));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, $"Invalid regex pattern: {pattern}");
            return entries; // Return unfiltered entries if regex is invalid
        }
    }

    private FilterCriterion ConvertIISToStandardCriterion(IISFilterCriterion iisFilter)
    {
        var criterion = new FilterCriterion();
        criterion.Field = iisFilter.Field;
        criterion.Operator = iisFilter.Operator;
        criterion.Value = iisFilter.Value;
        return criterion;
    }

    private Type GetFieldType(string fieldName)
    {
        return fieldName.ToLower() switch
        {
            "timestamp" => typeof(DateTime),
            "statuscode" => typeof(int),
            "bytessent" => typeof(int),
            "timetaken" => typeof(double),
            "pid" => typeof(int),
            _ => typeof(string)
        };
    }

    /// <summary>
    /// Apply a single filter criterion to RabbitMQ log entries
    /// </summary>
    private IEnumerable<RabbitMqLogEntry> ApplySingleRabbitMQFilterCriterion(IEnumerable<RabbitMqLogEntry> entries, FilterCriterion criterion)
    {
        try
        {
            if (string.IsNullOrEmpty(criterion.Field) || string.IsNullOrEmpty(criterion.Operator))
                return entries;

            return criterion.Field.ToLower() switch
            {
                "timestamp" => ApplyRabbitMQTimestampFilter(entries, criterion),
                "level" => ApplyRabbitMQLevelFilter(entries, criterion),
                "message" => ApplyRabbitMQMessageFilter(entries, criterion),
                "node" => ApplyRabbitMQNodeFilter(entries, criterion),
                "username" => ApplyRabbitMQUsernameFilter(entries, criterion),
                "processuid" => ApplyRabbitMQProcessUIDFilter(entries, criterion),
                _ => entries
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error applying RabbitMQ filter criterion: {criterion.Field} {criterion.Operator} {criterion.Value}");
            return entries; // Return unfiltered entries on error
        }
    }

    // RabbitMQ-specific filter methods
    private IEnumerable<RabbitMqLogEntry> ApplyRabbitMQTimestampFilter(IEnumerable<RabbitMqLogEntry> entries, FilterCriterion criterion)
    {
        if (!DateTime.TryParse(criterion.Value, out var filterDate))
            return entries;

        return criterion.Operator switch
        {
            "Equals" => entries.Where(e => e.EffectiveTimestamp.HasValue && e.EffectiveTimestamp.Value.Date == filterDate.Date),
            "Before" => entries.Where(e => e.EffectiveTimestamp.HasValue && e.EffectiveTimestamp.Value < filterDate),
            "After" => entries.Where(e => e.EffectiveTimestamp.HasValue && e.EffectiveTimestamp.Value > filterDate),
            _ => entries
        };
    }

    private IEnumerable<RabbitMqLogEntry> ApplyRabbitMQLevelFilter(IEnumerable<RabbitMqLogEntry> entries, FilterCriterion criterion)
    {
        return criterion.Operator switch
        {
            "Equals" => entries.Where(e => string.Equals(e.EffectiveLevel, criterion.Value, StringComparison.OrdinalIgnoreCase)),
            "Not Equals" => entries.Where(e => !string.Equals(e.EffectiveLevel, criterion.Value, StringComparison.OrdinalIgnoreCase)),
            _ => entries
        };
    }

    private IEnumerable<RabbitMqLogEntry> ApplyRabbitMQMessageFilter(IEnumerable<RabbitMqLogEntry> entries, FilterCriterion criterion)
    {
        var value = criterion.Value ?? string.Empty;
        return criterion.Operator switch
        {
            "Contains" => entries.Where(e => (e.EffectiveMessage ?? "").Contains(value, StringComparison.OrdinalIgnoreCase)),
            "Not Contains" => entries.Where(e => !(e.EffectiveMessage ?? "").Contains(value, StringComparison.OrdinalIgnoreCase)),
            "Equals" => entries.Where(e => string.Equals(e.EffectiveMessage, value, StringComparison.OrdinalIgnoreCase)),
            "StartsWith" => entries.Where(e => (e.EffectiveMessage ?? "").StartsWith(value, StringComparison.OrdinalIgnoreCase)),
            "EndsWith" => entries.Where(e => (e.EffectiveMessage ?? "").EndsWith(value, StringComparison.OrdinalIgnoreCase)),
            _ => entries
        };
    }

    private IEnumerable<RabbitMqLogEntry> ApplyRabbitMQNodeFilter(IEnumerable<RabbitMqLogEntry> entries, FilterCriterion criterion)
    {
        var value = criterion.Value ?? string.Empty;
        return criterion.Operator switch
        {
            "Contains" => entries.Where(e => (e.EffectiveNode ?? "").Contains(value, StringComparison.OrdinalIgnoreCase)),
            "Equals" => entries.Where(e => string.Equals(e.EffectiveNode, value, StringComparison.OrdinalIgnoreCase)),
            _ => entries
        };
    }

    private IEnumerable<RabbitMqLogEntry> ApplyRabbitMQUsernameFilter(IEnumerable<RabbitMqLogEntry> entries, FilterCriterion criterion)
    {
        var value = criterion.Value ?? string.Empty;
        return criterion.Operator switch
        {
            "Contains" => entries.Where(e => (e.EffectiveUserName ?? "").Contains(value, StringComparison.OrdinalIgnoreCase)),
            "Equals" => entries.Where(e => string.Equals(e.EffectiveUserName, value, StringComparison.OrdinalIgnoreCase)),
            "Not Equals" => entries.Where(e => !string.Equals(e.EffectiveUserName, value, StringComparison.OrdinalIgnoreCase)),
            _ => entries
        };
    }

    private IEnumerable<RabbitMqLogEntry> ApplyRabbitMQProcessUIDFilter(IEnumerable<RabbitMqLogEntry> entries, FilterCriterion criterion)
    {
        var value = criterion.Value ?? string.Empty;
        return criterion.Operator switch
        {
            "Contains" => entries.Where(e => (e.EffectiveProcessUID ?? "").Contains(value, StringComparison.OrdinalIgnoreCase)),
            "Equals" => entries.Where(e => string.Equals(e.EffectiveProcessUID, value, StringComparison.OrdinalIgnoreCase)),
            "Not Equals" => entries.Where(e => !string.Equals(e.EffectiveProcessUID, value, StringComparison.OrdinalIgnoreCase)),
            _ => entries
        };
    }

    #endregion
}

#region Event Args Classes

// Event args class moved to interface file

#endregion


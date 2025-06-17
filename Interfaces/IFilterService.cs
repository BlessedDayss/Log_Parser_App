using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Interfaces;

/// <summary>
/// Service interface for filter management following SRP principle.
/// Handles filter criteria, application, and log entry filtering operations.
/// </summary>
public interface IFilterService
{
    /// <summary>
    /// Apply filters to log entries collection
    /// </summary>
    /// <param name="logEntries">Source log entries to filter</param>
    /// <param name="filterCriteria">Collection of filter criteria to apply</param>
    /// <returns>Filtered log entries matching the criteria</returns>
    Task<IEnumerable<LogEntry>> ApplyFiltersAsync(IEnumerable<LogEntry> logEntries, IEnumerable<FilterCriterion> filterCriteria);
    
    /// <summary>
    /// Apply IIS-specific filters to log entries
    /// </summary>
    /// <param name="logEntries">Source log entries to filter</param>
    /// <param name="iisFilterCriteria">Collection of IIS-specific filter criteria</param>
    /// <returns>Filtered log entries matching the IIS criteria</returns>
    Task<IEnumerable<LogEntry>> ApplyIISFiltersAsync(IEnumerable<LogEntry> logEntries, IEnumerable<IISFilterCriterion> iisFilterCriteria);
    
    /// <summary>
    /// Validate filter criteria for correctness
    /// </summary>
    /// <param name="criterion">Filter criterion to validate</param>
    /// <returns>True if criterion is valid</returns>
    bool ValidateFilterCriterion(FilterCriterion criterion);
    
    /// <summary>
    /// Validate IIS filter criteria for correctness
    /// </summary>
    /// <param name="criterion">IIS filter criterion to validate</param>
    /// <returns>True if criterion is valid</returns>
    bool ValidateIISFilterCriterion(IISFilterCriterion criterion);
    
    /// <summary>
    /// Get available filter fields for specific log type
    /// </summary>
    /// <param name="logType">Log format type</param>
    /// <returns>Collection of available filter field names</returns>
    IEnumerable<string> GetAvailableFilterFields(LogFormatType logType);
    
    /// <summary>
    /// Get available filter operators for field type
    /// </summary>
    /// <param name="fieldType">Type of the field being filtered</param>
    /// <returns>Collection of available operators</returns>
    IEnumerable<string> GetAvailableOperators(Type fieldType);
    
    /// <summary>
    /// Clear all applied filters
    /// </summary>
    void ClearFilters();
    
    /// <summary>
    /// Export current filter configuration
    /// </summary>
    /// <returns>Serialized filter configuration</returns>
    string ExportFilterConfiguration();
    
    /// <summary>
    /// Import filter configuration from serialized data
    /// </summary>
    /// <param name="configuration">Serialized filter configuration</param>
    /// <returns>True if import was successful</returns>
    bool ImportFilterConfiguration(string configuration);
    
    // Events removed as they were not being used
} 

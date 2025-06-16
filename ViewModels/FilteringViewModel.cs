using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Log_Parser_App.Models;
using Log_Parser_App.Services;
using Log_Parser_App.Interfaces;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.ViewModels
{
    /// <summary>
    /// ViewModel responsible for filtering operations
    /// Follows Single Responsibility Principle
    /// </summary>
    public partial class FilteringViewModel : ViewModelBase
    {
        #region Dependencies

        private readonly IFilterService _filterService;
        private readonly ILogger<FilteringViewModel> _logger;

        #endregion

        #region Properties

        public ObservableCollection<FilterCriterion> FilterCriteria { get; } = new();

        [ObservableProperty]
        private bool _isFilteringEnabled = true;

        [ObservableProperty]
        private int _totalEntriesCount;

        [ObservableProperty]
        private int _filteredEntriesCount;

        [ObservableProperty]
        private string _filterSummary = "No filters applied";

        // Master list of available fields for filtering
        private readonly List<string> _masterAvailableFields = new List<string> 
        { 
            "Timestamp", "Level", "Message", "Source", "RawData", "CorrelationId", "ErrorType" 
        };

        public Dictionary<string, List<string>> OperatorsByFieldType { get; } = new()
        {
            {"DateTime", new List<string> {"Equals", "Before", "After", "Between"}},
            {"String", new List<string> {"Contains", "Equals", "StartsWith", "EndsWith", "Regex", "Not Contains"}},
            {"Integer", new List<string> {"Equals", "GreaterThan", "LessThan", "Between"}},
            {"Double", new List<string> {"Equals", "GreaterThan", "LessThan", "Between"}}
        };

        public Dictionary<string, ObservableCollection<string>> AvailableValuesByField { get; } = new()
        {
            {"Level", new ObservableCollection<string>()},
            {"Source", new ObservableCollection<string>()},
            {"ErrorType", new ObservableCollection<string>()}
        };

        #endregion

        #region Events

        /// <summary>
        /// Event fired when filters are applied
        /// </summary>
        public event EventHandler<FiltersAppliedEventArgs>? FiltersApplied;

        /// <summary>
        /// Event fired when filters are reset
        /// </summary>
        public event EventHandler<FiltersResetEventArgs>? FiltersReset;

        #endregion

        #region Constructor

        public FilteringViewModel(
            IFilterService filterService,
            ILogger<FilteringViewModel> logger)
        {
            _filterService = filterService;
            _logger = logger;

            // Subscribe to filter service events
            // TODO: Fix event signature compatibility
            // _filterService.FiltersApplied += OnFiltersApplied;
            // _filterService.FiltersReset += OnFiltersReset;
        }

        #endregion

        #region Commands

        [RelayCommand]
        private Task ApplyFilters()
        {
            try
            {
                _logger.LogInformation("Applying {Count} filter criteria", FilterCriteria.Count);
                
                var validCriteria = FilterCriteria.Where(c => c.IsEnabled && ValidateFilterCriterion(c)).ToList();
                
                if (!validCriteria.Any())
                {
                    _logger.LogInformation("No valid filter criteria found");
                    UpdateFilterSummary(0, 0);
                    return Task.CompletedTask;
                }

                // Notify subscribers that filters are being applied
                // Note: This needs to be called with actual filtered entries from the calling code
                // OnFiltersApplied(new FiltersAppliedEventArgs(validCriteria, filteredEntries, originalEntries));

                _logger.LogInformation("Successfully applied {Count} filters", validCriteria.Count);
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying filters");
                return Task.FromException(ex);
            }
        }

        [RelayCommand]
        private Task ResetFilters()
        {
            try
            {
                _logger.LogInformation("Resetting all filters");
                
                FilterCriteria.Clear();
                _filterService.ClearFilters();
                
                UpdateFilterSummary(0, 0);
                
                OnFiltersReset(new FiltersResetEventArgs());
                
                _logger.LogInformation("Successfully reset all filters");
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting filters");
                return Task.FromException(ex);
            }
        }

        [RelayCommand]
        private void AddFilterCriterion()
        {
            try
            {
                var criterion = new FilterCriterion
                {
                    Field = _masterAvailableFields.FirstOrDefault() ?? "Level",
                    Operator = "Contains",
                    Value = "",
                    IsActive = true
                };

                FilterCriteria.Add(criterion);
                _logger.LogDebug("Added new filter criterion: {Field}", criterion.Field);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding filter criterion");
            }
        }

        [RelayCommand]
        private async Task RemoveFilterCriterion(FilterCriterion? criterion)
        {
            if (criterion == null) return;

            try
            {
                FilterCriteria.Remove(criterion);
                _logger.LogDebug("Removed filter criterion: {Field} {Operator} {Value}", 
                    criterion.Field, criterion.Operator, criterion.Value);
                
                // Reapply remaining filters
                await ApplyFilters();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing filter criterion");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Apply filters to a collection of log entries
        /// </summary>
        public async Task<IEnumerable<LogEntry>> ApplyFiltersToEntriesAsync(IEnumerable<LogEntry> logEntries)
        {
            try
            {
                var validCriteria = FilterCriteria.Where(c => c.IsEnabled).ToList();
                
                if (!validCriteria.Any())
                {
                    return logEntries;
                }

                var filteredEntries = await _filterService.ApplyFiltersAsync(logEntries, validCriteria);
                
                UpdateFilterSummary(logEntries.Count(), filteredEntries.Count());
                
                return filteredEntries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying filters to entries");
                return logEntries; // Return original entries on error
            }
        }

        /// <summary>
        /// Update available filter values based on current log entries
        /// </summary>
        public void UpdateAvailableFilterValues(IEnumerable<LogEntry> logEntries)
        {
            try
            {
                var entries = logEntries.ToList();
                
                // Update Level values
                var levels = entries.Where(e => !string.IsNullOrEmpty(e.Level))
                                   .Select(e => e.Level!)
                                   .Distinct()
                                   .OrderBy(l => l)
                                   .ToList();
                
                AvailableValuesByField["Level"].Clear();
                foreach (var level in levels)
                {
                    AvailableValuesByField["Level"].Add(level);
                }

                // Update Source values
                var sources = entries.Where(e => !string.IsNullOrEmpty(e.Source))
                                    .Select(e => e.Source!)
                                    .Distinct()
                                    .OrderBy(s => s)
                                    .Take(50) // Limit to prevent UI overload
                                    .ToList();
                
                AvailableValuesByField["Source"].Clear();
                foreach (var source in sources)
                {
                    AvailableValuesByField["Source"].Add(source);
                }

                // Update ErrorType values
                var errorTypes = entries.Where(e => !string.IsNullOrEmpty(e.ErrorType))
                                       .Select(e => e.ErrorType!)
                                       .Distinct()
                                       .OrderBy(et => et)
                                       .ToList();
                
                AvailableValuesByField["ErrorType"].Clear();
                foreach (var errorType in errorTypes)
                {
                    AvailableValuesByField["ErrorType"].Add(errorType);
                }

                _logger.LogDebug("Updated filter values: {Levels} levels, {Sources} sources, {ErrorTypes} error types",
                    levels.Count, sources.Count, errorTypes.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating available filter values");
            }
        }

        /// <summary>
        /// Validate a filter criterion
        /// </summary>
        public bool ValidateFilterCriterion(FilterCriterion criterion)
        {
            try
            {
                return _filterService.ValidateFilterCriterion(criterion);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error validating filter criterion");
                return false;
            }
        }

        /// <summary>
        /// Export current filter configuration
        /// </summary>
        public string ExportFilterConfiguration()
        {
            try
            {
                return _filterService.ExportFilterConfiguration();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting filter configuration");
                return string.Empty;
            }
        }

        /// <summary>
        /// Import filter configuration
        /// </summary>
        public bool ImportFilterConfiguration(string configuration)
        {
            try
            {
                var success = _filterService.ImportFilterConfiguration(configuration);
                
                if (success)
                {
                    // Refresh criteria from service
                    // Note: This would need to be implemented in the filter service
                    _logger.LogInformation("Successfully imported filter configuration");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing filter configuration");
                return false;
            }
        }

        #endregion

        #region Private Methods

        private void UpdateFilterSummary(int totalCount, int filteredCount)
        {
            TotalEntriesCount = totalCount;
            FilteredEntriesCount = filteredCount;
            
            if (FilterCriteria.Any(c => c.IsEnabled))
            {
                var activeFilters = FilterCriteria.Count(c => c.IsEnabled);
                FilterSummary = $"{activeFilters} filter(s) applied: {filteredCount:N0} of {totalCount:N0} entries shown";
            }
            else
            {
                FilterSummary = "No filters applied";
            }
        }

        #endregion

        #region Event Handlers

        private void OnFiltersApplied(FiltersAppliedEventArgs e)
        {
            FiltersApplied?.Invoke(this, e);
        }

        private void OnFiltersReset(FiltersResetEventArgs e)
        {
            FiltersReset?.Invoke(this, e);
        }

        #endregion
    }

    #region Event Args

    public class FiltersAppliedEventArgs : EventArgs
    {
        public IReadOnlyList<FilterCriterion> AppliedCriteria { get; }
        public IEnumerable<LogEntry> FilteredEntries { get; }
        public IEnumerable<LogEntry> OriginalEntries { get; }

        public FiltersAppliedEventArgs(IReadOnlyList<FilterCriterion> appliedCriteria, IEnumerable<LogEntry> filteredEntries, IEnumerable<LogEntry> originalEntries)
        {
            AppliedCriteria = appliedCriteria;
            FilteredEntries = filteredEntries;
            OriginalEntries = originalEntries;
        }
    }

    public class FiltersResetEventArgs : EventArgs
    {
    }

    #endregion
} 
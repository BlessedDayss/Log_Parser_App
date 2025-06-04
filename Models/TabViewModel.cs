using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using System; // Added for StringComparer
// Forward declaration for MainViewModel to avoid circular dependency if full type info is not needed here
// However, for a property, we'll need the actual type. Assuming MainViewModel is in ViewModels namespace.
// using Log_Parser_App.ViewModels; // Not needed if we don't store MainViewModel directly

namespace Log_Parser_App.Models
{
    public partial class TabViewModel : ObservableObject
    {
        private string _filePath;
        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        private string _title;
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private List<LogEntry> _logEntries;
        public List<LogEntry> LogEntries
        {
            get => _logEntries;
            set => SetProperty(ref _logEntries, value);
        }

        private List<IISLogEntry> _iisLogEntries;
        public List<IISLogEntry> IISLogEntries
        {
            get => _iisLogEntries;
            set
            {
                if (SetProperty(ref _iisLogEntries, value))
                {
                    // If IIS logs are loaded or changed, re-apply filters (which also populates FilteredIISLogEntries initially)
                    if (LogType == LogFormatType.IIS)
                    {
                        ApplyIISFiltersCommand.Execute(null);
                    }
                }
            }
        }

        public LogFormatType LogType { get; }

        // New direct properties for tab type checking
        public bool IsThisTabIIS => LogType == LogFormatType.IIS;
        public bool IsThisTabStandard => LogType == LogFormatType.Standard;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        // --- IIS Filtering Properties and Commands ---
        public ObservableCollection<IISFilterCriterion> IISFilterCriteria { get; }
        public ObservableCollection<IISLogEntry> FilteredIISLogEntries { get; }

        // --- IIS Count Properties (read-only, derived from FilteredIISLogEntries) ---
        public int IIS_TotalCount => LogType == LogFormatType.IIS ? FilteredIISLogEntries.Count : 0;
        public int IIS_ErrorCount => LogType == LogFormatType.IIS ? FilteredIISLogEntries.Count(e => e.HttpStatus >= 400) : 0;
        public int IIS_InfoCount => LogType == LogFormatType.IIS ? FilteredIISLogEntries.Count(e => e.HttpStatus >= 200 && e.HttpStatus < 300) : 0;
        public int IIS_RedirectCount => LogType == LogFormatType.IIS ? FilteredIISLogEntries.Count(e => e.HttpStatus >= 300 && e.HttpStatus < 400) : 0;
        // OtherCount can be calculated in MainViewModel based on Total and other counts if needed, or defined here.
        // For now, let Other be Redirects for simplicity in MainViewModel handling.
        // --- End IIS Count Properties ---

        public IRelayCommand AddIISFilterCriterionCommand { get; }
        public IRelayCommand<IISFilterCriterion> RemoveIISFilterCriterionCommand { get; }
        public IRelayCommand ApplyIISFiltersCommand { get; }
        public IRelayCommand ResetIISFiltersCommand { get; }
        // --- End IIS Filtering ---


        public TabViewModel(string filePath, string title, List<LogEntry> logEntries)
        {
            _filePath = filePath;
            _title = title;
            _logEntries = logEntries;
            _iisLogEntries = new List<IISLogEntry>();
            LogType = LogFormatType.Standard;
            _isSelected = false;

            // Initialize IIS Filtering related collections and commands even for standard logs,
            // they just won't be used or visible.
            IISFilterCriteria = new ObservableCollection<IISFilterCriterion>();
            FilteredIISLogEntries = new ObservableCollection<IISLogEntry>();

            AddIISFilterCriterionCommand = new RelayCommand(ExecuteAddIISFilterCriterion);
            RemoveIISFilterCriterionCommand = new RelayCommand<IISFilterCriterion>(ExecuteRemoveIISFilterCriterion);
            ApplyIISFiltersCommand = new RelayCommand(ExecuteApplyIISFilters);
            ResetIISFiltersCommand = new RelayCommand(ExecuteResetIISFilters);
        }

        public TabViewModel(string filePath, string title, List<IISLogEntry> iisLogEntries)
        {
            _filePath = filePath;
            _title = title;
            _iisLogEntries = iisLogEntries;
            _logEntries = new List<LogEntry>();
            LogType = LogFormatType.IIS;
            _isSelected = false;

            IISFilterCriteria = new ObservableCollection<IISFilterCriterion>();
            FilteredIISLogEntries = new ObservableCollection<IISLogEntry>(iisLogEntries); // Initially populate with all IIS entries

            AddIISFilterCriterionCommand = new RelayCommand(ExecuteAddIISFilterCriterion);
            RemoveIISFilterCriterionCommand = new RelayCommand<IISFilterCriterion>(ExecuteRemoveIISFilterCriterion);
            ApplyIISFiltersCommand = new RelayCommand(ExecuteApplyIISFilters);
            ResetIISFiltersCommand = new RelayCommand(ExecuteResetIISFilters);
        }

        private void ExecuteAddIISFilterCriterion()
        {
            if (LogType != LogFormatType.IIS) return;
            var newCriterion = new IISFilterCriterion { ParentViewModel = this };
            IISFilterCriteria.Add(newCriterion);
        }

        private void ExecuteRemoveIISFilterCriterion(IISFilterCriterion? criterion)
        {
            if (LogType != LogFormatType.IIS) return;
            if (criterion != null)
            {
                IISFilterCriteria.Remove(criterion);
            }
        }

        private void ExecuteApplyIISFilters()
        {
            if (LogType != LogFormatType.IIS) return;

            List<IISLogEntry> tempList;

            if (IISFilterCriteria == null || !IISFilterCriteria.Any())
            {
                tempList = new List<IISLogEntry>(IISLogEntries);
            }
            else
            {
                IEnumerable<IISLogEntry> currentFilteredResults = IISLogEntries;

                var processedCriteria = IISFilterCriteria.Select(c => new {
                    Criterion = c,
                    LowerFilterValue = c.Value?.ToLowerInvariant() ?? string.Empty,
                    NumericFilterValue = (c.SelectedOperator == "GreaterThan" || c.SelectedOperator == "LessThan") &&
                                         IsNumericField(c.SelectedField) &&
                                         double.TryParse(c.Value, out double parsedNum) ? (double?)parsedNum : null
                }).ToList();

                foreach (var pCrit in processedCriteria)
                {
                    currentFilteredResults = currentFilteredResults.Where(entry => 
                        MatchCriterion(entry, pCrit.Criterion, pCrit.LowerFilterValue, pCrit.NumericFilterValue));
                }
                tempList = currentFilteredResults.ToList();
            }
            
            FilteredIISLogEntries.Clear();
            foreach (var entry in tempList)
            {
                FilteredIISLogEntries.Add(entry);
            }
            
            OnPropertyChanged(nameof(IIS_TotalCount));
            OnPropertyChanged(nameof(IIS_ErrorCount));
            OnPropertyChanged(nameof(IIS_InfoCount));
            OnPropertyChanged(nameof(IIS_RedirectCount));
        }
        
        private bool IsNumericField(IISLogField field)
        {
            return field == IISLogField.TimeTaken || field == IISLogField.Port ||
                   field == IISLogField.HttpStatus || field == IISLogField.Win32Status;
        }

        private bool MatchCriterion(IISLogEntry entry, IISFilterCriterion criterion, string lowerFilterValue, double? numericFilterValue)
        {
            string? valueToCompareLower = GetPropertyValue(entry, criterion.SelectedField)?.ToString()?.ToLowerInvariant();
            
            if (valueToCompareLower == null && !string.IsNullOrEmpty(lowerFilterValue)) return false;
            if (valueToCompareLower == null && string.IsNullOrEmpty(lowerFilterValue)) return true; 
            if (valueToCompareLower == null) return false;

            switch (criterion.SelectedOperator)
            {
                case "Equals":
                    return valueToCompareLower.Equals(lowerFilterValue);
                case "NotEquals":
                    return !valueToCompareLower.Equals(lowerFilterValue);
                case "Contains":
                    return valueToCompareLower.Contains(lowerFilterValue);
                case "NotContains":
                    return !valueToCompareLower.Contains(lowerFilterValue);
                case "GreaterThan":
                case "LessThan":
                    if (!numericFilterValue.HasValue) return false; // Filter value couldn't be parsed as number

                    object? rawPropertyValue = GetPropertyValue(entry, criterion.SelectedField);
                    if (rawPropertyValue == null) return false;

                    double entryNumericValueActual;

                    if (rawPropertyValue is int intVal) entryNumericValueActual = intVal;
                    else if (rawPropertyValue is long longVal) entryNumericValueActual = longVal;
                    // Add other supported numeric types from IISLogEntry if GetPropertyValue can return them for numeric fields
                    // else if (rawPropertyValue is short shortVal) entryNumericValueActual = shortVal; 
                    // else if (rawPropertyValue is double doubleVal) entryNumericValueActual = doubleVal;
                    // else if (rawPropertyValue is float floatVal) entryNumericValueActual = floatVal;
                    else return false; // Property is not of an expected numeric type

                    if (criterion.SelectedOperator == "GreaterThan")
                        return entryNumericValueActual > numericFilterValue.Value;
                    else // LessThan
                        return entryNumericValueActual < numericFilterValue.Value;
                default:
                    return false; // Default to false for unrecognized operators
            }
        }

        private object? GetPropertyValue(IISLogEntry entry, IISLogField field)
        {
            return field switch
            {
                IISLogField.Date => entry.DateTime.HasValue ? entry.DateTime.Value.Date : (DateTime?)null, 
                IISLogField.Time => entry.DateTime.HasValue ? entry.DateTime.Value.TimeOfDay : (TimeSpan?)null, 
                IISLogField.ServerIP => entry.ServerIPAddress,
                IISLogField.ClientIP => entry.ClientIPAddress,
                IISLogField.Method => entry.Method,
                IISLogField.UriStem => entry.UriStem,
                IISLogField.UriQuery => entry.UriQuery,
                IISLogField.Port => entry.ServerPort,
                IISLogField.UserName => entry.UserName,
                IISLogField.HttpStatus => entry.HttpStatus,
                IISLogField.Win32Status => entry.Win32Status,
                IISLogField.TimeTaken => entry.TimeTaken,
                IISLogField.UserAgent => entry.UserAgent,
                IISLogField.Referer => entry.Referer,
                _ => null,
            };
        }


        private void ExecuteResetIISFilters()
        {
            if (LogType != LogFormatType.IIS) return;
            IISFilterCriteria.Clear();
            ExecuteApplyIISFilters(); // Re-apply to show all entries and update counts
        }

        // Methods for IISFilterCriterion to get operators and distinct values
        public IEnumerable<string> GetOperatorsForIISField(IISLogField field)
        {
            var operators = new List<string> { "Equals", "NotEquals", "Contains", "NotContains" };
            // Add numeric operators for specific fields
            if (field == IISLogField.TimeTaken || field == IISLogField.Port || 
                field == IISLogField.HttpStatus || field == IISLogField.Win32Status)
            {
                operators.Add("GreaterThan");
                operators.Add("LessThan");
            }
            // Potentially add "StartsWith", "EndsWith" for string fields
            // Potentially add "IsNull", "IsNotNull"
            return operators;
        }

        public IEnumerable<string> GetDistinctValuesForIISField(IISLogField field)
        {
            if (IISLogEntries == null || !IISLogEntries.Any())
            {
                return Enumerable.Empty<string>();
            }

            // Return predefined lists for some fields for better UX
            if (field == IISLogField.HttpStatus)
            {
                return new List<string> { "200", "201", "204", "301", "302", "304", "400", "401", "403", "404", "500", "502", "503" }
                    .OrderBy(s => s).ToList();
            }
            if (field == IISLogField.Method)
            {
                return new List<string> { "GET", "POST", "PUT", "DELETE", "HEAD", "OPTIONS", "PATCH", "TRACE", "CONNECT" }
                    .OrderBy(s => s).ToList();
            }

            // For other fields, get distinct values from the log entries
            var distinctValues = IISLogEntries
                .Select(entry => GetPropertyValue(entry, field)?.ToString())
                .Where(value => !string.IsNullOrEmpty(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .ToList();
            
            return distinctValues!; // Non-null asserted because of Where clause
        }
    }
} 
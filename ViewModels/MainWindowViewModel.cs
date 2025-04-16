using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogParserApp.Models;
using LogParserApp.Services;
using LogParserApp.Views;
using Microsoft.Extensions.Logging;

namespace LogParserApp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly MainViewModel _mainView;

    [ObservableProperty]
    private string _appVersion = string.Empty;

    public MainViewModel MainView => _mainView;

    // Filter Builder properties and commands moved here
    [ObservableProperty]
    private ObservableCollection<FilterCriterion> _filterCriteria = new();

    public List<string> AvailableFields { get; } = new List<string> { "Timestamp", "Level", "Source", "Message" };
    public Dictionary<string, List<string>> OperatorsByFieldType { get; } = new Dictionary<string, List<string>>
    {
        { "Timestamp", new List<string> { "Equals", "NotEquals", "GreaterThan", "LessThan", "GreaterThanOrEqual", "LessThanOrEqual" } },
        { "Level", new List<string> { "Equals", "NotEquals", "Contains" } }, // Assuming Level is string
        { "Source", new List<string> { "Equals", "NotEquals", "Contains", "StartsWith", "EndsWith" } },
        { "Message", new List<string> { "Equals", "NotEquals", "Contains", "StartsWith", "EndsWith" } }
    };

    public MainWindowViewModel()
    {
        // Design-time constructor
        _logger = null!; 
        _mainView = new MainViewModel(null!, null!, null!, null!); // Provide dummy services for design time
        AppVersion = "v0.0.1-design";
        // Add a design-time filter criterion for the previewer
        FilterCriteria.Add(new FilterCriterion { SelectedField = "Level", SelectedOperator = "Equals", Value = "ERROR" });
    }
    
    public MainWindowViewModel(ILogger<MainWindowViewModel> logger, MainViewModel mainView)
    {
        _logger = logger;
        _mainView = mainView;
        OpenLogFileCommand = new RelayCommand<LogEntry?>(OpenLogFile);
        _mainView.ExternalOpenFileCommand = OpenLogFileCommand;
        
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        AppVersion = $"v{version?.Major ?? 0}.{version?.Minor ?? 0}.{version?.Build ?? 0}";
        
        _logger.LogInformation("MainWindowViewModel initialized");
    }

    [RelayCommand]
    private void AddFilterCriterion()
    {
        var newCriterion = new FilterCriterion
        {
            ParentViewModel = this // Set the parent ViewModel reference
        };
        FilterCriteria.Add(newCriterion);
        _logger.LogInformation("Added new filter criterion.");
    }

    [RelayCommand]
    private void RemoveFilterCriterion(FilterCriterion? criterion) // Make parameter nullable
    {
        if (criterion != null)
        {
            FilterCriteria.Remove(criterion);
            _logger.LogInformation("Removed filter criterion.");
        }
        else
        {
             _logger.LogWarning("Attempted to remove a null filter criterion.");
        }
    }

    [RelayCommand]
    private async Task ApplyFilters()
    {
         if (MainView.LogEntries.Count == 0)
        {
            MainView.StatusMessage = "No log entries to filter";
            return;
        }
         if (FilterCriteria.Count == 0 || FilterCriteria.Any(c => string.IsNullOrWhiteSpace(c.SelectedField) || string.IsNullOrWhiteSpace(c.SelectedOperator)))
         {
             MainView.StatusMessage = "Please configure all filter criteria";
             return;
         }

        MainView.StatusMessage = "Applying filters...";
        MainView.IsLoading = true; // Access IsLoading via MainView

        try
        {
            // Get log entries from MainViewModel
            var entriesToFilter = MainView.LogEntries.ToList();
            
            await Task.Run(() =>
            {
                IEnumerable<LogEntry> currentlyFiltered = entriesToFilter;
                foreach (var criterion in FilterCriteria)
                {
                    if (string.IsNullOrWhiteSpace(criterion.SelectedField) ||
                        string.IsNullOrWhiteSpace(criterion.SelectedOperator) ||
                        criterion.Value == null) // Check for null value
                    {
                        _logger.LogWarning("Skipping incomplete filter criterion: Field='{Field}', Operator='{Operator}', Value='{Value}'",
                            criterion.SelectedField, criterion.SelectedOperator, criterion.Value);
                        continue;
                    }
                    
                    currentlyFiltered = ApplySingleFilter(currentlyFiltered, criterion);
                }

                var filteredEntries = currentlyFiltered.ToList();

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MainView.FilteredLogEntries.Clear(); // Modify collection in MainView
                    foreach (var entry in filteredEntries)
                    {
                        MainView.FilteredLogEntries.Add(entry);
                    }
                    MainView.StatusMessage = $"Filters applied. Found {MainView.FilteredLogEntries.Count} entries";
                    MainView.SelectedTabIndex = 1; // Switch tab in MainView
                });
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying filters");
            MainView.StatusMessage = $"Error applying filters: {ex.Message}";
        }
        finally
        {
            MainView.IsLoading = false; // Access IsLoading via MainView
        }
    }
    
    private IEnumerable<LogEntry> ApplySingleFilter(IEnumerable<LogEntry> entries, FilterCriterion criterion)
    {
        _logger.LogDebug("Applying filter: {Field} {Operator} '{Value}'", criterion.SelectedField, criterion.SelectedOperator, criterion.Value);

        if (criterion.SelectedField == null || criterion.SelectedOperator == null || criterion.Value == null)
        {
            return entries; 
        }
             
        switch (criterion.SelectedField)
        {
            case "Level":
                return criterion.SelectedOperator switch
                {
                    "Equals" => entries.Where(e => e.Level.Equals(criterion.Value, StringComparison.OrdinalIgnoreCase)),
                    "NotEquals" => entries.Where(e => !e.Level.Equals(criterion.Value, StringComparison.OrdinalIgnoreCase)),
                    "Contains" => entries.Where(e => e.Level.Contains(criterion.Value, StringComparison.OrdinalIgnoreCase)),
                    _ => entries
                };
            case "Source":
                return criterion.SelectedOperator switch
                {
                    "Equals" => entries.Where(e => e.Source?.Equals(criterion.Value, StringComparison.OrdinalIgnoreCase) ?? false),
                    "NotEquals" => entries.Where(e => !(e.Source?.Equals(criterion.Value, StringComparison.OrdinalIgnoreCase) ?? false)),
                    "Contains" => entries.Where(e => e.Source?.Contains(criterion.Value, StringComparison.OrdinalIgnoreCase) ?? false),
                    "StartsWith" => entries.Where(e => e.Source?.StartsWith(criterion.Value, StringComparison.OrdinalIgnoreCase) ?? false),
                    "EndsWith" => entries.Where(e => e.Source?.EndsWith(criterion.Value, StringComparison.OrdinalIgnoreCase) ?? false),
                     _ => entries
                };
            case "Message": 
                 return criterion.SelectedOperator switch
                {
                    "Equals" => entries.Where(e => e.Message.Equals(criterion.Value, StringComparison.OrdinalIgnoreCase)),
                    "NotEquals" => entries.Where(e => !e.Message.Equals(criterion.Value, StringComparison.OrdinalIgnoreCase)),
                    "Contains" => entries.Where(e => e.Message.Contains(criterion.Value, StringComparison.OrdinalIgnoreCase)),
                    "StartsWith" => entries.Where(e => e.Message.StartsWith(criterion.Value, StringComparison.OrdinalIgnoreCase)),
                    "EndsWith" => entries.Where(e => e.Message.EndsWith(criterion.Value, StringComparison.OrdinalIgnoreCase)),
                     _ => entries
                };
            case "Timestamp":
                // Use DateTimeOffset from DatePicker
                if (DateTimeOffset.TryParse(criterion.Value, out var dateValue))
                {
                    // Compare using the Date part of DateTimeOffset for date-only comparisons
                    // Or use the full DateTimeOffset for exact comparisons if needed
                    return criterion.SelectedOperator switch
                    {
                        "Equals" => entries.Where(e => e.Timestamp.Date == dateValue.Date), // Compare Date part
                        "NotEquals" => entries.Where(e => e.Timestamp.Date != dateValue.Date),
                        "GreaterThan" => entries.Where(e => e.Timestamp > dateValue),
                        "LessThan" => entries.Where(e => e.Timestamp < dateValue),
                        "GreaterThanOrEqual" => entries.Where(e => e.Timestamp >= dateValue),
                        "LessThanOrEqual" => entries.Where(e => e.Timestamp <= dateValue),
                        _ => entries
                    };
                }
                else
                {
                    _logger.LogWarning("Could not parse date value for Timestamp filter: {Value}", criterion.Value);
                    // Optionally return empty if parsing fails and it's a required filter
                    // return Enumerable.Empty<LogEntry>(); 
                    return entries; // Or just ignore this filter if parsing fails
                }
                 
            default:
                _logger.LogWarning("Unsupported field for filtering: {Field}", criterion.SelectedField);
                return entries;
        }
    }

    private void OpenLogFile(LogEntry? entry)
    {
        MainView.OpenLogFileCommand.Execute(entry);
    }

    public IRelayCommand OpenLogFileCommand { get; }
}

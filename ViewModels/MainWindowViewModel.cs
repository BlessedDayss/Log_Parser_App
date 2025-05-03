using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Log_Parser_App;
using Log_Parser_App.Models;
using Log_Parser_App.Services;
using Log_Parser_App.ViewModels;
using Microsoft.Extensions.Logging;

namespace LogParserApp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly Log_Parser_App.ViewModels.MainViewModel _mainView;
    private readonly IUpdateService? _updateService;

    [ObservableProperty]
    private string _appVersion = string.Empty;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private UpdateInfo? _availableUpdate;

    [ObservableProperty]
    private bool _isDashboardVisible;

    public Log_Parser_App.ViewModels.MainViewModel MainView => _mainView;

    // Filter Builder properties and commands moved here
    [ObservableProperty]
    private ObservableCollection<FilterCriterion> _filterCriteria = new();

    public List<string> AvailableFields { get; } = new List<string> { "Timestamp", "Level", "Source", "Message" };
    public Dictionary<string, List<string>> OperatorsByFieldType { get; } = new Dictionary<string, List<string>>
    {
        { "Timestamp", new List<string> { "Equals", "NotEquals", "GreaterThan", "LessThan", "GreaterThanOrEqual", "LessThanOrEqual" } },
        { "Level", new List<string> { "Equals", "NotEquals", "Contains" } },
        { "Source", new List<string> { "Equals", "NotEquals", "Contains", "StartsWith", "EndsWith" } },
        { "Message", new List<string> { "Equals", "NotEquals", "Contains", "StartsWith", "EndsWith" } }
    };
    
    // Dictionary to store available values for fields
    public Dictionary<string, List<string>> AvailableValuesByField { get; } = new Dictionary<string, List<string>>
    {
        { "Level", new List<string>() },
        { "Source", new List<string>() }
    };

    public MainWindowViewModel()
    {
        // Design-time constructor
        _logger = null!; 
        _mainView = new Log_Parser_App.ViewModels.MainViewModel(null!, null!, null!, null!); // Provide dummy services for design time
        AppVersion = "v0.0.1-design";
        // Add a design-time filter criterion for the previewer
        FilterCriteria.Add(new FilterCriterion { SelectedField = "Level", SelectedOperator = "Equals", Value = "ERROR" });
    }
    
    public MainWindowViewModel(ILogger<MainWindowViewModel> logger, Log_Parser_App.ViewModels.MainViewModel mainView)
    {
        _logger = logger;
        _mainView = mainView;
        
        // Пытаемся получить сервис обновлений
        try
        {
            var serviceProvider = App.Services;
            if (serviceProvider != null)
            {
                _updateService = serviceProvider.GetService(typeof(IUpdateService)) as IUpdateService;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get update service");
        }
        
        OpenLogFileCommand = new RelayCommand<LogEntry?>(OpenLogFile);
        _mainView.ExternalOpenFileCommand = OpenLogFileCommand;
        
        // Получаем версию приложения
        LoadApplicationVersion();
        
        // Инициализируем статус панели дашборда
        IsDashboardVisible = false;
        
        // Subscribe to MainView's PropertyChanged event to update filter values when log entries change
        _mainView.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(_mainView.LogEntries))
            {
                PopulateAvailableFilterValues();
            }
        };
        
        // Проверяем обновления при запуске
        CheckForUpdatesCommand.Execute(null);
        
        _logger.LogInformation("MainWindowViewModel initialized");
    }
    
    private void LoadApplicationVersion()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            AppVersion = $"v{version?.Major ?? 0}.{version?.Minor ?? 0}.{version?.Build ?? 0}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load application version");
            AppVersion = "v?.?.?";
        }
    }
    
    [RelayCommand]
    private async Task CheckForUpdates()
    {
        if (_updateService == null)
        {
            _logger.LogWarning("Update service is not available");
            return;
        }
        
        try
        {
            _logger.LogInformation("Checking for updates");
            AvailableUpdate = await _updateService.CheckForUpdatesAsync();
            
            if (AvailableUpdate != null && AvailableUpdate.Version != null && AvailableUpdate.Version != new Version(0, 0, 0))
            {
                IsUpdateAvailable = true;
                _logger.LogInformation("Update available: {Version}", AvailableUpdate.Version);
            }
            else
            {
                IsUpdateAvailable = false;
                _logger.LogInformation("No updates available");
                // Ensure we don't keep an old update object around
                AvailableUpdate = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates");
            IsUpdateAvailable = false;
        }
    }
    
    [RelayCommand]
    private async Task InstallUpdate()
    {
        if (_updateService == null || AvailableUpdate == null)
        {
            _logger.LogWarning("Update service or update info is not available");
            return;
        }
        
        try
        {
            _logger.LogInformation("Starting update installation");
            
            // Загружаем обновление
            var progress = new Progress<int>(percent =>
            {
                _mainView.StatusMessage = $"Загрузка обновления... {percent}%";
            });
            
            var filePath = await _updateService.DownloadUpdateAsync(AvailableUpdate, progress);
            
            if (string.IsNullOrEmpty(filePath))
            {
                _logger.LogWarning("Download failed");
                _mainView.StatusMessage = "Не удалось загрузить обновление";
                return;
            }
            
            // Устанавливаем обновление
            _mainView.StatusMessage = "Установка обновления...";
            var result = await _updateService.InstallUpdateAsync(filePath);
            
            if (result)
            {
                _mainView.StatusMessage = "Обновление успешно установлено. Требуется перезапуск приложения.";
                
                // TODO: Добавить логику перезапуска приложения
                // Environment.Exit(0);
            }
            else
            {
                _mainView.StatusMessage = "Не удалось установить обновление";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install update");
            _mainView.StatusMessage = $"Ошибка установки: {ex.Message}";
        }
    }
    
    // Method to populate available values for filter fields based on log entries
    private void PopulateAvailableFilterValues()
    {
        if (MainView.LogEntries == null || MainView.LogEntries.Count == 0)
        {
            _logger.LogDebug("No log entries available to populate filter values");
            return;
        }
        
        _logger.LogInformation("Populating available filter values from {Count} log entries", MainView.LogEntries.Count);
        
        // Clear existing values
        foreach (var key in AvailableValuesByField.Keys.ToList())
        {
            AvailableValuesByField[key].Clear();
        }
        
        // Extract unique values for Level
        var levels = MainView.LogEntries
            .Select(e => e.Level)
            .Where(l => !string.IsNullOrEmpty(l))
            .Distinct()
            .OrderBy(l => l)
            .ToList();
        AvailableValuesByField["Level"] = levels;
        _logger.LogDebug("Found {Count} unique levels: {Levels}", levels.Count, string.Join(", ", levels));
        
        // Extract unique values for Source
        var sources = MainView.LogEntries
            .Select(e => e.Source)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .OrderBy(s => s)
            .ToList();
        AvailableValuesByField["Source"] = sources;
        _logger.LogDebug("Found {Count} unique sources", sources.Count);
        
        // Notify UI of changes
        OnPropertyChanged(nameof(AvailableValuesByField));
    }

    [RelayCommand]
    private void AddFilterCriterion()
    {
        var newCriterion = new FilterCriterion
        {
            ParentViewModel = this, // Set the parent ViewModel reference
            SelectedField = "Level", // Set default field to Level
            SelectedOperator = "Equals" // Set default operator to Equals
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
    private async Task ApplyFilterCriterion(FilterCriterion? criterion)
    {
        if (criterion == null)
        {
            _logger.LogWarning("Attempted to apply a null filter criterion.");
            return;
        }

        if (MainView.LogEntries.Count == 0)
        {
            MainView.StatusMessage = "No log entries to filter";
            return;
        }

        if (string.IsNullOrWhiteSpace(criterion.SelectedField) || 
            string.IsNullOrWhiteSpace(criterion.SelectedOperator) ||
            criterion.Value == null)
        {
            MainView.StatusMessage = "Please configure the filter criterion completely";
            return;
        }

        MainView.StatusMessage = "Applying filter...";
        MainView.IsLoading = true;

        try
        {
            var entriesToFilter = MainView.LogEntries.ToList();
            
            await Task.Run(() =>
            {
                IEnumerable<LogEntry> filtered = entriesToFilter;
                
                if (!string.IsNullOrWhiteSpace(criterion.SelectedField) &&
                    !string.IsNullOrWhiteSpace(criterion.SelectedOperator) &&
                    criterion.Value != null)
                {
                    filtered = ApplyFilterCriterion(filtered, criterion);
                }
                
                Dispatcher.UIThread.Post(() =>
                {
                    MainView.FilteredLogEntries.Clear();
                    foreach (var entry in filtered)
                    {
                        MainView.FilteredLogEntries.Add(entry);
                    }
                    MainView.StatusMessage = $"Applied filter. Showing {MainView.FilteredLogEntries.Count} entries.";
                });
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying filter");
            MainView.StatusMessage = $"Error applying filter: {ex.Message}";
        }
        finally
        {
            MainView.IsLoading = false;
        }
    }

    private IEnumerable<LogEntry> ApplyFilterCriterion(IEnumerable<LogEntry> entries, FilterCriterion criterion)
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

    [RelayCommand]
    private async Task ResetFilters()
    {
        if (MainView.LogEntries.Count == 0)
        {
            MainView.StatusMessage = "No log entries to reset";
            return;
        }
        
        try
        {
            MainView.StatusMessage = "Resetting filters...";
            MainView.IsLoading = true;
            
            await Task.Run(() =>
            {
                var allEntries = MainView.LogEntries.ToList();
                
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MainView.FilteredLogEntries.Clear();
                    foreach (var entry in allEntries)
                    {
                        MainView.FilteredLogEntries.Add(entry);
                    }
                    
                    MainView.StatusMessage = $"Filters reset. Showing all {MainView.FilteredLogEntries.Count} entries";
                });
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting filters");
            MainView.StatusMessage = $"Error resetting filters: {ex.Message}";
        }
        finally
        {
            MainView.IsLoading = false;
        }
    }

    private void OpenLogFile(LogEntry? entry)
    {
        MainView.OpenLogFileCommand.Execute(entry);
    }

    // Удаляем required и используем null! для подавления предупреждения компилятора
    public IRelayCommand OpenLogFileCommand { get; } = null!;

    [RelayCommand]
    private void ToggleDashboard()
    {
        IsDashboardVisible = !IsDashboardVisible;
        _logger.LogInformation("Dashboard visibility toggled to {IsVisible}", IsDashboardVisible);
    }
}

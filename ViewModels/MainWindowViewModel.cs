using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
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
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.ViewModels;

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

    public MainWindowViewModel()
    {
        // Design-time constructor
        _logger = null!; 
        _mainView = new Log_Parser_App.ViewModels.MainViewModel(null!, null!, null!, null!, null!); // Provide dummy services for design time
        _updateService = null!;
        AppVersion = "v0.0.1-design";
        // Design-time filter criterion for the previewer - this will cause issues if FilterCriteria is removed
        // We might need a way for the designer to see a sample criterion if MainView is also design-time.
        // For now, let's assume MainView can handle its own design-time data if needed.
        // FilterCriteria.Add(new FilterCriterion { SelectedField = "Level", SelectedOperator = "Equals", Value = "ERROR" }); 
        CheckForUpdatesAsyncCommand = new RelayCommand(async () => await Task.CompletedTask);
        // AddFilterCriterionCommand = new RelayCommand(() => {}); // Removed
    }
    
    public MainWindowViewModel(ILogger<MainWindowViewModel> logger, Log_Parser_App.ViewModels.MainViewModel mainView, IUpdateService updateService)
    {
        _logger = logger;
        _mainView = mainView;
        _updateService = updateService;
        
        LoadApplicationVersion();
        
        CheckForUpdatesAsyncCommand = new RelayCommand(async () => await ExecuteCheckForUpdatesAsync());
        InstallUpdateCommand = new RelayCommand(async () => await ExecuteInstallUpdateAsync());
        // AddFilterCriterionCommand = new RelayCommand(ExecuteAddFilterCriterion); // Removed
        
        IsDashboardVisible = false;
        
        // FilterCriteria = new ObservableCollection<FilterCriterion>(); // Removed
        // AvailableValuesByField = new Dictionary<string, HashSet<string>>(); // Removed
        
        CheckForUpdatesAsyncCommand.Execute(null);
        
        _logger.LogInformation("MainWindowViewModel initialized");
    }
    
    private void LoadApplicationVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        AppVersion = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "Unknown";
    }

    public ICommand CheckForUpdatesAsyncCommand { get; }
    public ICommand InstallUpdateCommand { get; }

    private async Task ExecuteCheckForUpdatesAsync()
    {
        try
        {
            var updateInfo = _updateService == null ? null : await _updateService.CheckForUpdatesAsync();
            if (updateInfo != null)
            {
                AvailableUpdate = updateInfo;
                IsUpdateAvailable = true;
                _logger.LogInformation($"Update available: {updateInfo.Version}");
            }
            else
            {
                IsUpdateAvailable = false;
                _logger.LogInformation("Application is up to date");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates");
            IsUpdateAvailable = false;
        }
    }

    private async Task ExecuteInstallUpdateAsync()
    {
        try
        {
            if (_updateService == null)
                return;

            var updateInfo = AvailableUpdate ?? await _updateService.CheckForUpdatesAsync();

            if (updateInfo == null)
            {
                IsUpdateAvailable = false;
                _logger.LogInformation("Application is up to date");
                return;
            }

            AvailableUpdate = updateInfo;
            IsUpdateAvailable = true;

            var filePath = await _updateService.DownloadUpdateAsync(updateInfo);
            if (!string.IsNullOrEmpty(filePath))
            {
                await _updateService.InstallUpdateAsync(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error installing update");
            IsUpdateAvailable = false;
        }
    }

    private void OpenLogFile(LogEntry? entry) // This calls MainView.OpenLogFileCommand, seems ok.
    {
        MainView.OpenLogFileCommand.Execute(entry);
    }

    public IRelayCommand OpenLogFileCommand { get; } = null!;

    public ICommand ToggleDashboardVisibilityCommand => new RelayCommand(() =>
    {
        _logger.LogInformation($"Toggling dashboard visibility. Current state: {IsDashboardVisible}");
        IsDashboardVisible = !IsDashboardVisible;
    });

    public ICommand SelectTabCommand => new RelayCommand<TabViewModel>(tab =>
    {
        if (tab != null)
        {
            MainView.SelectTabCommand.Execute(tab);
        }
    });

    public ICommand CloseTabCommand => new RelayCommand<TabViewModel>(tab =>
    {
        if (tab != null)
        {
            MainView.CloseTabCommand.Execute(tab);
        }
    });
}

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
using Log_Parser_App.Interfaces;

namespace Log_Parser_App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly Log_Parser_App.ViewModels.MainViewModel _mainView;
    private readonly Log_Parser_App.Interfaces.IUpdateService? _updateService;
    private readonly UpdateViewModel? _updateViewModel;

    [ObservableProperty]
    private string _appVersion = string.Empty;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private UpdateInfo? _availableUpdate;



    [ObservableProperty]
    private bool _isDownloadingUpdate;

    [ObservableProperty]
    private int _downloadProgress;

    public Log_Parser_App.ViewModels.MainViewModel MainView => _mainView;

    public MainWindowViewModel()
    {
        // Design-time constructor
        _logger = null!; 
        _mainView = new Log_Parser_App.ViewModels.MainViewModel(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        _updateService = null!;
        AppVersion = "v0.0.1-design";
        // Design-time filter criterion for the previewer - this will cause issues if FilterCriteria is removed
        // We might need a way for the designer to see a sample criterion if MainView is also design-time.
        // For now, let's assume MainView can handle its own design-time data if needed.
        // FilterCriteria.Add(new FilterCriterion { SelectedField = "Level", SelectedOperator = "Equals", Value = "ERROR" }); 
        CheckForUpdatesAsyncCommand = new RelayCommand(async () => await Task.CompletedTask);
        DownloadAndUpdateCommand = new RelayCommand(async () => await Task.CompletedTask);
        ShowUpdateSettingsCommand = new RelayCommand(async () => await Task.CompletedTask);
    }
    
    public MainWindowViewModel(ILogger<MainWindowViewModel> logger, Log_Parser_App.ViewModels.MainViewModel mainView, Log_Parser_App.Interfaces.IUpdateService updateService, UpdateViewModel updateViewModel)
    {
        _logger = logger;
        _mainView = mainView;
        _updateService = updateService;
        _updateViewModel = updateViewModel;
        
        LoadApplicationVersion();
        
        CheckForUpdatesAsyncCommand = new RelayCommand(async () => await ExecuteCheckForUpdatesAsync());
        DownloadAndUpdateCommand = new RelayCommand(async () => await ExecuteDownloadAndUpdateAsync());
        ShowUpdateSettingsCommand = new RelayCommand(async () => await ShowUpdateSettingsAsync());
        

        
        // Subscribe to UpdateViewModel events
        if (_updateViewModel != null)
        {
            _updateViewModel.PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(UpdateViewModel.AvailableUpdate):
                        AvailableUpdate = _updateViewModel.AvailableUpdate;
                        IsUpdateAvailable = _updateViewModel.AvailableUpdate != null;
                        break;
                    case nameof(UpdateViewModel.IsDownloadingUpdate):
                        IsDownloadingUpdate = _updateViewModel.IsDownloadingUpdate;
                        break;
                    case nameof(UpdateViewModel.DownloadProgress):
                        DownloadProgress = _updateViewModel.DownloadProgress;
                        break;
                }
            };
        }
        
        // Auto-update check is already handled by App.axaml.cs in CheckForUpdatesOnStartupAsync()
        // CheckForUpdatesAsyncCommand.Execute(null);  // REMOVED to avoid race condition
        
        _logger.LogInformation("MainWindowViewModel initialized with auto-update support");
    }
    
    private void LoadApplicationVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        AppVersion = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "Unknown";
    }

    public ICommand CheckForUpdatesAsyncCommand { get; }
    public ICommand DownloadAndUpdateCommand { get; private set; } = null!;
    public ICommand ShowUpdateSettingsCommand { get; private set; } = null!;

    private async Task ExecuteCheckForUpdatesAsync()
    {
        try
        {
            if (_updateViewModel != null)
            {
                await _updateViewModel.CheckForUpdatesAsync();
            }
            else
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates");
            IsUpdateAvailable = false;
        }
    }

    private async Task ExecuteDownloadAndUpdateAsync()
    {
        try
        {
            if (_updateViewModel != null)
            {
                await _updateViewModel.DownloadAndUpdateCommand.ExecuteAsync(null);
            }
            else
            {
                _logger.LogWarning("UpdateViewModel is not available for manual update");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing manual update");
        }
    }

    private async Task ShowUpdateSettingsAsync()
    {
        try
        {
            var updateWindow = new Log_Parser_App.Views.UpdateSettingsWindow();
            
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                await updateWindow.ShowDialog(desktop.MainWindow);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show update settings window");
        }
    }

    private void OpenLogFile(LogEntry? entry) // This calls MainView.OpenLogFileCommand, seems ok.
    {
        MainView.OpenLogFileCommand.Execute(entry);
    }

    public IRelayCommand OpenLogFileCommand { get; } = null!;



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

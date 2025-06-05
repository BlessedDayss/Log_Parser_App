#pragma warning disable CS0103 // The name 'InitializeComponent' does not exist in the current context

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Log_Parser_App.ViewModels;
using System;
using NLog;
using System.IO;
using Avalonia.Input;
using System.Diagnostics;
using Log_Parser_App.Models;

namespace Log_Parser_App.Views;

public partial class MainWindow : Window
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    private DateTime _lastTapTime = DateTime.MinValue;
    private object? _lastTappedItem = null;

    // Добавляем свойство для доступа к MainView из XAML
    public MainViewModel? MainViewModel => (DataContext as MainWindowViewModel)?.MainView;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
#if DEBUG
        this.AttachDevTools();
#endif
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            // vm.Initialize(); // Example if you need to initialize after DataContext is set
        }
    }
    
    private void UpdateTheme(bool isDarkTheme)
    {
        Application.Current!.RequestedThemeVariant = isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    private void UpdateVersion()
    {
        // Чтение версии из файла VERSION.txt, расположенного в корне проекта
        string versionFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VERSION.txt");
        if (File.Exists(versionFilePath))
        {
            string version = File.ReadAllText(versionFilePath).Trim();
            // Предполагается, что в MainWindow.axaml есть элемент с именем VersionLabel
            var versionLabel = this.FindControl<Avalonia.Controls.TextBlock>("VersionLabel");
            if(versionLabel != null)
            {
                versionLabel.Text = "v" + version;
            }
            else
            {
                logger.Warn("Элемент VersionLabel не найден в UI.");
            }
        }
        else
        {
            logger.Warn($"Файл версии не найден: {versionFilePath}");
        }
    }

    private void LogEntryRow_Tapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is DataGrid dataGrid && dataGrid.SelectedItem is Log_Parser_App.Models.LogEntry entry)
        {
            var now = DateTime.Now;
            if (_lastTappedItem == entry && (now - _lastTapTime).TotalMilliseconds < 400)
            {
                entry.IsExpanded = !entry.IsExpanded;
                _lastTapTime = DateTime.MinValue;
                _lastTappedItem = null;
            }
            else
            {
                _lastTapTime = now;
                _lastTappedItem = entry;
            }
        }
    }

    // Обработка выбора вкладки
    public void SelectTab(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.CommandParameter is TabViewModel tab 
            && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.MainView.SelectTabCommand.Execute(tab);
        }
    }
    
    // Обработка закрытия вкладки
    public void CloseTab(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.CommandParameter is TabViewModel tab
            && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.MainView.CloseTabCommand.Execute(tab);
            e.Handled = true; // Предотвращаем всплытие события
        }
    }

    private void Tab_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Button { CommandParameter: TabViewModel tabViewModel } && DataContext is MainWindowViewModel)
        {
            if (!string.IsNullOrEmpty(tabViewModel.FilePath) && File.Exists(tabViewModel.FilePath))
            {
                try
                {
                    var processStartInfo = new ProcessStartInfo(tabViewModel.FilePath)
                    {
                        UseShellExecute = true
                    };
                    Process.Start(processStartInfo);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to open file '{tabViewModel.FilePath}': {ex.Message}");
                }
            }
        }
    }

    // Обработчики для стандартных фильтров
    public void OnAddFilterClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && viewModel.MainView?.SelectedTab != null)
        {
            var criterion = new FilterCriterion { ParentViewModel = viewModel.MainView.SelectedTab };
            viewModel.MainView.SelectedTab.FilterCriteria.Add(criterion);
        }
    }
    
    public void OnApplyFiltersClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && viewModel.MainView?.SelectedTab != null)
        {
            // Здесь должна быть логика применения фильтров
            // Поскольку мы не знаем точной сигнатуры метода в TabViewModel,
            // предполагаем, что он вызывается через выполнение команды
            viewModel.MainView.SelectedTab.ApplyFiltersCommand?.Execute(null);
        }
    }
    
    public void OnResetFiltersClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && viewModel.MainView?.SelectedTab != null)
        {
            // Предполагаем, что команда сброса фильтров доступна в TabViewModel
            viewModel.MainView.SelectedTab.ResetFiltersCommand?.Execute(null);
        }
    }
    
    // Обработчики для IIS фильтров
    public void OnAddIISFilterClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && viewModel.MainView?.SelectedTab != null)
        {
            viewModel.MainView.SelectedTab.AddIISFilterCriterionCommand?.Execute(null);
        }
    }
    
    public void OnApplyIISFiltersClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && viewModel.MainView?.SelectedTab != null)
        {
            viewModel.MainView.SelectedTab.ApplyIISFiltersCommand?.Execute(null);
        }
    }
    
    public void OnResetIISFiltersClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && viewModel.MainView?.SelectedTab != null)
        {
            viewModel.MainView.SelectedTab.ResetIISFiltersCommand?.Execute(null);
        }
    }
}
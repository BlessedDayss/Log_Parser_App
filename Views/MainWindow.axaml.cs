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
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Specialized;

namespace Log_Parser_App.Views;

public partial class MainWindow : Window
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    private DateTime _lastTapTime = DateTime.MinValue;
    private object? _lastTappedItem = null;
    private EmptyStateView? _emptyStateView;
    private Grid? _mainContentGrid;

    // Добавляем свойство для доступа к MainView из XAML
    public MainViewModel? MainViewModel => (DataContext as MainWindowViewModel)?.MainView;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
#if DEBUG
        this.AttachDevTools();
#endif
        this.Opened += MainWindow_Opened;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            // Подписываемся на событие изменения коллекции вкладок
            viewModel.MainView.FileTabs.CollectionChanged += FileTabs_CollectionChanged;
            // Обновляем состояние
            UpdateContentVisibility(viewModel.MainView.FileTabs.Count);
        }
    }
    
    private void UpdateTheme(bool isDarkTheme)
    {
        Application.Current!.RequestedThemeVariant = isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    private void UpdateVersion()
    {
        var versionFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.txt");
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
            viewModel.MainView.SelectedTab.AddFilterCriteriaCommand?.Execute(null);
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

    private void MainWindow_Opened(object? sender, EventArgs e)
    {
        // Находим главный контент-грид после того, как окно полностью загрузилось
        _mainContentGrid = this.Find<Grid>("MainContentGrid");
        
        if (DataContext is MainWindowViewModel viewModel && viewModel.MainView.FileTabs.Count == 0)
        {
            ShowEmptyState();
        }
    }
    
    private void FileTabs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            UpdateContentVisibility(viewModel.MainView.FileTabs.Count);
        }
    }
    
    private void UpdateContentVisibility(int tabCount)
    {
        if (tabCount == 0)
        {
            ShowEmptyState();
        }
        else
        {
            HideEmptyState();
        }
    }
    
    private void ShowEmptyState()
    {
        if (_emptyStateView == null && _mainContentGrid != null)
        {
            _emptyStateView = new EmptyStateView();
            _emptyStateView.OpenLogFileRequested += EmptyStateView_OpenLogFileRequested;
            Grid.SetRow(_emptyStateView, 0);
            Grid.SetRowSpan(_emptyStateView, 3);
            Grid.SetColumn(_emptyStateView, 0);
            Grid.SetColumnSpan(_emptyStateView, 3);
            _mainContentGrid.Children.Add(_emptyStateView);
        }
        
        if (_emptyStateView != null)
        {
            _emptyStateView.IsVisible = true;
        }
    }
    
    private void HideEmptyState()
    {
        if (_emptyStateView != null)
        {
            _emptyStateView.IsVisible = false;
        }
    }
    
    private void EmptyStateView_OpenLogFileRequested(object? sender, EventArgs e)
    {
        var mainWindow = this;
        var dataContext = mainWindow.DataContext as MainWindowViewModel;
        if (dataContext?.MainView != null)
        {
            // Запускаем команду загрузки файла
            dataContext.MainView.LoadFileCommand?.Execute(null);
        }
    }

    // Handler for StackTrace double tap to expand/collapse full stack trace
    public void OnStackTraceDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is TextBlock textBlock)
        {
            // Toggle between collapsed and expanded state
            if (textBlock.MaxHeight == 60)
            {
                // Expand: remove height limit and make text selectable
                textBlock.MaxHeight = double.PositiveInfinity;
                textBlock.IsHitTestVisible = true;
                textBlock.Cursor = new Cursor(StandardCursorType.Arrow);
                textBlock.Background = Avalonia.Media.Brushes.DarkSlateGray;
                textBlock.Opacity = 1.0;
            }
            else
            {
                // Collapse: restore height limit
                textBlock.MaxHeight = 60;
                textBlock.Cursor = new Cursor(StandardCursorType.Hand);
                textBlock.Background = Avalonia.Media.Brushes.Transparent;
                textBlock.Opacity = 0.9;
            }
        }
    }
}
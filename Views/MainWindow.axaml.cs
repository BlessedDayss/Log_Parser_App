namespace Log_Parser_App.Views
{
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Interactivity;
    using Log_Parser_App.ViewModels;
    using System;
    using NLog;
    using System.IO;
    using Avalonia.Input;
    using System.Diagnostics;
    using Log_Parser_App.Models;
    using System.Collections.Specialized;
    using System.Threading.Tasks;



    public partial class MainWindow : Window
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private DateTime _lastTapTime = DateTime.MinValue;
        private object? _lastTappedItem = null;
        private EmptyStateView? _emptyStateView;
        private Grid? _mainContentGrid;

        public MainViewModel? MainViewModel => (DataContext as MainWindowViewModel)?.MainView;

        public MainWindow() {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
#if DEBUG
            this.AttachDevTools();
#endif
            this.Opened += MainWindow_Opened;
        }

        private void OnDataContextChanged(object? sender, EventArgs e) {
            if (DataContext is MainWindowViewModel viewModel) {
                viewModel.MainView.FileTabs.CollectionChanged += FileTabs_CollectionChanged;
                UpdateContentVisibility(viewModel.MainView.FileTabs.Count);
            }
        }



        private void LogEntryRow_Tapped(object? sender, Avalonia.Input.TappedEventArgs e) {
            if (sender is DataGrid dataGrid && dataGrid.SelectedItem is Log_Parser_App.Models.LogEntry entry) {
                var now = DateTime.Now;
                if (_lastTappedItem == entry && (now - _lastTapTime).TotalMilliseconds < 400) {
                    entry.IsExpanded = !entry.IsExpanded;
                    _lastTapTime = DateTime.MinValue;
                    _lastTappedItem = null;
                } else {
                    _lastTapTime = now;
                    _lastTappedItem = entry;
                }
            }
        }

        public void SelectTab(object? sender, RoutedEventArgs e) {
            if (sender is Button button && button.CommandParameter is TabViewModel tab && DataContext is MainWindowViewModel viewModel) {
                viewModel.MainView.SelectTabCommand.Execute(tab);
            }
        }

        public void CloseTab(object? sender, RoutedEventArgs e) {
            if (sender is Button button && button.CommandParameter is TabViewModel tab && DataContext is MainWindowViewModel viewModel) {
                viewModel.MainView.CloseTabCommand.Execute(tab);
                e.Handled = true; 
            }
        }

        private void Tab_DoubleTapped(object? sender, TappedEventArgs e) {
            if (sender is Button { CommandParameter: TabViewModel tabViewModel } && DataContext is MainWindowViewModel) {
                if (!string.IsNullOrEmpty(tabViewModel.FilePath) && File.Exists(tabViewModel.FilePath)) {
                    try {
                        var processStartInfo = new ProcessStartInfo(tabViewModel.FilePath) {
                            UseShellExecute = true
                        };
                        Process.Start(processStartInfo);
                    } catch (Exception ex) {
                        Console.WriteLine($"Failed to open file '{tabViewModel.FilePath}': {ex.Message}");
                    }
                }
            }
        }

        public void OnAddFilterClick(object? sender, RoutedEventArgs e) {
            if (DataContext is MainWindowViewModel viewModel && viewModel.MainView?.SelectedTab != null) {
                viewModel.MainView.SelectedTab.AddFilterCriteriaCommand?.Execute(null);
            }
        }

        public void OnApplyFiltersClick(object? sender, RoutedEventArgs e) {
            if (DataContext is MainWindowViewModel viewModel && viewModel.MainView?.SelectedTab != null) {
                viewModel.MainView.SelectedTab.ApplyFiltersCommand?.Execute(null);
            }
        }

        public void OnResetFiltersClick(object? sender, RoutedEventArgs e) {
            if (DataContext is MainWindowViewModel viewModel && viewModel.MainView?.SelectedTab != null) {
                viewModel.MainView.SelectedTab.ResetFiltersCommand?.Execute(null);
            }
        }

        public void OnAddIISFilterClick(object? sender, RoutedEventArgs e) {
            if (DataContext is MainWindowViewModel viewModel && viewModel.MainView?.SelectedTab != null) {
                viewModel.MainView.SelectedTab.AddIISFilterCriterionCommand?.Execute(null);
            }
        }

        public void OnApplyIISFiltersClick(object? sender, RoutedEventArgs e) {
            if (DataContext is MainWindowViewModel viewModel && viewModel.MainView?.SelectedTab != null) {
                viewModel.MainView.SelectedTab.ApplyIISFiltersCommand?.Execute(null);
            }
        }

        public void OnResetIISFiltersClick(object? sender, RoutedEventArgs e) {
            if (DataContext is MainWindowViewModel viewModel && viewModel.MainView?.SelectedTab != null) {
                viewModel.MainView.SelectedTab.ResetIISFiltersCommand?.Execute(null);
            }
        }

        public void OnAddRabbitMQFilterClick(object? sender, RoutedEventArgs e) {
            if (DataContext is MainWindowViewModel viewModel && viewModel.MainView?.SelectedTab != null) {
                viewModel.MainView.SelectedTab.AddRabbitMQFilterCriterionCommand?.Execute(null);
            }
        }

        public void OnApplyRabbitMQFiltersClick(object? sender, RoutedEventArgs e) {
            if (DataContext is MainWindowViewModel viewModel && viewModel.MainView?.SelectedTab != null) {
                viewModel.MainView.SelectedTab.ApplyRabbitMQFiltersCommand?.Execute(null);
            }
        }

        public void OnResetRabbitMQFiltersClick(object? sender, RoutedEventArgs e) {
            if (DataContext is MainWindowViewModel viewModel && viewModel.MainView?.SelectedTab != null) {
                viewModel.MainView.SelectedTab.ResetRabbitMQFiltersCommand?.Execute(null);
            }
        }

        private void MainWindow_Opened(object? sender, EventArgs e) {
            _mainContentGrid = this.Find<Grid>("MainContentGrid");

            if (DataContext is MainWindowViewModel viewModel && viewModel.MainView.FileTabs.Count == 0) {
                ShowEmptyState();
            }
        }

        private void FileTabs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
            if (DataContext is MainWindowViewModel viewModel) {
                UpdateContentVisibility(viewModel.MainView.FileTabs.Count);
            }
        }

        private void UpdateContentVisibility(int tabCount) {
            if (tabCount == 0) {
                ShowEmptyState();
            } else {
                HideEmptyState();
            }
        }

        private void ShowEmptyState() {
            if (_emptyStateView == null && _mainContentGrid != null) {
                _emptyStateView = new EmptyStateView();
                _emptyStateView.OpenLogFileRequested += EmptyStateView_OpenLogFileRequested;
                Grid.SetRow(_emptyStateView, 0);
                Grid.SetRowSpan(_emptyStateView, 3);
                Grid.SetColumn(_emptyStateView, 0);
                Grid.SetColumnSpan(_emptyStateView, 3);
                _mainContentGrid.Children.Add(_emptyStateView);
            }

            if (_emptyStateView != null) {
                _emptyStateView.IsVisible = true;
            }
        }

        private void HideEmptyState() {
            if (_emptyStateView != null) {
                _emptyStateView.IsVisible = false;
            }
        }

        private void EmptyStateView_OpenLogFileRequested(object? sender, EventArgs e) {
            var mainWindow = this;
            var dataContext = mainWindow.DataContext as MainWindowViewModel;
            if (dataContext?.MainView != null) {
                // Запускаем команду загрузки файла
                dataContext.MainView.LoadFileCommand?.Execute(null);
            }
        }

        public void OnStackTraceDoubleTapped(object? sender, TappedEventArgs e) {
            if (sender is TextBlock textBlock && textBlock.Parent is ScrollViewer scrollViewer) {
                if (scrollViewer.MaxHeight == 80) {
                    scrollViewer.MaxHeight = 200;
                    if (scrollViewer.Parent is Border border) {
                        border.Background = Avalonia.Media.Brushes.DarkSlateGray;
                        border.BorderBrush = Avalonia.Media.Brushes.Orange;
                    }
                } else {
                    scrollViewer.MaxHeight = 80;
                    if (scrollViewer.Parent is Border border) {
                        border.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1E1E1E"));
                        border.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#404040"));
                    }
                }
            }
        }

        public async void OnProcessUIDDoubleTapped(object? sender, TappedEventArgs e) {
            if (sender is TextBlock textBlock && !string.IsNullOrEmpty(textBlock.Text)) {
                try {
                    var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                    if (clipboard != null) {
                        await clipboard.SetTextAsync(textBlock.Text);
                        
                        // Visual feedback
                        var originalBrush = textBlock.Background;
                        textBlock.Background = Avalonia.Media.Brushes.Green;
                        await Task.Delay(200);
                        textBlock.Background = originalBrush;
                    }
                } catch (Exception ex) {
                    logger.Error(ex, "Failed to copy ProcessUID to clipboard");
                }
            }
        }

        public async void OnErrorMessageDoubleTapped(object? sender, TappedEventArgs e) {
            if (sender is TextBlock textBlock && !string.IsNullOrEmpty(textBlock.Text)) {
                try {
                    var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                    if (clipboard != null) {
                        await clipboard.SetTextAsync(textBlock.Text);
                        
                        // Visual feedback
                        var originalBrush = textBlock.Background;
                        textBlock.Background = Avalonia.Media.Brushes.LightBlue;
                        await Task.Delay(200);
                        textBlock.Background = originalBrush;
                    }
                } catch (Exception ex) {
                    logger.Error(ex, "Failed to copy Error Message to clipboard");
                }
            }
        }
    }
}

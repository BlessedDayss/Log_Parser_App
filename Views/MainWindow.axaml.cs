#pragma warning disable CS0103 // The name 'InitializeComponent' does not exist in the current context

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Log_Parser_App.ViewModels;
using System;
using NLog;
using System.IO;
using Avalonia.Input;

namespace Log_Parser_App.Views;

public partial class MainWindow : Window
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    private DateTime _lastTapTime = DateTime.MinValue;
    private object? _lastTappedItem = null;

    public MainWindow()
    {
        // Manual alternative to InitializeComponent
        AvaloniaXamlLoader.Load(this);

        // Логирование запуска окна
        logger.Info("MainWindow запущен.");

        this.AttachedToVisualTree += (_, _) =>
        {
            if (DataContext is MainWindowViewModel { MainView: not null } vm)
            {
                UpdateTheme(vm.MainView.IsDarkTheme);

                vm.MainView.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(vm.MainView.IsDarkTheme))
                    {
                        UpdateTheme(vm.MainView.IsDarkTheme);
                    }
                };
            }
            
            // Попытка обновления версии при успешном обновлении
            try
            {
                UpdateVersion();
                logger.Info("Версия успешно обновлена.");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Ошибка при обновлении версии.");
            }
        };
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
}
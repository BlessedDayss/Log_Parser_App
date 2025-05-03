using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Log_Parser_App.ViewModels;
using System;
using NLog;
using System.IO;

namespace Log_Parser_App.Views;

public partial class MainWindow : Window
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    public MainWindow()
    {
        InitializeComponent();

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
}
#pragma warning disable CS0103 // The name 'InitializeComponent' does not exist in the current context
namespace Log_Parser_App.Views
{
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Markup.Xaml;
    using Avalonia.Styling;
    using Log_Parser_App.ViewModels;
    using System;
    using NLog;
    using System.IO;


    public partial class MainWindow : Window
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public MainWindow() {
            AvaloniaXamlLoader.Load(this);
            _logger.Info("MainWindow запущен.");

            this.AttachedToVisualTree += (_, _) => {
                if (DataContext is MainWindowViewModel { MainView: not null } vm) {
                    UpdateTheme(vm.MainView.IsDarkTheme);

                    vm.MainView.PropertyChanged += (_, args) => {
                        if (args.PropertyName == nameof(vm.MainView.IsDarkTheme)) {
                            UpdateTheme(vm.MainView.IsDarkTheme);
                        }
                    };
                }
                try {
                    UpdateVersion();
                    _logger.Info("Версия успешно обновлена.");
                } catch (Exception ex) {
                    _logger.Error(ex, "Ошибка при обновлении версии.");
                }
            };
        }

        private static void UpdateTheme(bool isDarkTheme) {
            Application.Current!.RequestedThemeVariant = isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        private void UpdateVersion() {
            string versionFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VERSION.txt");
            if (File.Exists(versionFilePath)) {
                string version = File.ReadAllText(versionFilePath).Trim();
                var versionLabel = this.FindControl<TextBlock>("VersionLabel");
                if (versionLabel != null) {
                    versionLabel.Text = "v" + version;
                } else {
                    _logger.Warn("Элемент VersionLabel не найден в UI.");
                }
            } else {
                _logger.Warn($"Файл версии не найден: {versionFilePath}");
            }
        }
    }
}
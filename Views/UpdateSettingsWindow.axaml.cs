using Avalonia.Controls;
using Avalonia.Interactivity;
using Log_Parser_App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace Log_Parser_App.Views
{
    public partial class UpdateSettingsWindow : Window
    {
        public UpdateSettingsWindow()
        {
            InitializeComponent();
            
            // Получаем UpdateViewModel из DI контейнера
            if (App.ServiceProvider != null)
            {
                var updateViewModel = App.ServiceProvider.GetRequiredService<UpdateViewModel>();
                DataContext = updateViewModel;
                
                // Проверяем обновления при открытии окна
                _ = updateViewModel.CheckForUpdatesAsync();
            }
            
            // Добавляем обработчик клика на GitHub ссылку
            var gitHubLink = this.FindControl<TextBlock>("GitHubLink");
            if (gitHubLink != null)
            {
                gitHubLink.PointerPressed += GitHubLink_PointerPressed;
            }
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private void GitHubLink_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/BlessedDayss/Log_Parser_App",
                    UseShellExecute = true
                });
            }
            catch
            {
                // Если не удалось открыть браузер, игнорируем ошибку
            }
        }
    }
} 
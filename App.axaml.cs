using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using LogParserApp.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using Log_Parser_App.Services;
using LogParserApp.ViewModels;
using MainViewModel = Log_Parser_App.ViewModels.MainViewModel;
using MainWindow = Log_Parser_App.Views.MainWindow;

namespace LogParserApp;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Disable double validation
            DisableAvaloniaDataAnnotationValidation();
            
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            var services = serviceCollection.BuildServiceProvider();
            
            // Создаем главное окно и MainViewModel
            var mainViewModel = services.GetRequiredService<MainViewModel>();
            var logger = services.GetRequiredService<ILogger<MainWindowViewModel>>();
            
            var mainWindowViewModel = new MainWindowViewModel(logger, mainViewModel);
            
            MainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel
            };
            
            desktop.MainWindow = MainWindow;
            
            // Инициализируем FileService
            var fileService = services.GetRequiredService<IFileService>();
            if (fileService is FileService fs && MainWindow != null)
            {
                fs.InitializeTopLevel(MainWindow);
            }
            
            // Логируем запуск
            var appLogger = services.GetService<ILogger<App>>();
            appLogger?.LogInformation("Application started");
            
            // Настраиваем обработку завершения
            desktop.ShutdownRequested += (sender, args) => {
                appLogger?.LogInformation("Application shutdown requested");
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    private void ConfigureServices(ServiceCollection services)
    {
        // Configure logging
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LogParserApp", "logs", "app.log");
            
        // Create log directory if it doesn't exist
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            
            // Enable file logging with Debug level
            // builder.AddFile("Logs/LogParser-{Date}.txt");
            
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        
        // Register services
        RegisterServices(services);
        
        // Register view models
        RegisterViewModels(services);
    }
    
    private void RegisterServices(ServiceCollection services)
    {
        services.AddSingleton<ILogParserService, LogParserService>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IErrorRecommendationService, ErrorRecommendationService>();
    }
    
    private void RegisterViewModels(ServiceCollection services)
    {
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindowViewModel>();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}

// Класс для управления сервисами
// public static class ServiceManager<ServiceProvider>
// {
//     private static ServiceProvider? _provider;
//     
//     public static ServiceProvider Provider 
//     { 
//         get 
//         {
//             if (_provider == null)
//                 throw new InvalidOperationException("Service provider not initialized");
//                 
//             return _provider;
//         }
//     }
//     
//     public static void Initialize(ServiceProvider serviceProvider)
//     {
//         _provider = serviceProvider;
//     }
// }
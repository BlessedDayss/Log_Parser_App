using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Log_Parser_App.Services;
using Log_Parser_App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MainViewModel = Log_Parser_App.ViewModels.MainViewModel;
using MainWindow = Log_Parser_App.Views.MainWindow;

namespace Log_Parser_App;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }
    public static IServiceProvider? Services { get; private set; }
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            var services = serviceCollection.BuildServiceProvider();
            Services = services;
            
            var updateViewModel = services.GetService<Log_Parser_App.ViewModels.UpdateViewModel>();
            if (updateViewModel != null)
            {
                Task.Run(async () => await updateViewModel.CheckForUpdatesOnStartupAsync());
            }
            
            var mainViewModel = services.GetRequiredService<MainViewModel>();
            var logger = services.GetRequiredService<ILogger<MainWindowViewModel>>();
            
            var updateService = services.GetRequiredService<IUpdateService>();
            var mainWindowViewModel = new MainWindowViewModel(logger, mainViewModel, updateService);
            
            MainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel
            };
            
            desktop.MainWindow = MainWindow;
            
            var fileService = services.GetRequiredService<IFileService>();
            if (fileService is FileService fs && MainWindow != null)
            {
                fs.InitializeTopLevel(MainWindow);
            }
            
            // Регистрируем ассоциации файлов, если приложение запущено в Windows
            if (OperatingSystem.IsWindows())
            {
                var fileAssociationService = services.GetRequiredService<IFileAssociationService>();
                Task.Run(async () =>
                {
                    // Проверяем, зарегистрированы ли уже ассоциации
                    if (!await fileAssociationService.AreFileAssociationsRegisteredAsync())
                    {
                        await fileAssociationService.RegisterFileAssociationsAsync();
                    }
                });
            }
            
            var appLogger = services.GetService<ILogger<App>>();
            appLogger?.LogInformation("Application started");
            
            desktop.ShutdownRequested += (sender, args) => {
                appLogger?.LogInformation("Application shutdown requested");
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    private void ConfigureServices(ServiceCollection services)
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LogParserApp", "logs", "app.log");
            
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        
        RegisterServices(services);
        
        RegisterViewModels(services);
    }
    
    private void RegisterServices(ServiceCollection services)
    {
        services.AddSingleton<ILogParserService, LogParserService>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IErrorRecommendationService, ErrorRecommendationService>();
        
        // Регистрируем сервис ассоциаций файлов
        services.AddSingleton<IFileAssociationService, WindowsFileAssociationService>();
        
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<IUpdateService>(provider => 
            new GitHubUpdateService(
                provider.GetRequiredService<ILogger<GitHubUpdateService>>(),
                "BlessedDayss", 
                "Log_Parser_App"  
            ));
        services.AddSingleton<UpdateViewModel>();
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


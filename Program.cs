using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using Log_Parser_App.Services;
using LogParserApp.ViewModels;
using MainViewModel = Log_Parser_App.ViewModels.MainViewModel;

namespace LogParserApp;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
            
        ConfigureServices(builder);
            
        return builder;
    }
    
    private static void ConfigureServices(AppBuilder builder)
    {
        var services = new ServiceCollection();
        
        // Регистрация логгера
        services.AddLogging(configure => configure.AddConsole());
        
        // Регистрация сервисов
        services.AddSingleton<ILogParserService, LogParserService>();
        services.AddSingleton<IErrorRecommendationService, ErrorRecommendationService>();
        
        // Регистрация ViewModel
        services.AddSingleton<MainViewModel>();
        
        // Регистрация FileService с отложенной инициализацией TopLevel
        services.AddSingleton<IFileService>(provider => 
        {
            return new FileService(
                provider.GetRequiredService<ILogger<FileService>>(),
                null    // Будет инициализирован позже
            );
        });
    }
}

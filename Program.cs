using System;
using Avalonia;
using Log_Parser_App;
using Log_Parser_App.Services;
// Removed LogParserApp reference
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MainViewModel = Log_Parser_App.ViewModels.MainViewModel;
using UpdateViewModel = Log_Parser_App.ViewModels.UpdateViewModel;

namespace Log_Parser_App;

internal abstract class Program
{
    // Сохраняем аргументы командной строки для использования в приложении
    public static string[] StartupArgs { get; private set; } = Array.Empty<string>();

    [STAThread]
    public static void Main(string[] args)
    {
        // Настраиваем кодировку консоли для корректного отображения кириллицы
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        
        // Сохраняем аргументы для использования позже
        StartupArgs = args;
        
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
            
        ConfigureServices();
            
        return builder;
    }
    
    private static void ConfigureServices()
    {
        var services = new ServiceCollection();
        
        services.AddLogging(configure => configure.AddConsole());
        services.AddSingleton<ILogParserService, LogParserService>();
        services.AddSingleton<IErrorRecommendationService, ErrorRecommendationService>();
        services.AddSingleton<MainViewModel>();
        
        services.AddSingleton<IFileService>(provider => new FileService(
            provider.GetRequiredService<ILogger<FileService>>()   
        ));

        services.AddSingleton<IUpdateService>(provider => 
            new GitHubUpdateService(
                provider.GetRequiredService<ILogger<GitHubUpdateService>>(),
                "BlessedDayss", 
                "Log_Parser_App"  
            ));

        services.AddSingleton<UpdateViewModel>();
    }
}

using System;
using Avalonia;
using Log_Parser_App;
using Log_Parser_App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MainViewModel = Log_Parser_App.ViewModels.MainViewModel;
using UpdateViewModel = Log_Parser_App.ViewModels.UpdateViewModel;
using Log_Parser_App.Models.Interfaces;

namespace Log_Parser_App;

internal abstract class Program
{
    public static string[] StartupArgs { get; private set; } = Array.Empty<string>();

    [STAThread]
    public static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        
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
        services.AddSingleton<IIISLogParserService, IISLogParserService>();
        services.AddSingleton<MainViewModel>();
        
        services.AddSingleton<IFileService>(provider => new FileService(
            provider.GetRequiredService<ILogger<FileService>>()   
        ));

        services.AddSingleton<Log_Parser_App.Models.Interfaces.IUpdateService>(provider => 
            new GitHubUpdateService(
                provider.GetRequiredService<ILogger<GitHubUpdateService>>(),
                "BlessedDayss", 
                "Log_Parser_App"  
            ));

        services.AddSingleton<UpdateViewModel>();
    }
}

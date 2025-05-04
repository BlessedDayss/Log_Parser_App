namespace Log_Parser_App
{
    using System;
    using System.Net.Http;
    using Avalonia;
    using Services;
    using Services.Interfaces;
    using Services.UpdateStrategies;
    using Services.VersionParsers;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using MainViewModel = ViewModels.MainViewModel;
    using UpdateViewModel = ViewModels.UpdateViewModel;



    internal abstract class Program
    {
        public static string[] StartupArgs { get; private set; } = [
        ];

        [STAThread]
        public static void Main(string[] args) {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            StartupArgs = args;
            
            var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("ВАЖНО: Установите токен GitHub в переменной окружения GITHUB_TOKEN");
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        private static AppBuilder BuildAvaloniaApp() {
            var builder = AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();

            ConfigureServices();

            return builder;
        }

        private static void ConfigureServices() {
            var services = new ServiceCollection();
            services.AddLogging(configure => configure.AddConsole());
            services.AddSingleton<ILogParserService, LogParserService>();
            services.AddSingleton<IVersionParser, GitHubVersionParser>();
services.AddHttpClient();
services.AddSingleton<IGitHubUpdateStrategy>(provider => new DefaultGitHubUpdateStrategy(
    provider.GetRequiredService<HttpClient>(),
    provider.GetRequiredService<ILogger<DefaultGitHubUpdateStrategy>>(),
    provider.GetRequiredService<IVersionParser>(),
    "BlessedDayss",
    "Log_Parser_App"
));
services.AddSingleton<IGitHubConnectionService>(provider => new GitHubUpdateService(
    provider.GetRequiredService<ILogger<GitHubUpdateService>>(),
    provider.GetRequiredService<HttpClient>(),
    provider.GetRequiredService<IGitHubUpdateStrategy>(),
    "BlessedDayss",
    "Log_Parser_App"
));
            services.AddSingleton<IErrorRecommendationService, ErrorRecommendationService>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<IFileService>(provider => new FileService(provider.GetRequiredService<ILogger<FileService>>()));
            services.AddSingleton<IUpdateService>(provider => new GitHubUpdateService(
                provider.GetRequiredService<ILogger<GitHubUpdateService>>(),
                provider.GetRequiredService<HttpClient>(),
                provider.GetRequiredService<IGitHubUpdateStrategy>(),
                "BlessedDayss",
                "Log_Parser_App"
            ));
            services.AddSingleton<UpdateViewModel>();
        }
    }
}
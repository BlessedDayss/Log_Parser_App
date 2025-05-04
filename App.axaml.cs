namespace Log_Parser_App
{
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


    public partial class App : Application
    {
        public static Window? MainWindow { get; private set; }

        public override void Initialize() {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted() {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                DisableAvaloniaDataAnnotationValidation();

                var serviceCollection = new ServiceCollection();
                ConfigureServices(serviceCollection);
                var services = serviceCollection.BuildServiceProvider();

                var updateViewModel = services.GetService<UpdateViewModel>();
                if (updateViewModel != null) {
                    Task.Run(async () => await updateViewModel.CheckForUpdatesOnStartupAsync());
                }

                var mainViewModel = services.GetRequiredService<MainViewModel>();
                var logger = services.GetRequiredService<ILogger<MainWindowViewModel>>();

                var updateService = services.GetRequiredService<IUpdateService>();
                var mainWindowViewModel = new MainWindowViewModel(logger, mainViewModel, updateService);

                MainWindow = new MainWindow {
                    DataContext = mainWindowViewModel
                };

                desktop.MainWindow = MainWindow;

                var fileService = services.GetRequiredService<IFileService>();
                if (fileService is FileService fs) {
                    fs.InitializeTopLevel(MainWindow);
                }

                if (OperatingSystem.IsWindows()) {
                    var fileAssociationService = services.GetRequiredService<IFileAssociationService>();
                    Task.Run(async () => {
                        if (!await fileAssociationService.AreFileAssociationsRegisteredAsync()) {
                            await fileAssociationService.RegisterFileAssociationsAsync();
                        }
                    });
                }

                var appLogger = services.GetService<ILogger<App>>();
                appLogger?.LogInformation("Application started");

                desktop.ShutdownRequested += (sender, _) => {
                    appLogger?.LogInformation("Application shutdown requested");
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static void ConfigureServices(ServiceCollection services) {
            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LogParserApp", "logs", "app.log");

            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            services.AddLogging(builder => {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            RegisterServices(services);

            RegisterViewModels(services);
        }

        private static void RegisterServices(ServiceCollection services) {
            services.AddSingleton<ILogParserService, LogParserService>();
            services.AddSingleton<IFileService, FileService>();
            services.AddSingleton<IErrorRecommendationService, ErrorRecommendationService>();
            services.AddSingleton<IFileAssociationService, WindowsFileAssociationService>();
            services.AddSingleton<IUpdateService, UpdateService>();
            services.AddSingleton<IUpdateService>(provider => new GitHubUpdateService(provider.GetRequiredService<ILogger<GitHubUpdateService>>(), "BlessedDayss", "Log_Parser_App"));
            services.AddSingleton<UpdateViewModel>();
        }

        private static void RegisterViewModels(ServiceCollection services) {
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindowViewModel>();
        }

        private static void DisableAvaloniaDataAnnotationValidation() {
            var dataValidationPluginsToRemove = BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            foreach (var plugin in dataValidationPluginsToRemove) {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}
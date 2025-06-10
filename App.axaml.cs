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
using Log_Parser_App.Models.Interfaces;

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
        Console.WriteLine("[App] OnFrameworkInitializationCompleted started.");
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Console.WriteLine("[App] ApplicationLifetime is IClassicDesktopStyleApplicationLifetime.");
            DisableAvaloniaDataAnnotationValidation();
            Console.WriteLine("[App] AvaloniaDataAnnotationValidation disabled.");
            
            var serviceCollection = new ServiceCollection();
            Console.WriteLine("[App] ServiceCollection created.");
            ConfigureServices(serviceCollection);
            Console.WriteLine("[App] ConfigureServices(serviceCollection) called.");
            var services = serviceCollection.BuildServiceProvider();
            Services = services;
            Console.WriteLine("[App] ServiceProvider built and assigned.");
            
            Log_Parser_App.ViewModels.UpdateViewModel? updateViewModel = null;
            try
            {
                updateViewModel = services.GetService<Log_Parser_App.ViewModels.UpdateViewModel>();
                Console.WriteLine(updateViewModel == null ? "[App] UpdateViewModel NOT resolved." : "[App] UpdateViewModel resolved.");
            }
            catch (Exception ex) { Console.WriteLine($"[App] Error resolving UpdateViewModel: {ex.Message}"); }

            if (updateViewModel != null)
            {
                Console.WriteLine("[App] Starting UpdateViewModel.CheckForUpdatesOnStartupAsync().");
                Task.Run(async () => 
                {
                    try 
                    {
                        await updateViewModel.CheckForUpdatesOnStartupAsync();
                        Console.WriteLine("[App] UpdateViewModel.CheckForUpdatesOnStartupAsync() completed.");
                    }
                    catch (Exception ex_task) { Console.WriteLine($"[App] Error in CheckForUpdatesOnStartupAsync task: {ex_task.Message}"); }
                });
            }
            
            MainViewModel? mainViewModel = null;
            try
            {
                mainViewModel = services.GetRequiredService<MainViewModel>();
                Console.WriteLine("[App] MainViewModel resolved.");
            }
            catch (Exception ex) { Console.WriteLine($"[App] Error resolving MainViewModel: {ex.Message}"); }

            ILogger<MainWindowViewModel>? logger = null;
            try
            {
                logger = services.GetRequiredService<ILogger<MainWindowViewModel>>();
                Console.WriteLine("[App] ILogger<MainWindowViewModel> resolved.");
            }
            catch (Exception ex) { Console.WriteLine($"[App] Error resolving ILogger<MainWindowViewModel>: {ex.Message}"); }
            
            Log_Parser_App.Models.Interfaces.IUpdateService? updateService = null;
            try
            {
                updateService = services.GetRequiredService<Log_Parser_App.Models.Interfaces.IUpdateService>();
                Console.WriteLine("[App] IUpdateService resolved.");
            }
            catch (Exception ex) { Console.WriteLine($"[App] Error resolving IUpdateService: {ex.Message}"); }

            MainWindowViewModel? mainWindowViewModel = null;
            if (logger != null && mainViewModel != null && updateService != null)
            {
                try
                {
                    mainWindowViewModel = new MainWindowViewModel(logger, mainViewModel, updateService);
                    Console.WriteLine("[App] MainWindowViewModel created.");
                }
                catch (Exception ex) { Console.WriteLine($"[App] Error creating MainWindowViewModel: {ex.Message}"); }
            }
            else
            {
                Console.WriteLine("[App] Skipping MainWindowViewModel creation due to missing dependencies.");
            }
            
            // Создаем экран приветствия и показываем его
            var splashScreen = new Views.SplashScreen();
            desktop.MainWindow = splashScreen;
            
            // Запускаем задачу, которая покажет основное окно после задержки
            Task.Run(async () =>
            {
                try
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => splashScreen.UpdateStatus("Loading..."));
                    
                    await Task.Delay(3000);

                    // Переключаемся на UI поток для создания основного окна
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        try
                        {
                            MainWindow = new MainWindow
                            {
                                DataContext = mainWindowViewModel
                            };
                            Console.WriteLine("[App] MainWindow created and DataContext set.");
                            
                            desktop.MainWindow = MainWindow;
                            MainWindow.Show();
                            splashScreen.Close();
                            Console.WriteLine("[App] desktop.MainWindow assigned to MainWindow.");
                            
                            // Process command line arguments for file opening after UI is ready
                            if (mainViewModel != null)
                            {
                                Console.WriteLine("[App] Processing command line arguments for file opening...");
                                // Call CheckCommandLineArgs via reflection to avoid making it public
                                var checkMethod = typeof(MainViewModel).GetMethod("CheckCommandLineArgs", 
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                checkMethod?.Invoke(mainViewModel, null);
                            }
                            
                            // Инициализируем FileService после создания основного окна
                            IFileService? fileService = null;
                            try 
                            {
                                fileService = services.GetRequiredService<IFileService>();
                                Console.WriteLine("[App] IFileService resolved.");
                            }
                            catch (Exception ex) { Console.WriteLine($"[App] Error resolving IFileService: {ex.Message}"); }

                            if (fileService is FileService fs && MainWindow != null)
                            {
                                try
                                {
                                    fs.InitializeTopLevel(MainWindow);
                                    Console.WriteLine("[App] FileService.InitializeTopLevel called.");
                                }
                                catch (Exception ex) { Console.WriteLine($"[App] Error in FileService.InitializeTopLevel: {ex.Message}"); }
                            }
                        }
                        catch (Exception ex) { Console.WriteLine($"[App] Error creating MainWindow: {ex.Message}"); }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[App] Error in splash screen task: {ex.Message}");
                    
                    // Если произошла ошибка в задаче SplashScreen, показываем основное окно напрямую
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        try
                        {
                            MainWindow = new MainWindow
                            {
                                DataContext = mainWindowViewModel
                            };
                            desktop.MainWindow = MainWindow;
                            MainWindow.Show();
                            splashScreen.Close();
                        }
                        catch (Exception ex2) { Console.WriteLine($"[App] Error in fallback window creation: {ex2.Message}"); }
                    });
                }
            });
            
            if (OperatingSystem.IsWindows())
            {
                Console.WriteLine("[App] OperatingSystem is Windows. Setting up FileAssociationService.");
                IFileAssociationService? fileAssociationService = null;
                try
                {
                    fileAssociationService = services.GetRequiredService<IFileAssociationService>();
                    Console.WriteLine("[App] IFileAssociationService resolved.");
                }
                catch (Exception ex) { Console.WriteLine($"[App] Error resolving IFileAssociationService: {ex.Message}"); }

                if (fileAssociationService != null)
                {
                    Console.WriteLine("[App] Starting FileAssociationService task.");
                    Task.Run(async () =>
                    {
                        try
                        {
                            if (!await fileAssociationService.AreFileAssociationsRegisteredAsync())
                            {
                                Console.WriteLine("[App] File associations not registered. Registering...");
                                await fileAssociationService.RegisterFileAssociationsAsync();
                                Console.WriteLine("[App] File associations registration attempt completed.");
                            }
                            else
                            {
                                Console.WriteLine("[App] File associations already registered.");
                            }
                        }
                        catch (Exception ex_task) { Console.WriteLine($"[App] Error in FileAssociationService task: {ex_task.Message}"); }
                    });
                }
            }
            
            ILogger<App>? appLogger = null;
            try
            {
                appLogger = services.GetService<ILogger<App>>();
                Console.WriteLine(appLogger == null ? "[App] ILogger<App> NOT resolved." : "[App] ILogger<App> resolved.");
            }
            catch (Exception ex) { Console.WriteLine($"[App] Error resolving ILogger<App>: {ex.Message}"); }
            
            appLogger?.LogInformation("Application started (via logger).");
            Console.WriteLine("[App] Application startup sequence in OnFrameworkInitializationCompleted nearing end.");
            
            desktop.ShutdownRequested += (sender, args) => {
                appLogger?.LogInformation("Application shutdown requested (via logger)");
                Console.WriteLine("[App] Application shutdown requested.");
            };
        }
        else
        {
            Console.WriteLine("[App] ApplicationLifetime is NOT IClassicDesktopStyleApplicationLifetime.");
        }

        Console.WriteLine("[App] Calling base.OnFrameworkInitializationCompleted().");
        base.OnFrameworkInitializationCompleted();
        Console.WriteLine("[App] OnFrameworkInitializationCompleted finished.");
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
        services.AddSingleton<IIISLogParserService, IISLogParserService>();
        
        services.AddSingleton<ILogFileLoader, LogFileLoader>();
        services.AddSingleton<ILogFilesLoader, LogFilesLoader>();
        services.AddSingleton<StandardLogLineParser>();
        services.AddSingleton<SimpleLogLineParser>();
        services.AddSingleton<Log_Parser_App.Models.Interfaces.ILogLineParser>(provider =>
            new LogLineParserChain([
                provider.GetRequiredService<StandardLogLineParser>(),
                provider.GetRequiredService<SimpleLogLineParser>()
            ])
        );
        // FilePickerService registration updated
        services.AddSingleton<Log_Parser_App.Models.Interfaces.IFilePickerService, Log_Parser_App.Services.FilePickerService>();
        
        // Регистрируем сервис ассоциаций файлов
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IFileAssociationService, WindowsFileAssociationService>();
        }
        
        services.AddSingleton<Log_Parser_App.Models.Interfaces.IUpdateService>(provider =>
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
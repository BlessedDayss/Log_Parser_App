namespace Log_Parser_App
{
    using System;
    using Avalonia;
    using Log_Parser_App.Services;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using MainViewModel = Log_Parser_App.ViewModels.MainViewModel;
    using UpdateViewModel = Log_Parser_App.ViewModels.UpdateViewModel;
    using Log_Parser_App.Models.Interfaces;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    internal abstract class Program
    {
        public static string[]? StartupArgs { get; set; }
        public static string? StartupFilePath { get; private set; }

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                StartupArgs = args;
                
                // Process command line arguments for file opening
                ProcessCommandLineArguments(args);

                // Test parsing logic if --test-parsing argument is provided
                if (args.Length > 0 && args[0] == "--test-parsing")
                {
                    // TODO: Implement test parsing functionality
                    Console.WriteLine("Test parsing functionality not implemented yet.");
                    return;
                }

                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Program.Main] Unhandled exception: {ex}");
                Environment.Exit(1);
            }
        }

        private static void ProcessCommandLineArguments(string[] args)
        {
            if (args.Length == 0)
                return;

            // Skip special arguments like --test-parsing
            if (args[0].StartsWith("--"))
                return;

            // First argument should be file path
            var filePath = args[0];
            
            // Validate file exists
            if (System.IO.File.Exists(filePath))
            {
                StartupFilePath = System.IO.Path.GetFullPath(filePath);
                Console.WriteLine($"[Program] Startup file detected: {StartupFilePath}");
            }
            else
            {
                Console.WriteLine($"[Program] Invalid startup file path: {filePath}");
            }
        }

        public static string? DetermineLogParserType(string filePath)
        {
            // Все файлы обрабатываются как стандартные логи
            return "Standard";
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
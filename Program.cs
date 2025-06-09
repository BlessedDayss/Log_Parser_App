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

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static async Task Main(string[] args)
        {
            try
            {
                StartupArgs = args;

                // Test parsing logic if --test-parsing argument is provided
                if (args.Length > 0 && args[0] == "--test-parsing")
                {
                    await TestLogParsing.RunTest();
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

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
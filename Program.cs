using System;
using Avalonia;

namespace Log_Parser_App
{
	internal abstract class Program
	{
		public static string[]? StartupArgs { get; set; }
		public static string? StartupFilePath { get; private set; }

		[STAThread]
		public static int Main(string[] args)
		{
			try
			{
				StartupArgs = args;
				ProcessCommandLineArguments(args);
				return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[Program.Main] Unhandled exception: {ex}");
				return 1;
			}
		}

		public static string? DetermineLogParserType(string filePath)
		{
			return "Standard";
		}

		public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();

		private static void ProcessCommandLineArguments(string[] args)
		{
			if (args.Length == 0)
				return;
			if (args[0].StartsWith("--"))
				return;
			string filePath = args[0];
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
	}
}

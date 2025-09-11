namespace Log_Parser_App
{
	using System;
	using Avalonia;
using System.Threading.Tasks;

	#region Class: Program

	internal abstract class Program
	{

		#region Properties: Public

		public static string[]? StartupArgs { get; set; }

		public static string? StartupFilePath { get; private set; }

		#endregion

		// Initialization code. Don't use any Avalonia, third-party APIs or any
		// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
		// yet and stuff might break.

		#region Methods: Public

		[STAThread]
		public static async Task<int> Main(string[] args) {
			try {
				StartupArgs = args;
				// Process command line arguments for file opening
				ProcessCommandLineArguments(args);
				return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
			} catch (Exception ex) {
				Console.WriteLine($"[Program.Main] Unhandled exception: {ex}");
				return 1;
			}
		}

		public static string? DetermineLogParserType(string filePath) {
			return "Standard";
		}

		// Avalonia configuration, don't remove; also used by visual designer.

		public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();

		#endregion

		#region Methods: Private

		private static void ProcessCommandLineArguments(string[] args) {
			if (args.Length == 0)
				return;
			// Skip special arguments like --test-parsing or --perf-test
			if (args[0].StartsWith("--"))
				return;
			// First argument should be file path
			string filePath = args[0];
			// Validate file exists
			if (System.IO.File.Exists(filePath)) {
				StartupFilePath = System.IO.Path.GetFullPath(filePath);
				Console.WriteLine($"[Program] Startup file detected: {StartupFilePath}");
			} else {
				Console.WriteLine($"[Program] Invalid startup file path: {filePath}");
			}
		}

		#endregion

	}

	#endregion

}

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
				// Check for performance test argument
				if (args.Length > 0 && args[0] == "--perf-test") {
					Console.WriteLine("Running Performance Tests...");
					var perfTest = new PerformanceTest();
					await perfTest.RunPerformanceTests();
					return 0;
				}
				// Check for RabbitMQ filtering performance test
				if (args.Length > 0 && args[0] == "--rabbitmq-filter-test") {
					Console.WriteLine("=== RabbitMQ Filtering Performance Test Starting ===");
					try {
						await PerformanceTest.TestRabbitMQFilteringPerformanceAsync();
						Console.WriteLine("=== RabbitMQ Filtering Performance Test Complete ===");
					} catch (Exception ex) {
						Console.WriteLine($"=== Test Failed: {ex.Message} ===");
						Console.WriteLine($"Stack trace: {ex.StackTrace}");
					}
					return 0;
				}
				// Test parsing logic if --test-parsing argument is provided
				if (args.Length > 0 && args[0] == "--test-parsing") {
					Console.WriteLine("Testing RabbitMQ header parsing...");
					await TestRabbitMqHeaderParsing();
					Console.WriteLine("Test completed.");
					return 0;
				}

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

		private static async Task TestRabbitMqHeaderParsing() {
			try {
				// Simple test without complex logging setup
				Console.WriteLine("🧪 Testing header parsing with test file...");
				
				// Just run a simple JSON parsing test
				if (System.IO.File.Exists("test-headers.json")) {
					var content = await System.IO.File.ReadAllTextAsync("test-headers.json");
					using var doc = System.Text.Json.JsonDocument.Parse(content);
					
					Console.WriteLine("📁 Test file found and parsed");
					
					// Check for required fields
					bool hasFaultMessage = doc.RootElement.TryGetProperty("MT-Fault-Message", out var msgElement);
					bool hasStackTrace = doc.RootElement.TryGetProperty("MT-Fault-StackTrace", out var stackElement);
					bool hasTimestamp = doc.RootElement.TryGetProperty("MT-Fault-Timestamp", out var timeElement);
					
					Console.WriteLine($"   MT-Fault-Message found: {hasFaultMessage}");
					Console.WriteLine($"   MT-Fault-StackTrace found: {hasStackTrace}");
					Console.WriteLine($"   MT-Fault-Timestamp found: {hasTimestamp}");
					
					if (hasFaultMessage) {
						var message = msgElement.GetString();
						Console.WriteLine($"   Message preview: {message?.Substring(0, Math.Min(100, message?.Length ?? 0))}...");
					}
					
					if (hasStackTrace) {
						var stack = stackElement.GetString();
						Console.WriteLine($"   StackTrace length: {stack?.Length ?? 0} characters");
					}
				} else {
					Console.WriteLine("❌ Test file 'test-headers.json' not found");
				}
			} catch (Exception ex) {
				Console.WriteLine($"❌ Test failed with exception: {ex.Message}");
			}
		}

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

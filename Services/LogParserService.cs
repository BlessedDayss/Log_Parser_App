namespace Log_Parser_App.Services
{
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using Log_Parser_App.Models;
	using Log_Parser_App.Models.Interfaces;
	using Microsoft.Extensions.Logging;
	using System.IO; // Required for Path and Directory operations
	using System.Runtime.CompilerServices; // Added
	using System.Threading; // Added

	public partial class LogParserService(ILogger<LogParserService> logger, ILogLineParser lineParser, ILogFileLoader fileLoader, ILogFilesLoader filesLoader) : ILogParserService
	{
		private readonly ILogger<LogParserService> _logger = logger;
		private const string ErrorLevel = "ERROR";

		// Modified method
		public async IAsyncEnumerable<LogEntry> ParseLogFileAsync(string filePath, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
			var lines = fileLoader.LoadLinesAsync(filePath); // Returns IAsyncEnumerable<string>
			
			static async IAsyncEnumerable<(string filePath, string line)> GetFileLinesWithContext(string p, IAsyncEnumerable<string> lns, [EnumeratorCancellation] CancellationToken ct)
			{
				await foreach (var l in lns.WithCancellation(ct))
				{
					yield return (p, l);
				}
			}
			var linesWithContext = GetFileLinesWithContext(filePath, lines, cancellationToken);

			await foreach (var entry in ParseLines(linesWithContext, cancellationToken).WithCancellation(cancellationToken)) {
				yield return entry;
			}
		}

		// Modified method
		public async IAsyncEnumerable<LogEntry> ParseLogFilesAsync(IEnumerable<string> filePaths, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
			var lines = filesLoader.LoadLinesAsync(filePaths); // Returns IAsyncEnumerable<(string filePath, string line)>
			await foreach (var entry in ParseLines(lines, cancellationToken).WithCancellation(cancellationToken)) {
				yield return entry;
			}
		}

		// Modified method
		public async IAsyncEnumerable<LogEntry> ParseLogDirectoryAsync(string directoryPath, string searchPattern = "*.log", int? maxFilesToParse = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
			var allFilePaths = Directory.EnumerateFiles(directoryPath, searchPattern, SearchOption.TopDirectoryOnly);
            
            var filesToParse = maxFilesToParse.HasValue 
                ? allFilePaths.Take(maxFilesToParse.Value)
                : allFilePaths;

            var lines = filesLoader.LoadLinesAsync(filesToParse); // Returns IAsyncEnumerable<(string filePath, string line)>
			await foreach (var entry in ParseLines(lines, cancellationToken).WithCancellation(cancellationToken)) {
				yield return entry;
			}
		}


		public Task<IEnumerable<LogEntry>> FilterErrorsAsync(IEnumerable<LogEntry> logEntries) {
			var filteredEntries = logEntries.Where(e => e.Level.Trim().Equals(ErrorLevel, System.StringComparison.InvariantCultureIgnoreCase)).ToList();
			return Task.FromResult<IEnumerable<LogEntry>>(filteredEntries);
		}

		// Modified method
		private async IAsyncEnumerable<LogEntry> ParseLines(IAsyncEnumerable<(string filePath, string line)> lines, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
			var lastErrorEntryByFile = new Dictionary<string, LogEntry?>();
			var lineNumberByFile = new Dictionary<string, int>();
			var errorKeywords = new[] { "error", "exception", "not found", "failed", "timeout", "critical", "fatal" };
			var timeRegex = new System.Text.RegularExpressions.Regex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}");
			var errorWordRegex = new System.Text.RegularExpressions.Regex(@"\\berror\\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
			
			await foreach (var (filePath, line) in lines.WithCancellation(cancellationToken)) {
				cancellationToken.ThrowIfCancellationRequested(); // Check for cancellation

				lineNumberByFile.TryAdd(filePath, 0);
				lineNumberByFile[filePath]++;
				int lineNumber = lineNumberByFile[filePath];
				
				bool containsErrorKeyword = errorWordRegex.IsMatch(line) || errorKeywords.Skip(1).Any(k => line.Contains(k, System.StringComparison.OrdinalIgnoreCase));

				if (lineParser.IsLogLine(line)) {
					var entry = lineParser.Parse(line, lineNumber, filePath);
					if (entry == null)
						continue;
                    
					if (containsErrorKeyword) 
                    {
						entry.Level = "ERROR";
                    }

					yield return entry; // Changed
					if (entry.Level.Trim().Equals(ErrorLevel, System.StringComparison.InvariantCultureIgnoreCase))
						lastErrorEntryByFile[filePath] = entry;
					else
						lastErrorEntryByFile[filePath] = null;
				} else {
					bool handledAsStackTrace = false;
                    if (lastErrorEntryByFile.TryGetValue(filePath, out var lastErrorEntry) && lastErrorEntry != null) {
						if (!timeRegex.IsMatch(line)) {
							AppendStackTrace(lastErrorEntry, line);
                            handledAsStackTrace = true;
						}
					}
                    
                    if (!handledAsStackTrace) {
                        string levelForUnparsedLine = containsErrorKeyword ? "ERROR" : "INFO";
                        var unparsedEntry = new LogEntry { // Changed
                            Timestamp = System.DateTime.Now,
                            Level = levelForUnparsedLine, 
                            Message = line.Trim(),
                            RawData = line,
                            FilePath = filePath,
                            LineNumber = lineNumber
                        };
                        yield return unparsedEntry; // Changed
                        
                        if (levelForUnparsedLine == "ERROR") {
                            lastErrorEntryByFile[filePath] = unparsedEntry;
                        } else {
                            lastErrorEntryByFile[filePath] = null;
                        }
                    }
				}
			}
			// _logger.LogDebug($"[ParseLines] Completed parsing lines."); // Logging individual entries might be too verbose.
		}

		private static void AppendStackTrace(LogEntry entry, string line) {
			entry.StackTrace = string.IsNullOrEmpty(entry.StackTrace) ? line : $"{entry.StackTrace}\n{line}";
		}

		public Task<IEnumerable<LogEntry>> ExecuteQueryAsync(IEnumerable<LogEntry> logEntries, string query) {
			return Task.FromResult(logEntries);
		}

		public Task<string> DetectLogFormatAsync(string filePath) {
			return Task.FromResult("Standard");
		}

		public Task<IEnumerable<PackageLogEntry>> ParsePackageLogFileAsync(string filePath) {
			return Task.FromResult<IEnumerable<PackageLogEntry>>(new List<PackageLogEntry>());
		}
	}
}
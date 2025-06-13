namespace Log_Parser_App.Services
{
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using Log_Parser_App.Models;
	using Log_Parser_App.Models.Interfaces;
	using Microsoft.Extensions.Logging;
	using System.IO;
	using System.Runtime.CompilerServices;
	using System.Threading;

	public partial class LogParserService(ILogger<LogParserService> logger, ILogLineParser lineParser, ILogFileLoader fileLoader, ILogFilesLoader filesLoader) : ILogParserService
	{
		private readonly ILogger<LogParserService> _logger = logger;
		private const string ErrorLevel = "ERROR";

		// Modified method
		public async IAsyncEnumerable<LogEntry> ParseLogFileAsync(string filePath, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
			var lines = fileLoader.LoadLinesAsync(filePath); // Returns IAsyncEnumerable<string>

			static async IAsyncEnumerable<(string filePath, string line)> GetFileLinesWithContext(string p, IAsyncEnumerable<string> lns, [EnumeratorCancellation] CancellationToken ct) {
				await foreach (var l in lns.WithCancellation(ct)) {
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
		public async IAsyncEnumerable<LogEntry> ParseLogDirectoryAsync(
			string directoryPath,
			string searchPattern = "*.log",
			int? maxFilesToParse = null,
			[EnumeratorCancellation] CancellationToken cancellationToken = default) {
			var allFilePaths = Directory.EnumerateFiles(directoryPath, searchPattern, SearchOption.TopDirectoryOnly);

			var filesToParse = maxFilesToParse.HasValue ? allFilePaths.Take(maxFilesToParse.Value) : allFilePaths;

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
		private async IAsyncEnumerable<LogEntry> ParseLines(
			IAsyncEnumerable<(string filePath, string line)> lines,
			[EnumeratorCancellation] CancellationToken cancellationToken = default) {
			var lastErrorEntryByFile = new Dictionary<string, LogEntry?>();
			var lineNumberByFile = new Dictionary<string, int>();
			var singleWordKeywords = new[] { "error", "exception", "failed", "timeout", "critical", "fatal" };
			var timeRegex = new System.Text.RegularExpressions.Regex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}");
			var singleWordRegex = new System.Text.RegularExpressions.Regex($@"\b({string.Join("|", singleWordKeywords)})\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
			// var notFoundRegex = new System.Text.RegularExpressions.Regex(@"\bnot\s+found\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

			_logger.LogInformation("Starting ParseLines iteration");
			int processedLinesCount = 0;

			await foreach (var (filePath, line) in lines.WithCancellation(cancellationToken)) {
				cancellationToken.ThrowIfCancellationRequested(); // Check for cancellation

				processedLinesCount++;
				_logger.LogDebug("Processing line {ProcessedCount}: {Line}", processedLinesCount, line.Length > 100 ? line.Substring(0, 100) + "..." : line);

				lineNumberByFile.TryAdd(filePath, 0);
				lineNumberByFile[filePath]++;
				int lineNumber = lineNumberByFile[filePath];

				bool containsErrorKeyword = singleWordRegex.IsMatch(line); // Removed notFoundRegex check

				if (lineParser.IsLogLine(line)) {
					LogEntry? entry = null;
					try {
						entry = lineParser.Parse(line, lineNumber, filePath);
					} catch (System.Exception ex) {
						_logger.LogWarning(ex, "Failed to parse log line {LineNumber} in {FilePath}, creating error entry: {Line}", lineNumber, filePath, line);

						// Create an error entry for unparseable log lines
						entry = new LogEntry {
							Timestamp = System.DateTime.Now,
							Level = "ERROR",
							Message = $"[PARSE ERROR] {line.Trim()}",
							RawData = line,
							FilePath = filePath,
							LineNumber = lineNumber,
							ErrorType = "ParseError",
							ErrorDescription = $"Failed to parse log line: {ex.Message}"
						};
					}

					if (entry == null) {
						_logger.LogWarning("Line parser returned null for line {LineNumber} in {FilePath}, creating fallback entry: {Line}", lineNumber, filePath, line);

						// Create fallback entry for null results
						entry = new LogEntry {
							Timestamp = System.DateTime.Now,
							Level = "INFO",
							Message = $"[UNPARSED] {line.Trim()}",
							RawData = line,
							FilePath = filePath,
							LineNumber = lineNumber
						};
					}

					if (containsErrorKeyword) {
						entry.Level = "ERROR";
					}

					yield return entry;

					if (entry.Level.Trim().Equals(ErrorLevel, System.StringComparison.InvariantCultureIgnoreCase))
						lastErrorEntryByFile[filePath] = entry;
					else
						lastErrorEntryByFile[filePath] = null;
				} else {
					bool handledAsStackTrace = false;
					if (lastErrorEntryByFile.TryGetValue(filePath, out var lastErrorEntry) && lastErrorEntry != null) {
						if (!timeRegex.IsMatch(line)) {
							try {
								AppendStackTrace(lastErrorEntry, line);
								handledAsStackTrace = true;
							} catch (System.Exception ex) {
								_logger.LogError(ex, "Failed to append stack trace for line {LineNumber} in {FilePath}: {Line}", lineNumber, filePath, line);
							}
						}
					}

					if (!handledAsStackTrace) {
						string levelForUnparsedLine = containsErrorKeyword ? "ERROR" : "INFO";
						var unparsedEntry = new LogEntry {
							Timestamp = System.DateTime.Now,
							Level = levelForUnparsedLine,
							Message = line.Trim(),
							RawData = line,
							FilePath = filePath,
							LineNumber = lineNumber
						};
						yield return unparsedEntry;

						if (levelForUnparsedLine == "ERROR") {
							lastErrorEntryByFile[filePath] = unparsedEntry;
						} else {
							lastErrorEntryByFile[filePath] = null;
						}
					}
				}
			}

			_logger.LogInformation("ParseLines completed. Total processed lines: {ProcessedCount}", processedLinesCount);
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
namespace Log_Parser_App.Services
{
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using Log_Parser_App.Models;
	using Log_Parser_App.Models.Interfaces;
	using Microsoft.Extensions.Logging;
	using System.IO; // Required for Path and Directory operations

	public partial class LogParserService(ILogger<LogParserService> logger, ILogLineParser lineParser, ILogFileLoader fileLoader, ILogFilesLoader filesLoader) : ILogParserService
	{
		private readonly ILogger<LogParserService> _logger = logger;
		private const string ErrorLevel = "ERROR";

		public async Task<IEnumerable<LogEntry>> ParseLogFileAsync(string filePath) {
			var lines = await fileLoader.LoadLinesAsync(filePath);
			return ParseLines(lines.Select(l => (filePath, l)));
		}

		public async Task<IEnumerable<LogEntry>> ParseLogFilesAsync(IEnumerable<string> filePaths) {
			var lines = await filesLoader.LoadLinesAsync(filePaths);
			return ParseLines(lines);
		}


		public async Task<IEnumerable<LogEntry>> ParseLogDirectoryAsync(string directoryPath, string searchPattern = "*.log", int? maxFilesToParse = null) {
			var allFilePaths = Directory.EnumerateFiles(directoryPath, searchPattern, SearchOption.TopDirectoryOnly);
            
            var filesToParse = maxFilesToParse.HasValue 
                ? allFilePaths.Take(maxFilesToParse.Value)
                : allFilePaths;

            var lines = await filesLoader.LoadLinesAsync(filesToParse);
			return ParseLines(lines);
		}


		public Task<IEnumerable<LogEntry>> FilterErrorsAsync(IEnumerable<LogEntry> logEntries) {
			var filteredEntries = logEntries.Where(e => e.Level.Trim().Equals(ErrorLevel, System.StringComparison.InvariantCultureIgnoreCase)).ToList();
			return Task.FromResult<IEnumerable<LogEntry>>(filteredEntries);
		}

		private IEnumerable<LogEntry> ParseLines(IEnumerable<(string filePath, string line)> lines) {
			var logEntries = new List<LogEntry>();
			var lastErrorEntryByFile = new Dictionary<string, LogEntry?>();
			var lineNumberByFile = new Dictionary<string, int>();
			var errorKeywords = new[] { "error", "exception", "not found", "failed", "timeout", "critical", "fatal" };
			var timeRegex = new System.Text.RegularExpressions.Regex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}");
			var errorWordRegex = new System.Text.RegularExpressions.Regex(@"\\berror\\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
			foreach (var (filePath, line) in lines) {
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

					logEntries.Add(entry);
					if (entry.Level.Trim().Equals(ErrorLevel, System.StringComparison.OrdinalIgnoreCase))
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
                        logEntries.Add(new LogEntry {
                            Timestamp = System.DateTime.Now,
                            Level = levelForUnparsedLine, 
                            Message = line.Trim(),
                            RawData = line,
                            FilePath = filePath,
                            LineNumber = lineNumber
                        });
                        
                        if (levelForUnparsedLine == "ERROR") {
                            lastErrorEntryByFile[filePath] = logEntries.LastOrDefault(le => le.FilePath == filePath && le.LineNumber == lineNumber);
                        } else {
                            lastErrorEntryByFile[filePath] = null;
                        }
                    }
				}
			}
			_logger.LogDebug($"[ParseLines] Parsed logEntries count: {logEntries.Count}");
			return logEntries;
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
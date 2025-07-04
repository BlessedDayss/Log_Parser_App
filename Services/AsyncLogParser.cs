using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services
{
    public class AsyncLogParser : IAsyncLogParser
    {
        private readonly ILogger<AsyncLogParser> _logger;
        private readonly ILogEntryPool _logEntryPool;
        private LogParsingProgress _currentProgress;
        private readonly object _progressLock = new object();

        public AsyncLogParser(ILogger<AsyncLogParser> logger, ILogEntryPool logEntryPool)
        {
            _logger = logger;
            _logEntryPool = logEntryPool;
            _currentProgress = new LogParsingProgress();
        }

        public async IAsyncEnumerable<LogEntry> ParseAsync(
            string filePath, 
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Log file not found: {filePath}");

            using var reader = new StreamReader(filePath);
            string? line;
            var lineNumber = 1;

            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var logEntry = ParseLogLine(line, lineNumber, filePath);
                if (logEntry != null)
                {
                    yield return logEntry;
                }

                lineNumber++;
            }
        }

        public Task<LogParsingProgress> GetProgressAsync()
        {
            lock (_progressLock)
            {
                return Task.FromResult(_currentProgress);
            }
        }

        public async Task<long> EstimateLinesTotalAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return 0;

            using var reader = new StreamReader(filePath);
            var lineCount = 0L;
            while (await reader.ReadLineAsync() != null)
            {
                lineCount++;
            }
            return lineCount;
        }

        private LogEntry? ParseLogLine(string line, int lineNumber, string filePath)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            var logEntry = _logEntryPool.Get();
            logEntry.Message = line;
            logEntry.Level = "INFO";
            logEntry.LineNumber = lineNumber;
            logEntry.FilePath = filePath;

            return logEntry;
        }
    }
}

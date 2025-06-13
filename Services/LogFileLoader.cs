namespace Log_Parser_App.Services
{
    using System.Collections.Generic;
    using System.IO;
    using Log_Parser_App.Models.Interfaces;
    using Microsoft.Extensions.Logging;


    public class LogFileLoader : ILogFileLoader
    {
        private readonly ILogger<LogFileLoader>? _logger;

        public LogFileLoader(ILogger<LogFileLoader>? logger = null) {
            _logger = logger;
        }

        public async IAsyncEnumerable<string> LoadLinesAsync(string filePath) {
            _logger?.LogInformation("Starting to load file: {FilePath}", filePath);
            int lineNumber = 0;

            using var reader = new StreamReader(filePath);
            string? line;

            while ((line = await reader.ReadLineAsync()) != null) {
                lineNumber++;
                yield return line;

                if (lineNumber % 100 == 0) {
                    _logger?.LogDebug("Successfully read {LineNumber} lines from {FilePath}", lineNumber, filePath);
                }
            }

            _logger?.LogInformation("Completed loading file {FilePath}. Total lines read: {LineNumber}", filePath, lineNumber);
        }
    }
}
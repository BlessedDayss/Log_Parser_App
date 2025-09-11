namespace Log_Parser_App.Services
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Log_Parser_App.Models;
    using System.Threading;



    public interface ILogParserService
    {
        IAsyncEnumerable<LogEntry> ParseLogFileAsync(string filePath, CancellationToken cancellationToken = default); // Changed

        Task<IEnumerable<LogEntry>> ExecuteQueryAsync(IEnumerable<LogEntry> logEntries, string query);

        Task<string> DetectLogFormatAsync(string filePath);

        Task<IEnumerable<LogEntry>> FilterErrorsAsync(IEnumerable<LogEntry> logEntries);

        Task<IEnumerable<PackageLogEntry>> ParsePackageLogFileAsync(string filePath);

        IAsyncEnumerable<LogEntry> ParseLogFilesAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default); // Changed

        IAsyncEnumerable<LogEntry> ParseLogDirectoryAsync(
            string directoryPath,
            string searchPattern = "*.log",
            int? maxFilesToParse = null,
            CancellationToken cancellationToken = default); // Changed
    }
}

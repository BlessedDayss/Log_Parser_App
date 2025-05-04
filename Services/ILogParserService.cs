namespace Log_Parser_App.Services
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Log_Parser_App.Models;



    public interface ILogParserService
    {
        Task<IEnumerable<LogEntry>> ParseLogFileAsync(string filePath);

        Task<IEnumerable<LogEntry>> ExecuteQueryAsync(IEnumerable<LogEntry> logEntries, string query);

        Task<string> DetectLogFormatAsync(string filePath);

        Task<IEnumerable<LogEntry>> FilterErrorsAsync(IEnumerable<LogEntry> logEntries);

        Task<IEnumerable<PackageLogEntry>> ParsePackageLogFileAsync(string filePath);
    }
}
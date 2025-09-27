using System.Collections.Generic;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Interfaces
{
    public interface ILog4NetService
    {
        Task InitializeDatabaseAsync();
        Task SaveLog4NetLogsAsync(List<Log_Parser_App.Models.Log4NetLogEntry> logEntries);
        Task<List<Log_Parser_App.Models.Log4NetLogEntry>> GetLog4NetLogsAsync();
        Task<bool> IsDatabaseAvailable();
        Task EnsureDatabaseExistsAsync();
        Task<bool> RestoreFromBackupAsync(string backupFilePath);
    }
}

using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Interfaces
{
    public interface IPostgreSQLSettingsService
    {
        Task<PostgreSQLSettings> LoadSettingsAsync();
        Task SaveSettingsAsync(PostgreSQLSettings settings);
        Task<bool> TestConnectionAsync(PostgreSQLSettings settings);
        Task<bool> CreateDatabaseIfNotExistsAsync(PostgreSQLSettings settings);
        PostgreSQLSettings GetDefaultSettings();
    }
}



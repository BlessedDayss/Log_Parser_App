using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Log_Parser_App.Services.Filtering.Interfaces;

public interface IStorageProvider
{
    Task SaveAsync(string key, string content, CancellationToken cancellationToken = default);
    Task<string?> LoadAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetAllKeysAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}

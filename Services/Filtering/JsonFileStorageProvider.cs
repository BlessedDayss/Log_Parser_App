using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Services.Filtering.Interfaces;

namespace Log_Parser_App.Services.Filtering;

public class JsonFileStorageProvider : IStorageProvider
{
    private readonly string _directoryPath;
    private readonly SemaphoreSlim _fileSemaphore;

    public JsonFileStorageProvider(string directoryPath)
    {
        _directoryPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
        _fileSemaphore = new SemaphoreSlim(1, 1);
        EnsureDirectoryExists();
    }

    public async Task SaveAsync(string key, string content, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(key);

        await _fileSemaphore.WaitAsync(cancellationToken);
        try
        {
            await File.WriteAllTextAsync(filePath, content, cancellationToken);
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    public async Task<string?> LoadAsync(string key, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(key);

        if (!File.Exists(filePath))
            return null;

        await _fileSemaphore.WaitAsync(cancellationToken);
        try
        {
            return await File.ReadAllTextAsync(filePath, cancellationToken);
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(key);

        if (!File.Exists(filePath))
            return false;

        await _fileSemaphore.WaitAsync(cancellationToken);
        try
        {
            File.Delete(filePath);
            return true;
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    public async Task<IEnumerable<string>> GetAllKeysAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_directoryPath))
            return await Task.FromResult(Enumerable.Empty<string>());

        await _fileSemaphore.WaitAsync(cancellationToken);
        try
        {
            var files = Directory.GetFiles(_directoryPath, "*.json");
            var keys = files
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrEmpty(name))
                .Cast<string>()
                .ToList();
            return await Task.FromResult<IEnumerable<string>>(keys);
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(key);
        return Task.FromResult(File.Exists(filePath));
    }

    private string GetFilePath(string key)
    {
        return Path.Combine(_directoryPath, $"{key}.json");
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_directoryPath))
        {
            Directory.CreateDirectory(_directoryPath);
        }
    }
}

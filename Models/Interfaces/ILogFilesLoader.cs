using System.Collections.Generic;
using System.Threading.Tasks;

namespace Log_Parser_App.Models.Interfaces
{
    public interface ILogFilesLoader
    {
        IAsyncEnumerable<(string filePath, string line)> LoadLinesAsync(IEnumerable<string> filePaths);
        IAsyncEnumerable<(string filePath, string line)> LoadLinesFromDirectoryAsync(string directoryPath, string searchPattern = "*.log");
    }
} 
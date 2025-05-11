using System.Collections.Generic;
using System.Threading.Tasks;

namespace Log_Parser_App.Models.Interfaces
{
    public interface ILogFilesLoader
    {
        Task<IEnumerable<(string filePath, string line)>> LoadLinesAsync(IEnumerable<string> filePaths);
        Task<IEnumerable<(string filePath, string line)>> LoadLinesFromDirectoryAsync(string directoryPath, string searchPattern = "*.log");
    }
} 
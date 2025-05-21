using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Log_Parser_App.Models.Interfaces;

namespace Log_Parser_App.Services
{
    public class LogFilesLoader : ILogFilesLoader
    {
        public async IAsyncEnumerable<(string filePath, string line)> LoadLinesAsync(IEnumerable<string> filePaths)
        {
            foreach (var filePath in filePaths)
            {
                using var reader = new StreamReader(filePath);
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    yield return (filePath, line);
                }
            }
        }

        public IAsyncEnumerable<(string filePath, string line)> LoadLinesFromDirectoryAsync(string directoryPath, string searchPattern = "*.log")
        {
            var files = Directory.GetFiles(directoryPath, searchPattern, SearchOption.AllDirectories);
            return LoadLinesAsync(files);
        }
    }
}
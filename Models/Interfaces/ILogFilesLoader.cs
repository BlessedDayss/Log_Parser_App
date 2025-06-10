namespace Log_Parser_App.Models.Interfaces
{
    using System.Collections.Generic;
    using System.Threading.Tasks;


    public interface ILogFilesLoader
    {
        IAsyncEnumerable<(string filePath, string line)> LoadLinesAsync(IEnumerable<string> filePaths);
        IAsyncEnumerable<(string filePath, string line)> LoadLinesFromDirectoryAsync(string directoryPath, string searchPattern = "*.log");
    }
}
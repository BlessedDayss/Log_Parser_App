using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Log_Parser_App.Models.Interfaces;

namespace Log_Parser_App.Services
{
    public class LogFileLoader : ILogFileLoader
    {
        public async IAsyncEnumerable<string> LoadLinesAsync(string filePath)
        {
            using var reader = new StreamReader(filePath);
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                yield return line;
            }
        }
    }
}
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Log_Parser_App.Models.Interfaces
{
    public interface ILogFileLoader
    {
        Task<IEnumerable<string>> LoadLinesAsync(string filePath);
    }
} 
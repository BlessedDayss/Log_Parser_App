namespace Log_Parser_App.Models.Interfaces
{
    using System.Collections.Generic;
    using System.Threading.Tasks;


    public interface ILogFileLoader
    {
        IAsyncEnumerable<string> LoadLinesAsync(string filePath);
    }
}
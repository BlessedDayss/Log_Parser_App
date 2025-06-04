using Log_Parser_App.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Log_Parser_App.Models.Interfaces
{
    public interface IIISLogParserService
    {
        IAsyncEnumerable<IISLogEntry> ParseLogFileAsync(string filePath, CancellationToken cancellationToken);
    }
} 
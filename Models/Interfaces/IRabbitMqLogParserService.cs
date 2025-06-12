namespace Log_Parser_App.Models.Interfaces
{
    using Log_Parser_App.Models;
    using System.Collections.Generic;
    using System.Threading;

    public interface IRabbitMqLogParserService
    {
        IAsyncEnumerable<LogEntry> ParseLogFileAsync(string filePath, CancellationToken cancellationToken = default);
    }
} 
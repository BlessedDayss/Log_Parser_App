namespace Log_Parser_App.Services.Parsing
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Log_Parser_App.Models;

    public interface IPairedRabbitMqLogParserService
    {
        Task<List<RabbitMqLogEntry>> ParsePairedFilesAsync(
            IEnumerable<PairedFileData> pairedFiles, 
            CancellationToken cancellationToken = default);

        Task<RabbitMqLogEntry?> ParseSinglePairedFileAsync(
            PairedFileData pairedFile, 
            CancellationToken cancellationToken = default);

        Task<RabbitMqLogEntry?> ParseMainFileOnlyAsync(
            string mainFilePath, 
            CancellationToken cancellationToken = default);
    }
}

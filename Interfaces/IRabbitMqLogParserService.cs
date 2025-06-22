namespace Log_Parser_App.Interfaces
{
using Log_Parser_App.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

	#region Interface: IRabbitMqLogParserService

	public interface IRabbitMqLogParserService
	{

		#region Methods: Public

		IAsyncEnumerable<RabbitMqLogEntry> ParseLogFileAsync(string filePath, CancellationToken cancellationToken = default);

		IAsyncEnumerable<RabbitMqLogEntry> ParseLogFilesAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default);

		IAsyncEnumerable<RabbitMqLogEntry> ParseLogDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);

		Task<bool> IsValidRabbitMqLogFileAsync(string filePath);

		Task<int> GetEstimatedLogCountAsync(string filePath);

		#endregion

	}

	#endregion

}

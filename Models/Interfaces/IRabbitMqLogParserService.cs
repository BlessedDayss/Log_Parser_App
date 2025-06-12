namespace Log_Parser_App.Models.Interfaces
{
    using Log_Parser_App.Models;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for RabbitMQ JSON log parsing service.
    /// Provides methods for parsing JSON-formatted RabbitMQ logs with async enumerable support.
    /// </summary>
    public interface IRabbitMqLogParserService
    {
        /// <summary>
        /// Asynchronously parses RabbitMQ logs from a file path
        /// </summary>
        /// <param name="filePath">Path to the JSON log file</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Async enumerable of parsed RabbitMQ log entries</returns>
        IAsyncEnumerable<RabbitMqLogEntry> ParseLogFileAsync(string filePath, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Asynchronously parses RabbitMQ logs from multiple file paths
        /// </summary>
        /// <param name="filePaths">Collection of paths to JSON log files</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Async enumerable of parsed RabbitMQ log entries from all files</returns>
        IAsyncEnumerable<RabbitMqLogEntry> ParseLogFilesAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Validates if the file contains valid RabbitMQ JSON logs
        /// </summary>
        /// <param name="filePath">Path to the file to validate</param>
        /// <returns>True if file contains valid RabbitMQ JSON format</returns>
        Task<bool> IsValidRabbitMqLogFileAsync(string filePath);
        
        /// <summary>
        /// Gets estimated log entry count from a file (for progress tracking)
        /// </summary>
        /// <param name="filePath">Path to the JSON log file</param>
        /// <returns>Estimated number of log entries</returns>
        Task<int> GetEstimatedLogCountAsync(string filePath);
    }
} 
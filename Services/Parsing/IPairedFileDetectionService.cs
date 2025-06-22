namespace Log_Parser_App.Services.Parsing
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Log_Parser_App.Models;

    #region Interface: IPairedFileDetectionService

    /// <summary>
    /// Service interface for detecting and pairing RabbitMQ message files with their corresponding headers files
    /// </summary>
    public interface IPairedFileDetectionService
    {
        #region Methods: Public

        /// <summary>
        /// Scans a directory for paired RabbitMQ files and returns them as an async enumerable
        /// </summary>
        /// <param name="directoryPath">Directory path to scan for files</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>Async enumerable of paired file data</returns>
        IAsyncEnumerable<PairedFileData> DetectPairedFilesAsync(string directoryPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Scans a directory for paired RabbitMQ files and returns them as a list
        /// </summary>
        /// <param name="directoryPath">Directory path to scan for files</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>List of paired file data</returns>
        Task<List<PairedFileData>> DetectPairedFilesListAsync(string directoryPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Attempts to find the paired headers file for a given main message file
        /// </summary>
        /// <param name="mainFilePath">Path to the main message file</param>
        /// <returns>Paired file data indicating the result</returns>
        Task<PairedFileData> FindPairedFileAsync(string mainFilePath);

        /// <summary>
        /// Validates whether a file follows the expected RabbitMQ file naming pattern
        /// </summary>
        /// <param name="filePath">Path to the file to validate</param>
        /// <returns>True if file matches main file pattern, false otherwise</returns>
        bool IsMainMessageFile(string filePath);

        /// <summary>
        /// Validates whether a file follows the expected RabbitMQ headers file naming pattern
        /// </summary>
        /// <param name="filePath">Path to the file to validate</param>
        /// <returns>True if file matches headers file pattern, false otherwise</returns>
        bool IsHeadersFile(string filePath);

        /// <summary>
        /// Extracts the message ID from a RabbitMQ file name
        /// </summary>
        /// <param name="fileName">File name to extract ID from</param>
        /// <returns>Message ID if successful, null otherwise</returns>
        string? ExtractMessageId(string fileName);

        #endregion
    }

    #endregion
} 
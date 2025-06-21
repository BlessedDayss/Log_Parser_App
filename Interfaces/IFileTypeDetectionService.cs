namespace Log_Parser_App.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using Log_Parser_App.Models;

    /// <summary>
    /// Service for detecting file types based on content analysis
    /// Follows Single Responsibility Principle - only handles file type detection
    /// </summary>
    public interface IFileTypeDetectionService
    {
        /// <summary>
        /// Analyzes file content to determine its format type
        /// </summary>
        /// <param name="filePath">Path to the file to analyze</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Detected log format type</returns>
        Task<LogFormatType> DetectFileTypeAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if file is an IIS log based on content
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if file is IIS log</returns>
        Task<bool> IsIISLogAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if file is a RabbitMQ log based on content
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if file is RabbitMQ log</returns>
        Task<bool> IsRabbitMQLogAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if file is a standard application log
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if file is standard log</returns>
        Task<bool> IsStandardLogAsync(string filePath, CancellationToken cancellationToken = default);
    }
} 
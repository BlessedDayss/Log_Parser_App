namespace Log_Parser_App.Models
{
    using System;

    #region Class: PairedFileData

    /// <summary>
    /// Represents a paired RabbitMQ file structure containing main message file and headers file
    /// </summary>
    public class PairedFileData
    {
        #region Properties: Public

        /// <summary>
        /// Path to the main message file (e.g., msg-12345)
        /// </summary>
        public string MainFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Path to the headers and properties file (e.g., msg-12345-headers+properties.json)
        /// </summary>
        public string HeadersFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Message identifier extracted from filename (e.g., "12345" from msg-12345)
        /// </summary>
        public string MessageId { get; set; } = string.Empty;

        /// <summary>
        /// Status indicating the completeness of the paired files
        /// </summary>
        public PairedFileStatus Status { get; set; } = PairedFileStatus.Incomplete;

        /// <summary>
        /// Indicates whether both main file and headers file are present and accessible, or unified JSON
        /// </summary>
        public bool IsComplete => Status == PairedFileStatus.Complete || Status == PairedFileStatus.UnifiedJson;

        /// <summary>
        /// Indicates whether only the main file is available (for fallback parsing)
        /// </summary>
        public bool HasMainFileOnly => 
            !string.IsNullOrEmpty(MainFilePath) && string.IsNullOrEmpty(HeadersFilePath);

        /// <summary>
        /// Error message if validation failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        #endregion

        #region Methods: Public

        /// <summary>
        /// Creates a complete paired file data instance
        /// </summary>
        /// <param name="mainFilePath">Path to main message file</param>
        /// <param name="headersFilePath">Path to headers file</param>
        /// <param name="messageId">Message identifier</param>
        /// <returns>Complete PairedFileData instance</returns>
        public static PairedFileData CreateComplete(string mainFilePath, string headersFilePath, string messageId)
        {
            return new PairedFileData
            {
                MainFilePath = mainFilePath,
                HeadersFilePath = headersFilePath,
                MessageId = messageId,
                Status = PairedFileStatus.Complete
            };
        }

        /// <summary>
        /// Creates a partial paired file data instance (main file only)
        /// </summary>
        /// <param name="mainFilePath">Path to main message file</param>
        /// <param name="messageId">Message identifier</param>
        /// <returns>Partial PairedFileData instance</returns>
        public static PairedFileData CreatePartial(string mainFilePath, string messageId)
        {
            return new PairedFileData
            {
                MainFilePath = mainFilePath,
                MessageId = messageId,
                Status = PairedFileStatus.Partial
            };
        }

        /// <summary>
        /// Creates a failed paired file data instance
        /// </summary>
        /// <param name="messageId">Message identifier</param>
        /// <param name="errorMessage">Error description</param>
        /// <returns>Failed PairedFileData instance</returns>
        public static PairedFileData CreateFailed(string messageId, string errorMessage)
        {
            return new PairedFileData
            {
                MessageId = messageId,
                Status = PairedFileStatus.Failed,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// Returns a string representation of the paired file data
        /// </summary>
        /// <returns>String description</returns>
        public override string ToString()
        {
            return Status switch
            {
                PairedFileStatus.Complete => $"Complete: {MessageId} (Main: {MainFilePath}, Headers: {HeadersFilePath})",
                PairedFileStatus.UnifiedJson => $"UnifiedJson: {MessageId} (Main: {MainFilePath}, Headers: {HeadersFilePath})",
                PairedFileStatus.Partial => $"Partial: {MessageId} (Main: {MainFilePath})",
                PairedFileStatus.Failed => $"Failed: {MessageId} ({ErrorMessage})",
                _ => $"Incomplete: {MessageId}"
            };
        }

        #endregion
    }

    #endregion

    #region Enum: PairedFileStatus

    /// <summary>
    /// Enumeration representing the status of paired file validation
    /// </summary>
    public enum PairedFileStatus
    {
        /// <summary>
        /// File pairing is incomplete or not yet validated
        /// </summary>
        Incomplete,

        /// <summary>
        /// Both main file and headers file are present and accessible
        /// </summary>
        Complete,

        /// <summary>
        /// Only main file is available, headers file is missing
        /// </summary>
        Partial,

        /// <summary>
        /// File pairing failed due to errors (file access issues, etc.)
        /// </summary>
        Failed,

        /// <summary>
        /// Main file contains unified JSON with embedded headers (best of both worlds)
        /// </summary>
        UnifiedJson
    }

    #endregion
} 
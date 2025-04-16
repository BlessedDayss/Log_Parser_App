using System;
using System.Collections.Generic;

namespace LogParserApp.Models
{
    /// <summary>
    /// Represents a log entry from package logs (.log files)
    /// </summary>
    public class PackageLogEntry : LogEntry
    {
        /// <summary>
        /// Package identifier
        /// </summary>
        public string PackageId { get; set; } = string.Empty;
        
        /// <summary>
        /// Package version
        /// </summary>
        public string Version { get; set; } = string.Empty;
        
        /// <summary>
        /// Installation or operation status
        /// </summary>
        public string Status { get; set; } = string.Empty;
        
        /// <summary>
        /// Dependencies information
        /// </summary>
        public string Dependencies { get; set; } = string.Empty;
        
        /// <summary>
        /// Operation name (install, update, remove)
        /// </summary>
        public string Operation { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets the operation icon for visualization
        /// </summary>
        public string OperationIcon => Operation.ToLowerInvariant() switch
        {
            "install" => "📥",
            "update" => "🔄",
            "remove" => "🗑️",
            "rollback" => "↩️",
            _ => "📦"
        };
    }
} 
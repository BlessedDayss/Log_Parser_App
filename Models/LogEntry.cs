using System;
using System.Collections.Generic;
using Avalonia.Media;
using System.Text.RegularExpressions;
// using CommunityToolkit.Mvvm.ComponentModel; // Removed dependency

namespace LogParserApp.Models
{
    /// <summary>
    /// Represents a log entry parsed from a log file
    /// </summary>
    public partial class LogEntry // Removed : ObservableObject
    {
        /// <summary>
        /// Timestamp of the log entry
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Log level (e.g., INFO, WARNING, ERROR, DEBUG)
        /// </summary>
        public string Level { get; set; } = "INFO";
        
        /// <summary>
        /// Source of the log entry (e.g., application name, class name)
        /// </summary>
        public string Source { get; set; } = string.Empty;
        
        /// <summary>
        /// The actual log message
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// The original raw log data
        /// </summary>
        public string RawData { get; set; } = string.Empty;
        
        /// <summary>
        /// NEW: Correlation ID property
        /// </summary>
        public string? CorrelationId { get; set; }
        
        /// <summary>
        /// Gets the log level icon for visualization
        /// </summary>
        public string LevelIcon => Level switch
        {
            "ERROR" => "🔴",
            "WARNING" => "🟠",
            "INFO" => "🔵",
            "DEBUG" => "🟢",
            "TRACE" => "⚪",
            "CRITICAL" => "⛔",
            _ => "ℹ️"
        };
        
        /// <summary>
        /// Gets the background color based on log level
        /// </summary>
        public string LevelBackground => Level.ToUpperInvariant() switch
        {
            "ERROR" => "#FFEBEE",    // Light red
            "WARNING" => "#FFF3E0",  // Light orange
            "INFO" => "#E3F2FD",     // Light blue
            "DEBUG" => "#E8F5E9",    // Light green
            "TRACE" => "#F3F3F3",    // Light gray
            "CRITICAL" => "#5C0011", // Dark red
            _ => "#FFFFFF"           // White
        };

        // Color based on log level (new property)
        public IBrush LevelColor => Level switch
        {
            "ERROR" => new SolidColorBrush(Color.Parse("#F15B5B")),
            "WARNING" => new SolidColorBrush(Color.Parse("#F9A825")),
            "INFO" => new SolidColorBrush(Color.Parse("#64B5F6")),
            _ => new SolidColorBrush(Color.Parse("#BBBBBB"))
        };
        
        /// <summary>
        /// Тип ошибки (если это запись об ошибке)
        /// </summary>
        public string? ErrorType { get; set; }
        
        /// <summary>
        /// Описание ошибки
        /// </summary>
        public string? ErrorDescription { get; set; }
        
        /// <summary>
        /// Рекомендации по исправлению ошибки
        /// </summary>
        public List<string> ErrorRecommendations { get; set; } = new List<string>();
        
        /// <summary>
        /// Показывает, есть ли рекомендации для этой ошибки
        /// </summary>
        public bool HasRecommendations => Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase) && ErrorRecommendations != null && ErrorRecommendations.Count > 0;

        public string? FilePath { get; set; }
        public int? LineNumber { get; set; }

        public System.Windows.Input.ICommand? OpenFileCommand { get; set; }

        public string DisplayMessage
        {
            get
            {
                if (string.IsNullOrEmpty(Message))
                    return string.Empty;
                var lines = Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var regex = new Regex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}");
                foreach (var line in lines)
                {
                    if (regex.IsMatch(line))
                        return line.Trim();
                }
                return lines[0].Trim();
            }
        }

        public string? StackTrace { get; set; }
    }
} 
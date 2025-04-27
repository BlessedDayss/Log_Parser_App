using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia.Media;

namespace Log_Parser_App.Models
{

    public partial class LogEntry 
    {

        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        public string Level { get; set; } = "INFO";
        
        public string Source { get; set; } = string.Empty;
        
        public string Message { get; set; } = string.Empty;
        
        public string RawData { get; init; } = string.Empty;

        public string? CorrelationId { get; set; }
        

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

        public IBrush LevelColor => Level switch
        {
            "ERROR" => new SolidColorBrush(Color.Parse("#F15B5B")),
            "WARNING" => new SolidColorBrush(Color.Parse("#F9A825")),
            "INFO" => new SolidColorBrush(Color.Parse("#64B5F6")),
            _ => new SolidColorBrush(Color.Parse("#BBBBBB"))
        };
        

        public string? ErrorType { get; set; }

        public string? ErrorDescription { get; set; }
        
        public List<string> ErrorRecommendations { get; set; } = [];
        
        public bool HasRecommendations => Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase) && ErrorRecommendations.Count > 0;

        public string? FilePath { get; set; }
        public int? LineNumber { get; set; }

        public System.Windows.Input.ICommand? OpenFileCommand { get; set; }

        public string DisplayMessage
        {
            get
            {
                if (string.IsNullOrEmpty(Message))
                    return string.Empty;
                var lines = Message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                var regex = MyRegex();
                foreach (var line in lines)
                {
                    if (regex.IsMatch(line))
                        return line.Trim();
                }
                return lines[0].Trim();
            }
        }

        public string? StackTrace { get; set; }

        [GeneratedRegex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}")]
        private static partial Regex MyRegex();
    }
} 
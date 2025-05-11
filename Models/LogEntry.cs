using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using System.Windows.Input;

namespace Log_Parser_App.Models
{

    public partial class LogEntry : INotifyPropertyChanged
    {
        private DateTime _timestamp = DateTime.Now;
        public DateTime Timestamp
        {
            get => _timestamp;
            set
            {
                if (_timestamp != value)
                {
                    _timestamp = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedTimestamp));
                    UpdateGraphs();
                }
            }
        }

        public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
        
        private string _level = "INFO";
        
        public string Level
        {
            get => _level;
            set
            {
                if (_level != value)
                {
                    Logger?.LogTrace("LogEntry (Line {LineNum}): Level changing from '{OldLevel}' to '{NewLevel}'", Timestamp.Ticks, _level, value);
                    _level = value;
                    OnPropertyChanged(); // Уведомляем об изменении Level
                    OnPropertyChanged(nameof(LevelIcon)); // Уведомляем об изменении зависимых свойств
                    OnPropertyChanged(nameof(LevelBackground));
                    OnPropertyChanged(nameof(LevelColor));
                    UpdateGraphs();
                }
            }
        }
        
        private string _source = string.Empty;
        public string Source
        {
            get => _source;
            set
            {
                if (_source != value)
                {
                    _source = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedSource));
                    UpdateGraphs();
                }
            }
        }

        public string FormattedSource => string.IsNullOrEmpty(Source) ? "Unknown" : Source;
        
        private string _message = string.Empty;
        public string Message
        {
            get => _message;
            set
            {
                if (_message != value)
                {
                    _message = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayMessage));
                    UpdateGraphs();
                }
            }
        }
        
        public string RawData { get; init; } = string.Empty;

        public string? CorrelationId { get; set; }
        

        public string LevelIcon => Level.ToUpperInvariant() switch
        {
            "ERROR" => "🔴",
            "WARNING" => "🟠",
            "INFO" => "🔵",
            "DEBUG" => "🟢",
            "TRACE" => "⚪",
            "CRITICAL" => "⛔",
            "VERBOSE" => "⚫",
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
            "VERBOSE" => "#F5F5F5",  // Light gray
            _ => "#FFFFFF"           // White
        };

        public IBrush LevelColor => Level.ToUpperInvariant() switch
        {
            "ERROR" => new SolidColorBrush(Color.Parse("#F15B5B")),
            "WARNING" => new SolidColorBrush(Color.Parse("#F9A825")),
            "INFO" => new SolidColorBrush(Color.Parse("#64B5F6")),
            "DEBUG" => new SolidColorBrush(Color.Parse("#4CAF50")),
            "TRACE" => new SolidColorBrush(Color.Parse("#9E9E9E")),
            "CRITICAL" => new SolidColorBrush(Color.Parse("#D32F2F")),
            "VERBOSE" => new SolidColorBrush(Color.Parse("#757575")),
            _ => new SolidColorBrush(Color.Parse("#BBBBBB"))
        };
        

        public string? ErrorType { get; set; }

        public string? ErrorDescription { get; set; }
        
        public List<string> ErrorRecommendations { get; set; } = new List<string>();
        
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
                    
                var lines = Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0)
                    return string.Empty;
                    
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

        public event PropertyChangedEventHandler? PropertyChanged;

        [System.Text.Json.Serialization.JsonIgnore] // Не сериализуем логгер
        public Microsoft.Extensions.Logging.ILogger? Logger { get; set; }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void UpdateGraphs()
        {
            OnPropertyChanged(nameof(Level));
            OnPropertyChanged(nameof(Source));
            OnPropertyChanged(nameof(Timestamp));
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        private ICommand? _toggleExpandCommand;
        public ICommand ToggleExpandCommand => _toggleExpandCommand ??= new DelegateCommand(() => IsExpanded = !IsExpanded);
    }
} 
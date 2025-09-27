using System;

namespace Log_Parser_App.Models
{
    public class Log4NetLogEntry
    {
        public int Id { get; set; } // Primary key for database
        public DateTime Date { get; set; }
        public string? Host { get; set; }
        public string? Site { get; set; }
        public string? Thread { get; set; }
        public string? Level { get; set; }
        public string? Logger { get; set; }
        public string? User { get; set; }
        public string? Message { get; set; }
        public string? Exception { get; set; }
        public string? MessageObject { get; set; }

        // Additional computed properties for UI
        public string DisplayText => $"{Date:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {Logger} - {Message}";
        public string ShortMessage => Message is not null && Message.Length > 100 ? Message.Substring(0, 100) + "..." : Message ?? string.Empty;
        public string ShortException => Exception is not null && Exception.Length > 100 ? Exception.Substring(0, 100) + "..." : Exception ?? string.Empty;
    }
}

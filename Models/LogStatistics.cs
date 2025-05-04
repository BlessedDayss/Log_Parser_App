namespace Log_Parser_App.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;


    public class LogStatistics
    {
        public int TotalCount { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public int InfoCount { get; set; }
        public int OtherCount { get; set; }

        public double ErrorPercent { get; set; }
        public double WarningPercent { get; set; }
        public double InfoPercent { get; set; }
        public double OtherPercent { get; set; }
        public double ErrorPercentage => ErrorPercent;
        public double WarningPercentage => WarningPercent;
        public double InfoPercentage => InfoPercent;
        public double OtherPercentage => OtherPercent;

        public static LogStatistics FromLogEntries(IEnumerable<LogEntry>? entries) {
            var stats = new LogStatistics();
            if (entries == null)
                return stats;

            var logEntries = entries.ToList();
            stats.TotalCount = logEntries.Count;

            stats.ErrorCount = logEntries.Count(e => e.Level.Trim().ToUpperInvariant() == "ERROR");
            stats.WarningCount = logEntries.Count(e => e.Level.Trim().ToUpperInvariant() == "WARNING");
            stats.InfoCount = logEntries.Count(e => e.Level.Trim().ToUpperInvariant() == "INFO");
            stats.OtherCount = stats.TotalCount - stats.ErrorCount - stats.WarningCount - stats.InfoCount;
            stats.ErrorPercent = stats.TotalCount > 0 ? Math.Round((double)stats.ErrorCount / stats.TotalCount * 100, 1) : 0;
            stats.WarningPercent = stats.TotalCount > 0 ? Math.Round((double)stats.WarningCount / stats.TotalCount * 100, 1) : 0;
            stats.InfoPercent = stats.TotalCount > 0 ? Math.Round((double)stats.InfoCount / stats.TotalCount * 100, 1) : 0;
            stats.OtherPercent = stats.TotalCount > 0 ? Math.Round((double)stats.OtherCount / stats.TotalCount * 100, 1) : 0;

            return stats;
        }
    }
}
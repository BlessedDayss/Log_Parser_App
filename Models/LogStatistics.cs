using System;
using System.Collections.Generic;
using System.Linq;

namespace LogParserApp.Models
{
    /// <summary>
    /// Provides statistics for log entries
    /// </summary>
    public class LogStatistics
    {
        public int TotalEntries { get; private set; }
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public int InfoCount { get; private set; }
        public int OtherCount { get; private set; }
        
        public double ErrorPercentage => TotalEntries > 0 ? Math.Round((double)ErrorCount / TotalEntries * 100, 1) : 0;
        public double WarningPercentage => TotalEntries > 0 ? Math.Round((double)WarningCount / TotalEntries * 100, 1) : 0;
        public double InfoPercentage => TotalEntries > 0 ? Math.Round((double)InfoCount / TotalEntries * 100, 1) : 0;
        public double OtherPercentage => TotalEntries > 0 ? Math.Round((double)OtherCount / TotalEntries * 100, 1) : 0;
        
        public static LogStatistics FromLogEntries(IEnumerable<LogEntry> entries)
        {
            var stats = new LogStatistics();
            if (entries == null) return stats;
            
            var logEntries = entries.ToList();
            stats.TotalEntries = logEntries.Count;
            
            stats.ErrorCount = logEntries.Count(e => e.Level.Trim().ToUpperInvariant() == "ERROR");
            stats.WarningCount = logEntries.Count(e => e.Level.Trim().ToUpperInvariant() == "WARNING");
            stats.InfoCount = logEntries.Count(e => e.Level.Trim().ToUpperInvariant() == "INFO");
            stats.OtherCount = stats.TotalEntries - stats.ErrorCount - stats.WarningCount - stats.InfoCount;
            
            return stats;
        }
    }
} 
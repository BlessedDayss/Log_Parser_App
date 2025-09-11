namespace Log_Parser_App.Models
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// Comprehensive log statistics model
	/// </summary>
	public class LogStatistics
	{
		// Basic counts
		public int TotalEntries { get; set; }
		public int ErrorEntries { get; set; }
		public int WarningEntries { get; set; }
		public int InfoEntries { get; set; }
		public int DebugEntries { get; set; }
		public int TraceEntries { get; set; }
		public int OtherEntries { get; set; }

		// Percentages
		public double ErrorPercentage { get; set; }
		public double WarningPercentage { get; set; }
		public double InfoPercentage { get; set; }
		public double DebugPercentage { get; set; }
		public double TracePercentage { get; set; }
		public double OtherPercentage { get; set; }

		// Time-based information
		public DateTime FirstLogTime { get; set; }
		public DateTime LastLogTime { get; set; }
		public TimeSpan TimeSpan { get; set; }

		// Source and distribution information
		public int UniqueSources { get; set; }
		public int PeakHour { get; set; }
		public DateTime PeakDay { get; set; }
		public double LogsPerHour { get; set; }
		public double LogsPerMinute { get; set; }

		// Dictionaries for detailed analysis
		public Dictionary<int, int> HourlyDistribution { get; set; } = new();
		public Dictionary<string, int> LevelDistribution { get; set; } = new();
		public Dictionary<string, int> SourceDistribution { get; set; } = new();
		public Dictionary<string, int> TopErrors { get; set; } = new();

		// Computed properties for backward compatibility
		public int TotalCount 
		{ 
			get => TotalEntries; 
			set => TotalEntries = value; 
		}
		public int ErrorCount 
		{ 
			get => ErrorEntries; 
			set => ErrorEntries = value; 
		}
		public int WarningCount 
		{ 
			get => WarningEntries; 
			set => WarningEntries = value; 
		}
		public int InfoCount 
		{ 
			get => InfoEntries; 
			set => InfoEntries = value; 
		}
		public int OtherCount 
		{ 
			get => OtherEntries; 
			set => OtherEntries = value; 
		}
		public double ErrorPercent 
		{ 
			get => ErrorPercentage; 
			set => ErrorPercentage = value; 
		}
		public double WarningPercent 
		{ 
			get => WarningPercentage; 
			set => WarningPercentage = value; 
		}
		public double InfoPercent 
		{ 
			get => InfoPercentage; 
			set => InfoPercentage = value; 
		}
		public double OtherPercent 
		{ 
			get => OtherPercentage; 
			set => OtherPercentage = value; 
		}

		// Time properties for backward compatibility
		public DateTime FirstTimestamp 
		{ 
			get => FirstLogTime; 
			set => FirstLogTime = value; 
		}
		public DateTime LastTimestamp 
		{ 
			get => LastLogTime; 
			set => LastLogTime = value; 
		}

		// Legacy factory method
		public static LogStatistics FromLogEntries(IEnumerable<LogEntry>? entries)
		{
			var stats = new LogStatistics();
			if (entries == null)
				return stats;

			var logEntries = entries.ToList();
			stats.TotalEntries = logEntries.Count;
			
			// Count by level
			stats.ErrorEntries = logEntries.Count(e => e.Level.Trim().ToUpperInvariant() == "ERROR");
			stats.WarningEntries = logEntries.Count(e => e.Level.Trim().ToUpperInvariant() == "WARNING");
			stats.InfoEntries = logEntries.Count(e => e.Level.Trim().ToUpperInvariant() == "INFO");
			stats.DebugEntries = logEntries.Count(e => e.Level.Trim().ToUpperInvariant() == "DEBUG");
			stats.TraceEntries = logEntries.Count(e => e.Level.Trim().ToUpperInvariant() == "TRACE");
			stats.OtherEntries = stats.TotalEntries - stats.ErrorEntries - stats.WarningEntries - stats.InfoEntries - stats.DebugEntries - stats.TraceEntries;

			// Calculate percentages
			if (stats.TotalEntries > 0)
			{
				stats.ErrorPercentage = Math.Round((double)stats.ErrorEntries / stats.TotalEntries * 100, 1);
				stats.WarningPercentage = Math.Round((double)stats.WarningEntries / stats.TotalEntries * 100, 1);
				stats.InfoPercentage = Math.Round((double)stats.InfoEntries / stats.TotalEntries * 100, 1);
				stats.DebugPercentage = Math.Round((double)stats.DebugEntries / stats.TotalEntries * 100, 1);
				stats.TracePercentage = Math.Round((double)stats.TraceEntries / stats.TotalEntries * 100, 1);
				stats.OtherPercentage = Math.Round((double)stats.OtherEntries / stats.TotalEntries * 100, 1);
			}

			// Time information
			if (logEntries.Any())
			{
				stats.FirstLogTime = logEntries.Min(e => e.Timestamp);
				stats.LastLogTime = logEntries.Max(e => e.Timestamp);
				stats.TimeSpan = stats.LastLogTime - stats.FirstLogTime;

				// Calculate rates
				if (stats.TimeSpan.TotalHours > 0)
				{
					stats.LogsPerHour = stats.TotalEntries / stats.TimeSpan.TotalHours;
					stats.LogsPerMinute = stats.TotalEntries / stats.TimeSpan.TotalMinutes;
				}
			}

			// Distribution analysis
			stats.HourlyDistribution = logEntries
				.GroupBy(e => e.Timestamp.Hour)
				.ToDictionary(g => g.Key, g => g.Count());

			stats.LevelDistribution = logEntries
				.GroupBy(e => e.Level)
				.ToDictionary(g => g.Key, g => g.Count());

			stats.SourceDistribution = logEntries
				.Where(e => !string.IsNullOrEmpty(e.Source))
				.GroupBy(e => e.Source!)
				.ToDictionary(g => g.Key, g => g.Count());

			stats.TopErrors = logEntries
				.Where(e => e.Level.Trim().ToUpperInvariant() == "ERROR")
				.GroupBy(e => e.Message)
				.OrderByDescending(g => g.Count())
				.Take(10)
				.ToDictionary(g => g.Key, g => g.Count());

			stats.UniqueSources = stats.SourceDistribution.Count;
			stats.PeakHour = stats.HourlyDistribution.Any() ? 
				stats.HourlyDistribution.OrderByDescending(kvp => kvp.Value).First().Key : 0;

			return stats;
		}
	}
}

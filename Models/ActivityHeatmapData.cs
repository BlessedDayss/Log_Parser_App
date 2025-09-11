using System;
using System.Collections.Generic;
using System.Linq;

namespace Log_Parser_App.Models
{
    /// <summary>
    /// Data structure for activity heatmap visualization
    /// </summary>
    public class ActivityHeatmapData
    {
        /// <summary>
        /// Time-based activity data points
        /// </summary>
        public IEnumerable<HeatmapDataPoint> DataPoints { get; set; } = new List<HeatmapDataPoint>();

        /// <summary>
        /// Maximum activity value for scaling
        /// </summary>
        public int MaxActivityValue { get; set; }

        /// <summary>
        /// Minimum activity value for scaling
        /// </summary>
        public int MinActivityValue { get; set; }

        /// <summary>
        /// Time range start
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Time range end
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Total duration covered by heatmap
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;

        /// <summary>
        /// Time interval between data points (in minutes)
        /// </summary>
        public int IntervalMinutes { get; set; } = 60;

        /// <summary>
        /// Number of errors in the dataset
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Color scheme for heatmap visualization
        /// </summary>
        public HeatmapColorScheme ColorScheme { get; set; } = new HeatmapColorScheme();
    }

    /// <summary>
    /// Individual data point in the activity heatmap
    /// </summary>
    public class HeatmapDataPoint
    {
        /// <summary>
        /// Day of week (0 = Sunday, 6 = Saturday)
        /// </summary>
        public int DayOfWeek { get; set; }

        /// <summary>
        /// Hour of day (0-23)
        /// </summary>
        public int Hour { get; set; }

        /// <summary>
        /// Activity count for this time slot
        /// </summary>
        public int ActivityCount { get; set; }

        /// <summary>
        /// Number of errors in this time slot
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Timestamp for this data point
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Normalized activity value (0.0 to 1.0)
        /// </summary>
        public double NormalizedValue { get; set; }

        /// <summary>
        /// Color value for this data point based on activity
        /// </summary>
        public string Color { get; set; } = string.Empty;

        /// <summary>
        /// Display label for this data point
        /// </summary>
        public string Label => $"{GetDayName(DayOfWeek)} {Hour:D2}:00";

        /// <summary>
        /// Tooltip text for this data point
        /// </summary>
        public string Tooltip => $"{Label}: {ActivityCount} entries, {ErrorCount} errors";

        /// <summary>
        /// Whether this data point is clickable (has data)
        /// </summary>
        public bool IsClickable => ActivityCount > 0;

        private static string GetDayName(int dayOfWeek)
        {
            return dayOfWeek switch
            {
                0 => "Sun",
                1 => "Mon",
                2 => "Tue",
                3 => "Wed",
                4 => "Thu",
                5 => "Fri",
                6 => "Sat",
                _ => "Unknown"
            };
        }
    }

    /// <summary>
    /// Color scheme configuration for heatmap visualization
    /// </summary>
    public class HeatmapColorScheme
    {
        /// <summary>
        /// Color for no activity
        /// </summary>
        public string NoActivity { get; set; } = "#F5F5F5";

        /// <summary>
        /// Color for low activity
        /// </summary>
        public string LowActivity { get; set; } = "#C6E48B";

        /// <summary>
        /// Color for medium activity
        /// </summary>
        public string MediumActivity { get; set; } = "#7BC96F";

        /// <summary>
        /// Color for high activity
        /// </summary>
        public string HighActivity { get; set; } = "#239A3B";

        /// <summary>
        /// Color for very high activity
        /// </summary>
        public string VeryHighActivity { get; set; } = "#196127";

        /// <summary>
        /// Color for error-heavy time slots
        /// </summary>
        public string ErrorHeavy { get; set; } = "#D73A49";

        /// <summary>
        /// Get color based on normalized activity value
        /// </summary>
        /// <param name="normalizedValue">Activity value from 0.0 to 1.0</param>
        /// <param name="hasErrors">Whether this time slot has errors</param>
        /// <returns>Hex color string</returns>
        public string GetColor(double normalizedValue, bool hasErrors = false)
        {
            if (hasErrors)
                return ErrorHeavy;

            return normalizedValue switch
            {
                0.0 => NoActivity,
                <= 0.25 => LowActivity,
                <= 0.5 => MediumActivity,
                <= 0.75 => HighActivity,
                _ => VeryHighActivity
            };
        }
    }

    /// <summary>
    /// Filter criteria for heatmap data
    /// </summary>
    public class HeatmapFilter
    {
        /// <summary>
        /// Filter to show only error time slots
        /// </summary>
        public bool ErrorsOnly { get; set; }

        /// <summary>
        /// Minimum activity threshold
        /// </summary>
        public int MinActivity { get; set; }

        /// <summary>
        /// Days of week to include (0=Sunday to 6=Saturday)
        /// </summary>
        public IEnumerable<int> IncludeDays { get; set; } = new List<int> { 0, 1, 2, 3, 4, 5, 6 };

        /// <summary>
        /// Hour range to include (0-23)
        /// </summary>
        public (int Start, int End) HourRange { get; set; } = (0, 23);

        /// <summary>
        /// Whether to apply filters
        /// </summary>
        public bool IsActive => ErrorsOnly || MinActivity > 0 || 
                               IncludeDays.Count() < 7 || 
                               HourRange.Start > 0 || HourRange.End < 23;
    }
} 
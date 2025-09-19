using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Models;
using Log_Parser_App.Services.ErrorDetection.Interfaces;

namespace Log_Parser_App.Services.ErrorDetection;

public class ActivityHeatmapGenerator : IActivityHeatmapGenerator
{
    public async Task<ActivityHeatmapData> GenerateHeatmapAsync(
        IEnumerable<LogEntry> entries,
        int intervalMinutes = 60,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var entriesList = entries.ToList();

            if (!entriesList.Any())
                return new ActivityHeatmapData();

            var dataPoints = new List<HeatmapDataPoint>();
            var colorScheme = new HeatmapColorScheme();

            var timeSlots = entriesList
                .GroupBy(e => new
                {
                    DayOfWeek = (int)e.Timestamp.DayOfWeek,
                    Hour = e.Timestamp.Hour
                })
                .ToDictionary(g => g.Key, g => g.ToList());

            var maxActivity = timeSlots.Values.Max(list => list.Count);

            foreach (var slot in timeSlots)
            {
                var errorCount = slot.Value.Count(e => HasErrorKeywords(e));
                var normalizedValue = maxActivity > 0 ? slot.Value.Count / (double)maxActivity : 0;

                var dataPoint = new HeatmapDataPoint
                {
                    DayOfWeek = slot.Key.DayOfWeek,
                    Hour = slot.Key.Hour,
                    ActivityCount = slot.Value.Count,
                    ErrorCount = errorCount,
                    Timestamp = slot.Value.First().Timestamp,
                    NormalizedValue = normalizedValue,
                    Color = colorScheme.GetColor(normalizedValue, errorCount > 0)
                };

                dataPoints.Add(dataPoint);
            }

            return new ActivityHeatmapData
            {
                DataPoints = dataPoints.OrderBy(dp => dp.DayOfWeek).ThenBy(dp => dp.Hour),
                MaxActivityValue = maxActivity,
                MinActivityValue = timeSlots.Values.Min(list => list.Count),
                StartTime = entriesList.Min(e => e.Timestamp),
                EndTime = entriesList.Max(e => e.Timestamp),
                IntervalMinutes = intervalMinutes,
                ErrorCount = entriesList.Count(HasErrorKeywords),
                ColorScheme = colorScheme
            };
        }, cancellationToken);
    }

    public IEnumerable<LogEntry> FilterByHeatmapSelection(
        IEnumerable<LogEntry> entries,
        HeatmapDataPoint selectedDataPoint)
    {
        return entries.Where(entry =>
            (int)entry.Timestamp.DayOfWeek == selectedDataPoint.DayOfWeek &&
            entry.Timestamp.Hour == selectedDataPoint.Hour);
    }

    private bool HasErrorKeywords(LogEntry entry)
    {
        var message = entry.Message ?? string.Empty;
        var errorKeywords = new[] { "Error", "Exception", "DbOperationException", "PostgresException", "Invalid" };

        return errorKeywords.Any(keyword =>
            message.Contains(keyword, System.StringComparison.OrdinalIgnoreCase));
    }
}



using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Services.ErrorDetection.Interfaces;

public interface IActivityHeatmapGenerator
{
    Task<ActivityHeatmapData> GenerateHeatmapAsync(
        IEnumerable<LogEntry> entries,
        int intervalMinutes = 60,
        CancellationToken cancellationToken = default);

    IEnumerable<LogEntry> FilterByHeatmapSelection(
        IEnumerable<LogEntry> entries,
        HeatmapDataPoint selectedDataPoint);
}

using System.Collections.Generic;
using System.Linq;
using Log_Parser_App.Models;
using Log_Parser_App.Services.ErrorDetection.Interfaces;

namespace Log_Parser_App.Services.ErrorDetection;

public class ErrorNavigator : IErrorNavigator
{
    public ErrorNavigationInfo GetErrorNavigation(IEnumerable<LogEntry> entries, int currentIndex)
    {
        var entriesList = entries.ToList();
        var errorEntries = entriesList
            .Select((entry, index) => new { Entry = entry, Index = index })
            .Where(x => HasErrorKeywords(x.Entry))
            .ToList();

        return new ErrorNavigationInfo
        {
            TotalErrors = errorEntries.Count,
            CurrentIndex = System.Math.Max(0, System.Math.Min(currentIndex, errorEntries.Count - 1)),
            ErrorIndices = errorEntries.Select(x => x.Index)
        };
    }

    public IEnumerable<LogEntry> FilterByErrorNavigation(IEnumerable<LogEntry> entries)
    {
        return entries.Where(HasErrorKeywords);
    }

    private bool HasErrorKeywords(LogEntry entry)
    {
        var message = entry.Message ?? string.Empty;
        var errorKeywords = new[] { "Error", "Exception", "DbOperationException", "PostgresException", "Invalid" };

        return errorKeywords.Any(keyword =>
            message.Contains(keyword, System.StringComparison.OrdinalIgnoreCase));
    }
}



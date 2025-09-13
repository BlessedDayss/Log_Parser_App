using System.Collections.Generic;
using Log_Parser_App.Models;

namespace Log_Parser_App.Services.ErrorDetection.Interfaces;

public interface IErrorNavigator
{
    ErrorNavigationInfo GetErrorNavigation(IEnumerable<LogEntry> entries, int currentIndex);
    IEnumerable<LogEntry> FilterByErrorNavigation(IEnumerable<LogEntry> entries);
}

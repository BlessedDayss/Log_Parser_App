using System.Collections.Generic;
using Log_Parser_App.Models;

namespace Log_Parser_App.Services.ErrorDetection.Interfaces;

public interface IStackTraceParser
{
    IEnumerable<StackTraceInfo> ParseStackTraces(IEnumerable<LogEntry> entries);
}

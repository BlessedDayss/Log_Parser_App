using System.Collections.Generic;
using Log_Parser_App.Models;

namespace Log_Parser_App.Services.ErrorDetection.Interfaces;

public interface IKeywordDetector
{
    IEnumerable<ErrorKeywordMatch> DetectKeywords(IEnumerable<LogEntry> entries);
    IReadOnlyList<string> GetDetectableKeywords();
    ErrorType ClassifyErrorKeyword(string keyword);
    string GetErrorHighlightColor(ErrorType errorType);
}

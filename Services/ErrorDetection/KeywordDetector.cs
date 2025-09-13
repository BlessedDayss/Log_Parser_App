using System.Collections.Generic;
using System.Linq;
using Log_Parser_App.Models;
using Log_Parser_App.Services.ErrorDetection.Interfaces;

namespace Log_Parser_App.Services.ErrorDetection;

public class KeywordDetector : IKeywordDetector
{
    private readonly Dictionary<string, ErrorType> _errorKeywords = new()
    {
        { "Error", ErrorType.Error },
        { "Exception", ErrorType.Exception },
        { "DbOperationException", ErrorType.DatabaseError },
        { "PostgresException", ErrorType.DatabaseError },
        { "Invalid", ErrorType.ValidationError },
        { "RootAlreadyExists", ErrorType.ValidationError }
    };

    public IEnumerable<ErrorKeywordMatch> DetectKeywords(IEnumerable<LogEntry> entries)
    {
        var matches = new List<ErrorKeywordMatch>();
        var errorIndex = 0;

        foreach (var entry in entries)
        {
            var entryMatches = DetectKeywordsInEntry(entry, errorIndex);
            matches.AddRange(entryMatches);

            if (entryMatches.Any())
                errorIndex++;
        }

        return matches;
    }

    public IReadOnlyList<string> GetDetectableKeywords()
    {
        return _errorKeywords.Keys.ToList().AsReadOnly();
    }

    public ErrorType ClassifyErrorKeyword(string keyword)
    {
        return _errorKeywords.GetValueOrDefault(keyword, ErrorType.Unknown);
    }

    public string GetErrorHighlightColor(ErrorType errorType)
    {
        return errorType switch
        {
            ErrorType.Error => "#FFEBEE",
            ErrorType.Exception => "#FFF3E0",
            ErrorType.DatabaseError => "#F3E5F5",
            ErrorType.ValidationError => "#FFFDE7",
            _ => "#F5F5F5"
        };
    }

    private IEnumerable<ErrorKeywordMatch> DetectKeywordsInEntry(LogEntry entry, int errorIndex)
    {
        var matches = new List<ErrorKeywordMatch>();
        var message = entry.Message ?? string.Empty;

        foreach (var keywordPair in _errorKeywords)
        {
            var keyword = keywordPair.Key;
            var errorType = keywordPair.Value;

            var index = message.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                matches.Add(new ErrorKeywordMatch
                {
                    Keyword = keyword,
                    ErrorType = errorType,
                    Position = index,
                    Length = keyword.Length,
                    BackgroundColor = GetErrorHighlightColor(errorType),
                    LogEntry = entry,
                    ErrorIndex = errorIndex
                });
            }
        }

        return matches;
    }
}


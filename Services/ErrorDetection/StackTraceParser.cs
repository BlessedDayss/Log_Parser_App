using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Log_Parser_App.Models;
using Log_Parser_App.Services.ErrorDetection.Interfaces;

namespace Log_Parser_App.Services.ErrorDetection;

public class StackTraceParser : IStackTraceParser
{
    public IEnumerable<StackTraceInfo> ParseStackTraces(IEnumerable<LogEntry> entries)
    {
        var stackTraces = new List<StackTraceInfo>();

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.StackTrace))
                continue;

            var stackTraceInfo = ParseStackTrace(entry);
            if (stackTraceInfo.Frames.Any())
                stackTraces.Add(stackTraceInfo);
        }

        return stackTraces;
    }

    private StackTraceInfo ParseStackTrace(LogEntry entry)
    {
        var stackTrace = entry.StackTrace ?? string.Empty;
        var frames = new List<StackFrame>();

        var lines = stackTrace.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;

            var frame = ParseStackFrame(trimmedLine);
            if (frame != null)
                frames.Add(frame);
        }

        return new StackTraceInfo
        {
            LogEntry = entry,
            Frames = frames,
            IsParsed = frames.Any()
        };
    }

    private StackFrame? ParseStackFrame(string frameText)
    {
        var atMatch = Regex.Match(frameText, @"at\s+(.+)");
        if (!atMatch.Success)
            return null;

        var methodInfo = atMatch.Groups[1].Value;
        var fileMatch = Regex.Match(methodInfo, @"in\s+(.+):line\s+(\d+)");

        return new StackFrame
        {
            Method = ExtractMethodName(methodInfo),
            Class = ExtractClassName(methodInfo),
            FileName = fileMatch.Success ? fileMatch.Groups[1].Value : null,
            LineNumber = fileMatch.Success && int.TryParse(fileMatch.Groups[2].Value, out var lineNum) ? lineNum : null,
            RawText = frameText
        };
    }

    private string? ExtractMethodName(string methodInfo)
    {
        var methodMatch = Regex.Match(methodInfo, @"([^.]+\([^)]*\))");
        return methodMatch.Success ? methodMatch.Groups[1].Value : null;
    }

    private string? ExtractClassName(string methodInfo)
    {
        var parts = methodInfo.Split('.');
        return parts.Length > 1 ? string.Join(".", parts.Take(parts.Length - 1)) : null;
    }
}



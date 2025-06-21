using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.ErrorDetection
{
    /// <summary>
    /// Advanced error detection service implementing pipeline pattern for extensible error processing
    /// Provides keyword-based detection, stack trace parsing, and activity heatmap generation
    /// </summary>
    public class AdvancedErrorDetectionService : IAdvancedErrorDetectionService
    {
        private readonly ILogger<AdvancedErrorDetectionService> _logger;
        private readonly AdvancedErrorDetectionConfig _config;
        private readonly Dictionary<string, Regex> _keywordRegexCache;

        public event EventHandler<ErrorAnalysisCompletedEventArgs>? ErrorAnalysisCompleted;
        public event EventHandler<ErrorNavigationChangedEventArgs>? ErrorNavigationChanged;

        public AdvancedErrorDetectionService(ILogger<AdvancedErrorDetectionService> logger)
        {
            _logger = logger;
            _config = new AdvancedErrorDetectionConfig();
            _keywordRegexCache = InitializeKeywordRegexCache();
        }

        public AdvancedErrorDetectionService(
            ILogger<AdvancedErrorDetectionService> logger,
            AdvancedErrorDetectionConfig config)
        {
            _logger = logger;
            _config = config;
            _keywordRegexCache = InitializeKeywordRegexCache();
        }

        public async Task<ErrorAnalysisResult> AnalyzeErrorsAsync(
            IEnumerable<LogEntry> entries, 
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var entriesList = entries.ToList();
            
            _logger.LogInformation("Starting error analysis for {EntryCount} entries", entriesList.Count);

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_config.TimeoutMs);

                var result = new ErrorAnalysisResult();

                // Pipeline: KeywordDetection → StackTrace → Classification → Navigation
                var tasks = new List<Task>();

                // Keyword detection pipeline
                var keywordTask = ProcessKeywordDetectionPipelineAsync(entriesList, cts.Token);
                tasks.Add(keywordTask);

                // Stack trace parsing pipeline (if enabled)
                Task<IEnumerable<StackTraceInfo>>? stackTraceTask = null;
                if (_config.EnableStackTraceParsing)
                {
                    stackTraceTask = ParseStackTracesAsync(entriesList, cts.Token);
                    tasks.Add(stackTraceTask);
                }

                // Activity heatmap generation (if enabled)
                Task<ActivityHeatmapData>? heatmapTask = null;
                if (_config.EnableActivityHeatmap)
                {
                    heatmapTask = GenerateActivityHeatmapAsync(entriesList, cancellationToken: cts.Token);
                    tasks.Add(heatmapTask);
                }

                await Task.WhenAll(tasks);

                // Assemble results
                result.Keywords = await keywordTask;
                result.TotalErrorCount = result.Keywords.Count();

                if (stackTraceTask != null)
                    result.StackTraces = await stackTraceTask;

                if (heatmapTask != null)
                    result.HeatmapData = await heatmapTask;

                // Generate navigation info
                result.Navigation = GenerateNavigationInfo(result.Keywords);

                var analysisTime = DateTime.UtcNow - startTime;
                _logger.LogInformation("Error analysis completed in {AnalysisTime}ms. Found {ErrorCount} errors", 
                    analysisTime.TotalMilliseconds, result.TotalErrorCount);

                // Fire completion event
                ErrorAnalysisCompleted?.Invoke(this, new ErrorAnalysisCompletedEventArgs(
                    result, entriesList.Count, analysisTime));

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Error analysis was cancelled");
                var emptyResult = new ErrorAnalysisResult();
                ErrorAnalysisCompleted?.Invoke(this, new ErrorAnalysisCompletedEventArgs(
                    emptyResult, entriesList.Count, DateTime.UtcNow - startTime, false, "Analysis cancelled"));
                return emptyResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during error analysis");
                var emptyResult = new ErrorAnalysisResult();
                ErrorAnalysisCompleted?.Invoke(this, new ErrorAnalysisCompletedEventArgs(
                    emptyResult, entriesList.Count, DateTime.UtcNow - startTime, false, ex.Message));
                return emptyResult;
            }
        }

        public async Task<IEnumerable<LogEntry>> HighlightErrorKeywordsAsync(
            IEnumerable<LogEntry> entries, 
            CancellationToken cancellationToken = default)
        {
            var entriesList = entries.ToList();
            var keywords = await ProcessKeywordDetectionPipelineAsync(entriesList, cancellationToken);
            
            // Create a lookup for faster access
            var keywordLookup = keywords.ToLookup(k => k.LogEntry);

            return entriesList.Select(entry =>
            {
                var entryKeywords = keywordLookup[entry];
                if (!entryKeywords.Any())
                    return entry;

                // Clone entry with highlighting information
                var highlightedEntry = new LogEntry
                {
                    Timestamp = entry.Timestamp,
                    Level = entry.Level,
                    Message = entry.Message,
                    Source = entry.Source,
                    StackTrace = entry.StackTrace,
                    Logger = entry.Logger,
                    CorrelationId = entry.CorrelationId,
                    ErrorType = entry.ErrorType,
                    ErrorDescription = entry.ErrorDescription,
                    ErrorRecommendations = entry.ErrorRecommendations,
                    FilePath = entry.FilePath,
                    LineNumber = entry.LineNumber,
                    SourceTabTitle = entry.SourceTabTitle,
                    Recommendation = entry.Recommendation,
                    OpenFileCommand = entry.OpenFileCommand
                };

                return highlightedEntry;
            });
        }

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
                CurrentIndex = Math.Max(0, Math.Min(currentIndex, errorEntries.Count - 1)),
                ErrorIndices = errorEntries.Select(x => x.Index)
            };
        }

        public async Task<ActivityHeatmapData> GenerateActivityHeatmapAsync(
            IEnumerable<LogEntry> entries,
            int intervalMinutes = 60,
            CancellationToken cancellationToken = default)
        {
            var entriesList = entries.ToList();
            
            if (!entriesList.Any())
                return new ActivityHeatmapData();

            var startTime = entriesList.Min(e => e.Timestamp);
            var endTime = entriesList.Max(e => e.Timestamp);

            var dataPoints = new List<HeatmapDataPoint>();
            var colorScheme = new HeatmapColorScheme();

            // Group entries by time slots
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
                StartTime = startTime,
                EndTime = endTime,
                IntervalMinutes = intervalMinutes,
                ErrorCount = entriesList.Count(HasErrorKeywords),
                ColorScheme = colorScheme
            };
        }

        public async Task<IEnumerable<StackTraceInfo>> ParseStackTracesAsync(
            IEnumerable<LogEntry> entries,
            CancellationToken cancellationToken = default)
        {
            var stackTraces = new List<StackTraceInfo>();

            await Task.Run(() =>
            {
                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (string.IsNullOrEmpty(entry.StackTrace))
                        continue;

                    var stackTraceInfo = ParseStackTrace(entry);
                    if (stackTraceInfo.Frames.Any())
                        stackTraces.Add(stackTraceInfo);
                }
            }, cancellationToken);

            return stackTraces;
        }

        public IEnumerable<LogEntry> FilterByHeatmapSelection(
            IEnumerable<LogEntry> entries, 
            HeatmapDataPoint selectedDataPoint)
        {
            return entries.Where(entry =>
                (int)entry.Timestamp.DayOfWeek == selectedDataPoint.DayOfWeek &&
                entry.Timestamp.Hour == selectedDataPoint.Hour);
        }

        public IReadOnlyList<string> GetDetectableKeywords()
        {
            return _config.ErrorKeywords.Keys.ToList().AsReadOnly();
        }

        public ErrorType ClassifyErrorKeyword(string keyword)
        {
            return _config.ErrorKeywords.GetValueOrDefault(keyword, ErrorType.Unknown);
        }

        public string GetErrorHighlightColor(ErrorType errorType)
        {
            return _config.ErrorColors.GetValueOrDefault(errorType, "#F5F5F5");
        }

        #region Private Pipeline Methods

        private async Task<IEnumerable<ErrorKeywordMatch>> ProcessKeywordDetectionPipelineAsync(
            IList<LogEntry> entries, 
            CancellationToken cancellationToken)
        {
            var matches = new List<ErrorKeywordMatch>();
            var errorIndex = 0;

            await Task.Run(() =>
            {
                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var entryMatches = DetectKeywordsInEntry(entry, errorIndex);
                    matches.AddRange(entryMatches);
                    
                    if (entryMatches.Any())
                        errorIndex++;
                }
            }, cancellationToken);

            return matches;
        }

        private IEnumerable<ErrorKeywordMatch> DetectKeywordsInEntry(LogEntry entry, int errorIndex)
        {
            var matches = new List<ErrorKeywordMatch>();
            var message = entry.Message ?? string.Empty;

            foreach (var keywordPair in _config.ErrorKeywords)
            {
                if (_keywordRegexCache.TryGetValue(keywordPair.Key, out var regex))
                {
                    var regexMatches = regex.Matches(message);
                    foreach (Match match in regexMatches)
                    {
                        matches.Add(new ErrorKeywordMatch
                        {
                            Keyword = keywordPair.Key,
                            ErrorType = keywordPair.Value,
                            Position = match.Index,
                            Length = match.Length,
                            BackgroundColor = GetErrorHighlightColor(keywordPair.Value),
                            LogEntry = entry,
                            ErrorIndex = errorIndex
                        });
                    }
                }
            }

            return matches;
        }

        private StackTraceInfo ParseStackTrace(LogEntry entry)
        {
            var stackTrace = entry.StackTrace ?? string.Empty;
            var frames = new List<StackFrame>();

            // Simple stack trace parsing - can be enhanced
            var lines = stackTrace.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
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
            // Basic stack frame parsing - enhanced version would handle more formats
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

        private ErrorNavigationInfo GenerateNavigationInfo(IEnumerable<ErrorKeywordMatch> keywords)
        {
            var keywordsList = keywords.ToList();
            var errorIndices = keywordsList.Select(k => k.ErrorIndex).Distinct().ToList();

            return new ErrorNavigationInfo
            {
                TotalErrors = errorIndices.Count,
                CurrentIndex = 0,
                ErrorIndices = errorIndices
            };
        }

        private bool HasErrorKeywords(LogEntry entry)
        {
            var message = entry.Message ?? string.Empty;
            return _config.ErrorKeywords.Keys.Any(keyword =>
                message.Contains(keyword, _config.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase));
        }

        private Dictionary<string, Regex> InitializeKeywordRegexCache()
        {
            var cache = new Dictionary<string, Regex>();
            var options = _config.CaseSensitive ? RegexOptions.Compiled : RegexOptions.Compiled | RegexOptions.IgnoreCase;

            foreach (var keyword in _config.ErrorKeywords.Keys)
            {
                // Escape special regex characters and create word boundary pattern
                var escapedKeyword = Regex.Escape(keyword);
                var pattern = $@"\b{escapedKeyword}\b";
                cache[keyword] = new Regex(pattern, options);
            }

            return cache;
        }

        #endregion
    }
} 
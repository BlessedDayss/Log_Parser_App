using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Models;
using Log_Parser_App.Services.ErrorDetection.Interfaces;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.ErrorDetection
{
    public class AdvancedErrorDetectionService : IAdvancedErrorDetectionService
    {
        private readonly ILogger<AdvancedErrorDetectionService> _logger;
        private readonly IKeywordDetector _keywordDetector;
        private readonly IStackTraceParser _stackTraceParser;
        private readonly IActivityHeatmapGenerator _heatmapGenerator;
        private readonly IErrorNavigator _errorNavigator;

        public event EventHandler<ErrorAnalysisCompletedEventArgs>? ErrorAnalysisCompleted;
        public event EventHandler<ErrorNavigationChangedEventArgs>? ErrorNavigationChanged;

        public AdvancedErrorDetectionService(
            ILogger<AdvancedErrorDetectionService> logger,
            IKeywordDetector keywordDetector,
            IStackTraceParser stackTraceParser,
            IActivityHeatmapGenerator heatmapGenerator,
            IErrorNavigator errorNavigator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _keywordDetector = keywordDetector ?? throw new ArgumentNullException(nameof(keywordDetector));
            _stackTraceParser = stackTraceParser ?? throw new ArgumentNullException(nameof(stackTraceParser));
            _heatmapGenerator = heatmapGenerator ?? throw new ArgumentNullException(nameof(heatmapGenerator));
            _errorNavigator = errorNavigator ?? throw new ArgumentNullException(nameof(errorNavigator));
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
                cts.CancelAfter(TimeSpan.FromSeconds(30));

                var result = new ErrorAnalysisResult();

                var tasks = new List<Task>();
                var keywordTask = Task.Run(() => _keywordDetector.DetectKeywords(entriesList), cts.Token);
                tasks.Add(keywordTask);

                var stackTraceTask = Task.Run(() => _stackTraceParser.ParseStackTraces(entriesList), cts.Token);
                    tasks.Add(stackTraceTask);

                var heatmapTask = _heatmapGenerator.GenerateHeatmapAsync(entriesList, 60, cts.Token);
                    tasks.Add(heatmapTask);

                await Task.WhenAll(tasks);

                result.Keywords = await keywordTask;
                result.TotalErrorCount = result.Keywords.Count();
                    result.StackTraces = await stackTraceTask;
                    result.HeatmapData = await heatmapTask;
                result.Navigation = _errorNavigator.GetErrorNavigation(entriesList, 0);

                var analysisTime = DateTime.UtcNow - startTime;
                _logger.LogInformation("Error analysis completed in {AnalysisTime}ms. Found {ErrorCount} errors", 
                    analysisTime.TotalMilliseconds, result.TotalErrorCount);

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
            var keywords = await Task.Run(() => _keywordDetector.DetectKeywords(entriesList), cancellationToken);
            var keywordLookup = keywords.ToLookup(k => k.LogEntry);

            return entriesList.Select(entry =>
            {
                var entryKeywords = keywordLookup[entry];
                if (!entryKeywords.Any())
                    return entry;

                return new LogEntry
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
            });
        }

        public ErrorNavigationInfo GetErrorNavigation(IEnumerable<LogEntry> entries, int currentIndex)
        {
            return _errorNavigator.GetErrorNavigation(entries, currentIndex);
        }

        public Task<ActivityHeatmapData> GenerateActivityHeatmapAsync(
            IEnumerable<LogEntry> entries,
            int intervalMinutes = 60,
            CancellationToken cancellationToken = default)
        {
            return _heatmapGenerator.GenerateHeatmapAsync(entries, intervalMinutes, cancellationToken);
        }

        public Task<IEnumerable<StackTraceInfo>> ParseStackTracesAsync(
            IEnumerable<LogEntry> entries,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() => _stackTraceParser.ParseStackTraces(entries), cancellationToken);
        }

        public IEnumerable<LogEntry> FilterByHeatmapSelection(
            IEnumerable<LogEntry> entries, 
            HeatmapDataPoint selectedDataPoint)
        {
            return _heatmapGenerator.FilterByHeatmapSelection(entries, selectedDataPoint);
        }

        public IReadOnlyList<string> GetDetectableKeywords()
        {
            return _keywordDetector.GetDetectableKeywords();
        }

        public ErrorType ClassifyErrorKeyword(string keyword)
        {
            return _keywordDetector.ClassifyErrorKeyword(keyword);
        }

        public string GetErrorHighlightColor(ErrorType errorType)
        {
            return _keywordDetector.GetErrorHighlightColor(errorType);
        }

    }
} 
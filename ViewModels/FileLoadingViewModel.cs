using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Log_Parser_App.Models;
using Log_Parser_App.Services;
using Log_Parser_App.Interfaces;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.ViewModels
{
    /// <summary>
    /// ViewModel responsible for file loading operations
    /// Follows Single Responsibility Principle
    /// </summary>
    public partial class FileLoadingViewModel : ViewModelBase
    {
        #region Dependencies

        private readonly ILogParserService _logParserService;
        private readonly IIISLogParserService _iisLogParserService; 
        private readonly IRabbitMqLogParserService _rabbitMqLogParserService;
        private readonly IFilePickerService _filePickerService;
        private readonly ISimpleErrorRecommendationService _simpleErrorRecommendationService;
        private readonly ILogger<FileLoadingViewModel> _logger;

        #endregion

        #region Properties

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = "Ready to work";

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private string _fileStatus = "No file selected";

        public string? LastOpenedFilePath { get; set; }

        #endregion

        #region Events

        /// <summary>
        /// Event fired when file loading starts
        /// </summary>
        public event EventHandler<FileLoadingStartedEventArgs>? FileLoadingStarted;

        /// <summary>
        /// Event fired when file loading completes successfully
        /// </summary>
        public event EventHandler<FileLoadedEventArgs>? FileLoaded;

        /// <summary>
        /// Event fired when file loading fails
        /// </summary>
        public event EventHandler<FileLoadingFailedEventArgs>? FileLoadingFailed;

        #endregion

        #region Constructor

        public FileLoadingViewModel(
            ILogParserService logParserService,
            IIISLogParserService iisLogParserService,
            IRabbitMqLogParserService rabbitMqLogParserService,
            IFilePickerService filePickerService,
            ISimpleErrorRecommendationService simpleErrorRecommendationService,
            ILogger<FileLoadingViewModel> logger)
        {
            _logParserService = logParserService;
            _iisLogParserService = iisLogParserService;
            _rabbitMqLogParserService = rabbitMqLogParserService;
            _filePickerService = filePickerService;
            _simpleErrorRecommendationService = simpleErrorRecommendationService;
            _logger = logger;
        }

        #endregion

        #region Commands

        [RelayCommand]
        private async Task LoadFile()
        {
            try
            {
                var files = await _filePickerService.PickFilesAsync(null);
                if (files != null && files.Any())
                {
                    await LoadFileAsync(files.First());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file selection");
                StatusMessage = $"Error selecting file: {ex.Message}";
                OnFileLoadingFailed(new FileLoadingFailedEventArgs(ex));
            }
        }

        [RelayCommand]
        private async Task LoadDirectory()
        {
            try
            {
                var directory = await _filePickerService.PickDirectoryAsync(null);
                if (!string.IsNullOrEmpty(directory))
                {
                    await LoadDirectoryAsync(directory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during directory selection");
                StatusMessage = $"Error selecting directory: {ex.Message}";
                OnFileLoadingFailed(new FileLoadingFailedEventArgs(ex));
            }
        }

        [RelayCommand]
        private async Task LoadIISLogs()
        {
            try
            {
                var files = await _filePickerService.PickFilesAsync(null);
                if (files != null && files.Any())
                {
                    foreach (var file in files)
                    {
                        await LoadIISFileAsync(file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading IIS logs");
                StatusMessage = $"Error loading IIS logs: {ex.Message}";
                OnFileLoadingFailed(new FileLoadingFailedEventArgs(ex));
            }
        }

        [RelayCommand]
        private async Task LoadRabbitMqLogs()
        {
            try
            {
                var files = await _filePickerService.PickFilesAsync(null);
                if (files != null && files.Any())
                {
                    await LoadRabbitMqFilesAsync(files);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading RabbitMQ logs");
                StatusMessage = $"Error loading RabbitMQ logs: {ex.Message}";
                OnFileLoadingFailed(new FileLoadingFailedEventArgs(ex));
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Load a single file
        /// </summary>
        public async Task LoadFileAsync(string filePath)
        {
            try
            {
                OnFileLoadingStarted(new FileLoadingStartedEventArgs(filePath));

                StatusMessage = $"Opening {Path.GetFileName(filePath)}...";
                IsLoading = true;
                FileStatus = Path.GetFileName(filePath);
                FilePath = filePath;

                _logger.LogInformation("Starting file load: {FilePath}", filePath);

                // Determine file type
                var isIISLog = await IsIISLogFileAsync(filePath);
                var logType = isIISLog ? LogFormatType.IIS : LogFormatType.Standard;

                _logger.LogInformation("File {FilePath} detected as {LogType}", filePath, logType);

                List<LogEntry> entries;

                if (isIISLog)
                {
                    entries = await LoadIISLogEntriesAsync(filePath);
                }
                else
                {
                    entries = await LoadStandardLogEntriesAsync(filePath);
                }

                // Process entries with error recommendations
                await ProcessErrorRecommendationsAsync(entries);

                StatusMessage = $"Loaded {entries.Count} entries from {Path.GetFileName(filePath)}";
                _logger.LogInformation("Successfully loaded {Count} entries from {FilePath}", entries.Count, filePath);

                OnFileLoaded(new FileLoadedEventArgs(filePath, entries, logType));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading file: {FilePath}", filePath);
                StatusMessage = $"Error loading file: {ex.Message}";
                OnFileLoadingFailed(new FileLoadingFailedEventArgs(ex));
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Private Methods

        private async Task<List<LogEntry>> LoadStandardLogEntriesAsync(string filePath)
        {
            var entries = new List<LogEntry>();
            int processedCount = 0;
            int failedCount = 0;

            try
            {
                await foreach (var entry in _logParserService.ParseLogFileAsync(filePath, CancellationToken.None))
                {
                    try
                    {
                        ProcessLogEntry(entry);
                        entries.Add(entry);
                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        _logger.LogWarning(ex, "Failed to process log entry at line {LineNumber}",
                            entry?.LineNumber ?? -1);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing standard log file: {FilePath}", filePath);
                throw;
            }

            _logger.LogInformation("Processed {ProcessedCount} entries, failed {FailedCount} entries from {FilePath}",
                processedCount, failedCount, filePath);

            return entries;
        }

        private async Task<List<LogEntry>> LoadIISLogEntriesAsync(string filePath)
        {
            try
            {
                var entriesEnumerable = _iisLogParserService.ParseLogFileAsync(filePath, CancellationToken.None);
                var entries = new List<LogEntry>();
                await foreach (var iisEntry in entriesEnumerable)
                {
                    // Convert IIS entry to LogEntry
                    var logEntry = new LogEntry
                    {
                        Timestamp = iisEntry.DateTime?.DateTime ?? System.DateTime.Now,
                        Level = DetermineIISLogLevel(iisEntry),
                        Message = $"{iisEntry.Method} {iisEntry.UriStem} - Status: {iisEntry.HttpStatus}",
                        Source = iisEntry.ServerIPAddress ?? "IIS",
                        RawData = iisEntry.RawLine ?? string.Empty
                    };
                    entries.Add(logEntry);
                }
                return entries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing IIS log file: {FilePath}", filePath);
                throw;
            }
        }

        private async Task<bool> IsIISLogFileAsync(string filePath)
        {
            try
            {
                using var reader = new StreamReader(filePath);
                for (int i = 0; i < 10 && !reader.EndOfStream; i++)
                {
                    var line = await reader.ReadLineAsync();
                    if (line != null && (line.StartsWith("#Fields:") || line.Contains("sc-status")))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking if file is IIS log: {FilePath}", filePath);
                return false;
            }
        }

        private void ProcessLogEntry(LogEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.Message))
            {
                var lines = entry.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var mainLine = lines.FirstOrDefault(l => !l.TrimStart().StartsWith("at ")) ?? 
                               (lines.Length > 0 ? lines[0] : string.Empty);
                
                var stackLines = lines.Where(l => l.TrimStart().StartsWith("at ")).ToList();
                
                entry.Message = mainLine.Trim();
                entry.StackTrace = stackLines.Any() ? string.Join("\n", stackLines) : null;
            }
        }

        private async Task ProcessErrorRecommendationsAsync(List<LogEntry> entries)
        {
            var errorEntries = entries.Where(e => 
                e.Level?.Equals("Error", StringComparison.OrdinalIgnoreCase) == true).ToList();

            if (!errorEntries.Any()) return;

            await Task.Run(() =>
            {
                Parallel.ForEach(errorEntries, entry =>
                {
                    try
                    {
                        var simpleResult = _simpleErrorRecommendationService.AnalyzeError(entry.Message);
                        if (simpleResult != null)
                        {
                            entry.ErrorType = "PatternMatch";
                            entry.ErrorDescription = simpleResult.Message;
                            entry.ErrorRecommendations.Clear();
                            entry.ErrorRecommendations.Add(simpleResult.Fix);
                            entry.Recommendation = simpleResult.Fix;
                        }
                        else
                        {
                            entry.ErrorType = "UnknownError";
                            entry.ErrorDescription = "Unknown error pattern";
                            entry.ErrorRecommendations.Clear();
                            entry.ErrorRecommendations.Add("Please contact developer to add this error pattern");
                            entry.Recommendation = "Please contact developer to add this error pattern";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing recommendations for entry {LineNumber}", 
                            entry.LineNumber);
                    }
                });
            });
        }

        private async Task LoadDirectoryAsync(string directory)
        {
            try
            {
                var logFiles = Directory.GetFiles(directory, "*.log", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(directory, "*.txt", SearchOption.AllDirectories))
                    .ToArray();

                if (!logFiles.Any())
                {
                    StatusMessage = "No log files found in directory";
                    return;
                }

                foreach (var file in logFiles)
                {
                    await LoadFileAsync(file);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading directory: {Directory}", directory);
                throw;
            }
        }

        private async Task LoadIISFileAsync(string filePath)
        {
            await LoadFileAsync(filePath); // IIS detection is handled in LoadFileAsync
        }

        private async Task LoadRabbitMqFilesAsync(IEnumerable<string> filePaths)
        {
            foreach (var filePath in filePaths)
            {
                try
                {
                    var entriesEnumerable = _rabbitMqLogParserService.ParseLogFileAsync(filePath, CancellationToken.None);
                    var entries = new List<LogEntry>();
                    await foreach (var rabbitEntry in entriesEnumerable)
                    {
                        // Convert RabbitMQ entry to LogEntry using built-in method
                        var logEntry = rabbitEntry.ToLogEntry();
                        entries.Add(logEntry);
                    }
                    OnFileLoaded(new FileLoadedEventArgs(filePath, entries, LogFormatType.RabbitMQ));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading RabbitMQ file: {FilePath}", filePath);
                    OnFileLoadingFailed(new FileLoadingFailedEventArgs(ex));
                }
            }
        }

        #endregion

        #region Event Handlers

        private void OnFileLoadingStarted(FileLoadingStartedEventArgs e)
        {
            FileLoadingStarted?.Invoke(this, e);
        }

        private void OnFileLoaded(FileLoadedEventArgs e)
        {
            FileLoaded?.Invoke(this, e);
        }

        private void OnFileLoadingFailed(FileLoadingFailedEventArgs e)
        {
            FileLoadingFailed?.Invoke(this, e);
        }

        private string DetermineIISLogLevel(Models.IisLogEntry iisEntry)
        {
            if (iisEntry.HttpStatus >= 500) return "Error";
            if (iisEntry.HttpStatus >= 400) return "Warning";
            if (iisEntry.HttpStatus >= 300) return "Info";
            return "Info";
        }

        #endregion
    }

    #region Event Args

    public class FileLoadingStartedEventArgs : EventArgs
    {
        public string FilePath { get; }

        public FileLoadingStartedEventArgs(string filePath)
        {
            FilePath = filePath;
        }
    }

    public class FileLoadedEventArgs : EventArgs
    {
        public string FilePath { get; }
        public List<LogEntry> LogEntries { get; }
        public LogFormatType LogType { get; }

        public FileLoadedEventArgs(string filePath, List<LogEntry> logEntries, LogFormatType logType)
        {
            FilePath = filePath;
            LogEntries = logEntries;
            LogType = logType;
        }
    }

    public class FileLoadingFailedEventArgs : EventArgs
    {
        public Exception Exception { get; }

        public FileLoadingFailedEventArgs(Exception exception)
        {
            Exception = exception;
        }
    }

    #endregion
} 
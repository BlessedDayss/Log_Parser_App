using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services
{
    /// <summary>
    /// Business logic service implementation for IIS log operations
    /// Single Responsibility: Business operations for IIS logs only
    /// Open/Closed: Extensible through interfaces
    /// Dependency Inversion: Depends on abstractions
    /// </summary>
    public class IISService : IIISService
    {
        private readonly IIISRepository _repository;
        private readonly ILogger<IISService> _logger;

        public IISService(IIISRepository repository, ILogger<IISService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Load and process IIS logs from files or directory
        /// </summary>
        public async Task<IISProcessingResult> ProcessIISLogsAsync(IEnumerable<string> paths, bool isDirectory = false, CancellationToken cancellationToken = default)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = new IISProcessingResult();

            try
            {
                _logger.LogInformation("Starting IIS log processing for {PathCount} paths, isDirectory: {IsDirectory}", paths.Count(), isDirectory);

                var filePaths = isDirectory ? GetLogFilesFromDirectories(paths) : paths;
                var validationResults = await _repository.ValidateIISFilesAsync(filePaths);

                var validFiles = validationResults.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
                var invalidFiles = validationResults.Where(kvp => !kvp.Value).Select(kvp => kvp.Key).ToList();

                result.Metadata.FilesProcessed = validFiles.Count;
                result.Metadata.FilesSkipped = invalidFiles.Count;
                result.Metadata.ProcessedFiles = validFiles;
                result.Metadata.SkippedFiles = invalidFiles;

                if (validFiles.Any())
                {
                    var entries = await _repository.LoadIISLogsAsync(validFiles, cancellationToken);
                    result.Entries = entries.ToList();
                    result.Statistics = _repository.GetStatistics(entries);
                    result.Metadata.EntriesProcessed = entries.Count();
                    result.IsSuccess = true;

                    _logger.LogInformation("Successfully processed {EntriesCount} IIS log entries from {ValidFiles} valid files", 
                        entries.Count(), validFiles.Count);
                }
                else
                {
                    _logger.LogWarning("No valid IIS log files found in provided paths");
                    result.ErrorMessage = "No valid IIS log files found";
                }

                result.Metadata.SuccessRate = result.Metadata.FilesProcessed + result.Metadata.FilesSkipped > 0 ?
                    (double)result.Metadata.FilesProcessed / (result.Metadata.FilesProcessed + result.Metadata.FilesSkipped) * 100 : 0;

                result.ProcessingTime = sw.Elapsed;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing IIS logs");
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                result.ProcessingTime = sw.Elapsed;
                return result;
            }
        }

        /// <summary>
        /// Create optimized view model for IIS data
        /// </summary>
        public IISViewModel CreateViewModel(IEnumerable<IisLogEntry> entries, IEnumerable<string> filePaths)
        {
            var entriesList = entries.ToList();
            var filePathsList = filePaths.ToList();

            var title = filePathsList.Count == 1 ? 
                        Path.GetFileName(filePathsList.First()) : 
                        $"IIS Logs ({filePathsList.Count} files)";

            var viewModel = new IISViewModel
            {
                Title = title,
                FilePath = filePathsList.FirstOrDefault() ?? string.Empty,
                AllEntries = entriesList,
                FilteredEntries = entriesList, // Initially show all
                Statistics = _repository.GetStatistics(entriesList)
            };

            _logger.LogDebug("Created IIS view model '{Title}' with {EntryCount} entries", title, entriesList.Count);
            return viewModel;
        }

        /// <summary>
        /// Apply complex filtering with multiple criteria
        /// </summary>
        public IEnumerable<IisLogEntry> ApplyAdvancedFiltering(IEnumerable<IisLogEntry> entries, IISAdvancedFilterCriteria criteria)
        {
            var filteredEntries = entries.AsEnumerable();

            // Apply time range filtering
            if (criteria.StartTime.HasValue)
            {
                filteredEntries = filteredEntries.Where(e => e.DateTime >= criteria.StartTime.Value);
            }

            if (criteria.EndTime.HasValue)
            {
                filteredEntries = filteredEntries.Where(e => e.DateTime <= criteria.EndTime.Value);
            }

            // Apply status code filtering
            if (criteria.StatusCodes.Any())
            {
                filteredEntries = filteredEntries.Where(e => e.HttpStatus.HasValue && criteria.StatusCodes.Contains(e.HttpStatus.Value));
            }

            // Apply HTTP method filtering
            if (criteria.HttpMethods.Any())
            {
                filteredEntries = filteredEntries.Where(e => !string.IsNullOrEmpty(e.Method) && criteria.HttpMethods.Contains(e.Method));
            }

            // Apply IP address filtering
            if (criteria.IPAddresses.Any())
            {
                filteredEntries = filteredEntries.Where(e => !string.IsNullOrEmpty(e.ClientIPAddress) && criteria.IPAddresses.Contains(e.ClientIPAddress));
            }

            // Apply response time filtering
            if (criteria.MinResponseTime.HasValue)
            {
                filteredEntries = filteredEntries.Where(e => e.TimeTaken >= criteria.MinResponseTime.Value);
            }

            if (criteria.MaxResponseTime.HasValue)
            {
                filteredEntries = filteredEntries.Where(e => e.TimeTaken <= criteria.MaxResponseTime.Value);
            }

            // Apply filter groups
            foreach (var group in criteria.FilterGroups)
            {
                var groupCriteria = new IISFilterCriteria
                {
                    Criteria = group.Criteria,
                    Operation = group.Operation
                };
                filteredEntries = _repository.FilterEntries(filteredEntries, groupCriteria);
            }

            var result = filteredEntries.ToList();
            _logger.LogDebug("Advanced filtering reduced {OriginalCount} entries to {FilteredCount}", entries.Count(), result.Count);

            return result;
        }

        /// <summary>
        /// Get real-time analytics for IIS logs
        /// </summary>
        public IISAnalytics GetAnalytics(IEnumerable<IisLogEntry> entries)
        {
            var entriesList = entries.ToList();
            var analytics = new IISAnalytics();

            if (!entriesList.Any())
                return analytics;

            // Top error pages (4xx, 5xx status codes)
            analytics.TopErrorPages = entriesList
                .Where(e => e.HttpStatus >= 400 && !string.IsNullOrEmpty(e.UriStem))
                .GroupBy(e => e.UriStem!)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToDictionary(g => g.Key, g => g.Count());

            // Top IP addresses
            analytics.TopIPAddresses = entriesList
                .Where(e => !string.IsNullOrEmpty(e.ClientIPAddress))
                .GroupBy(e => e.ClientIPAddress!)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToDictionary(g => g.Key, g => g.Count());

            // Top user agents
            analytics.TopUserAgents = entriesList
                .Where(e => !string.IsNullOrEmpty(e.UserAgent))
                .GroupBy(e => e.UserAgent!)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToDictionary(g => g.Key, g => g.Count());

            // Response time by hour
            analytics.ResponseTimeByHour = entriesList
                .Where(e => e.DateTime.HasValue && e.TimeTaken.HasValue)
                .GroupBy(e => e.DateTime!.Value.Hour)
                .ToDictionary(g => g.Key, g => g.Average(e => e.TimeTaken!.Value));

            // Requests by hour
            analytics.RequestsByHour = entriesList
                .Where(e => e.DateTime.HasValue)
                .GroupBy(e => e.DateTime!.Value.Hour)
                .ToDictionary(g => g.Key, g => g.Count());

            // Generate performance alerts
            analytics.PerformanceAlerts = GeneratePerformanceAlerts(entriesList);

            // Generate security alerts
            analytics.SecurityAlerts = GenerateSecurityAlerts(entriesList);

            _logger.LogDebug("Generated analytics for {EntryCount} IIS entries with {AlertCount} performance alerts and {SecurityAlertCount} security alerts",
                entriesList.Count, analytics.PerformanceAlerts.Count, analytics.SecurityAlerts.Count);

            return analytics;
        }

        /// <summary>
        /// Export IIS data in various formats
        /// </summary>
        public async Task<bool> ExportDataAsync(IEnumerable<IisLogEntry> entries, IISExportFormat format, string filePath)
        {
            try
            {
                _logger.LogInformation("Exporting {EntryCount} IIS entries to {Format} format at {FilePath}", 
                    entries.Count(), format, filePath);

                switch (format)
                {
                    case IISExportFormat.Csv:
                        await ExportToCsvAsync(entries, filePath);
                        break;
                    case IISExportFormat.Json:
                        await ExportToJsonAsync(entries, filePath);
                        break;
                    case IISExportFormat.Excel:
                        await ExportToExcelAsync(entries, filePath);
                        break;
                    case IISExportFormat.Xml:
                        await ExportToXmlAsync(entries, filePath);
                        break;
                    case IISExportFormat.Html:
                        await ExportToHtmlAsync(entries, filePath);
                        break;
                    default:
                        throw new ArgumentException($"Unsupported export format: {format}");
                }

                _logger.LogInformation("Successfully exported IIS data to {FilePath}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting IIS data to {FilePath}", filePath);
                return false;
            }
        }

        /// <summary>
        /// Validate IIS log files and get detailed validation report
        /// </summary>
        public async Task<IISValidationReport> ValidateFilesAsync(IEnumerable<string> filePaths)
        {
            var report = new IISValidationReport();

            try
            {
                _logger.LogInformation("Validating {FileCount} IIS log files", filePaths.Count());

                var validationResults = await _repository.ValidateIISFilesAsync(filePaths);
                report.FileValidationResults = validationResults;

                // Get detailed metadata for valid files
                var validFiles = validationResults.Where(kvp => kvp.Value).Select(kvp => kvp.Key);
                foreach (var filePath in validFiles)
                {
                    try
                    {
                        var metadata = await GetFileMetadata(filePath);
                        report.FileMetadata[filePath] = metadata;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not get metadata for file {FilePath}", filePath);
                    }
                }

                // Collect validation errors for invalid files
                var invalidFiles = validationResults.Where(kvp => !kvp.Value).Select(kvp => kvp.Key);
                foreach (var filePath in invalidFiles)
                {
                    report.ValidationErrors[filePath] = new List<string> { "File is not a valid IIS log format" };
                }

                _logger.LogInformation("Validation complete: {ValidFiles} valid, {InvalidFiles} invalid files", 
                    report.ValidFilesCount, report.InvalidFilesCount);

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during IIS file validation");
                throw;
            }
        }

        #region Private Methods

        /// <summary>
        /// Get log files from directories
        /// </summary>
        private IEnumerable<string> GetLogFilesFromDirectories(IEnumerable<string> directories)
        {
            var files = new List<string>();

            foreach (var directory in directories)
            {
                if (Directory.Exists(directory))
                {
                    var logFiles = Directory.EnumerateFiles(directory, "*.log", SearchOption.TopDirectoryOnly);
                    files.AddRange(logFiles);
                }
            }

            return files;
        }

        /// <summary>
        /// Generate performance alerts based on IIS data
        /// </summary>
        private List<IISPerformanceAlert> GeneratePerformanceAlerts(List<IisLogEntry> entries)
        {
            var alerts = new List<IISPerformanceAlert>();

            // High response time alert
            var slowRequests = entries.Where(e => e.TimeTaken > 5000).ToList(); // > 5 seconds
            if (slowRequests.Any())
            {
                alerts.Add(new IISPerformanceAlert
                {
                    AlertType = "High Response Time",
                    Message = $"{slowRequests.Count} requests with response time > 5 seconds",
                    Timestamp = DateTimeOffset.Now,
                    Severity = "Warning",
                    Data = new Dictionary<string, object>
                    {
                        ["SlowRequestCount"] = slowRequests.Count,
                        ["AverageSlowResponseTime"] = slowRequests.Average(e => e.TimeTaken ?? 0)
                    }
                });
            }

            // High error rate alert
            var errorRequests = entries.Where(e => e.HttpStatus >= 500).ToList();
            if (errorRequests.Count > entries.Count * 0.05) // > 5% error rate
            {
                alerts.Add(new IISPerformanceAlert
                {
                    AlertType = "High Error Rate",
                    Message = $"Error rate is {((double)errorRequests.Count / entries.Count * 100):F1}%",
                    Timestamp = DateTimeOffset.Now,
                    Severity = "Critical",
                    Data = new Dictionary<string, object>
                    {
                        ["ErrorCount"] = errorRequests.Count,
                        ["TotalRequests"] = entries.Count,
                        ["ErrorRate"] = (double)errorRequests.Count / entries.Count * 100
                    }
                });
            }

            return alerts;
        }

        /// <summary>
        /// Generate security alerts based on IIS data
        /// </summary>
        private List<IISSecurityAlert> GenerateSecurityAlerts(List<IisLogEntry> entries)
        {
            var alerts = new List<IISSecurityAlert>();

            // Suspicious IP activity
            var ipGroups = entries
                .Where(e => !string.IsNullOrEmpty(e.ClientIPAddress))
                .GroupBy(e => e.ClientIPAddress!)
                .Where(g => g.Count() > 1000) // > 1000 requests from single IP
                .ToList();

            foreach (var ipGroup in ipGroups)
            {
                alerts.Add(new IISSecurityAlert
                {
                    AlertType = "Suspicious IP Activity",
                    IPAddress = ipGroup.Key,
                    Message = $"IP {ipGroup.Key} made {ipGroup.Count()} requests",
                    Timestamp = DateTimeOffset.Now,
                    RiskLevel = "Medium",
                    Evidence = new Dictionary<string, object>
                    {
                        ["RequestCount"] = ipGroup.Count(),
                        ["UniquePages"] = ipGroup.Select(e => e.UriStem).Distinct().Count(),
                        ["ErrorRequests"] = ipGroup.Count(e => e.HttpStatus >= 400)
                    }
                });
            }

            return alerts;
        }

        /// <summary>
        /// Get detailed metadata for a file
        /// </summary>
        private async Task<IISFileMetadata> GetFileMetadata(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            var metadata = new IISFileMetadata
            {
                FileSize = fileInfo.Length,
                FileCreated = fileInfo.CreationTime,
                FileModified = fileInfo.LastWriteTime
            };

            // Read first few lines to extract IIS metadata
            using var reader = new StreamReader(filePath);
            var lineCount = 0;
            string? line;
            
            while ((line = await reader.ReadLineAsync()) != null && lineCount < 10)
            {
                if (line.StartsWith("#Software:"))
                {
                    metadata.IISVersion = line.Substring("#Software:".Length).Trim();
                }
                else if (line.StartsWith("#Fields:"))
                {
                    metadata.FieldNames = line.Substring("#Fields:".Length).Trim().Split(' ').ToList();
                }
                
                lineCount++;
            }

            // Estimate entry count (rough estimate based on file size)
            metadata.EstimatedEntryCount = (int)(fileInfo.Length / 200); // Rough estimate: 200 bytes per entry

            return metadata;
        }

        /// <summary>
        /// Export to CSV format
        /// </summary>
        private async Task ExportToCsvAsync(IEnumerable<IisLogEntry> entries, string filePath)
        {
            using var writer = new StreamWriter(filePath);
            
            // Write header
            await writer.WriteLineAsync("DateTime,ClientIP,Method,UriStem,UriQuery,HttpStatus,TimeTaken,UserAgent");
            
            // Write data
            foreach (var entry in entries)
            {
                var line = $"{entry.DateTime},{entry.ClientIPAddress},{entry.Method},{entry.UriStem},{entry.UriQuery},{entry.HttpStatus},{entry.TimeTaken},\"{entry.UserAgent}\"";
                await writer.WriteLineAsync(line);
            }
        }

        /// <summary>
        /// Export to JSON format
        /// </summary>
        private async Task ExportToJsonAsync(IEnumerable<IisLogEntry> entries, string filePath)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(entries, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            await File.WriteAllTextAsync(filePath, json);
        }

        /// <summary>
        /// Export to Excel format (basic implementation)
        /// </summary>
        private async Task ExportToExcelAsync(IEnumerable<IisLogEntry> entries, string filePath)
        {
            // For now, export as CSV with .xlsx extension
            // In a real implementation, you would use a library like EPPlus or ClosedXML
            await ExportToCsvAsync(entries, filePath);
        }

        /// <summary>
        /// Export to XML format
        /// </summary>
        private async Task ExportToXmlAsync(IEnumerable<IisLogEntry> entries, string filePath)
        {
            using var writer = new StreamWriter(filePath);
            await writer.WriteLineAsync("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            await writer.WriteLineAsync("<IISLogs>");
            
            foreach (var entry in entries)
            {
                await writer.WriteLineAsync($"  <LogEntry>");
                await writer.WriteLineAsync($"    <DateTime>{entry.DateTime}</DateTime>");
                await writer.WriteLineAsync($"    <ClientIP>{entry.ClientIPAddress}</ClientIP>");
                await writer.WriteLineAsync($"    <Method>{entry.Method}</Method>");
                await writer.WriteLineAsync($"    <UriStem>{entry.UriStem}</UriStem>");
                await writer.WriteLineAsync($"    <HttpStatus>{entry.HttpStatus}</HttpStatus>");
                await writer.WriteLineAsync($"    <TimeTaken>{entry.TimeTaken}</TimeTaken>");
                await writer.WriteLineAsync($"  </LogEntry>");
            }
            
            await writer.WriteLineAsync("</IISLogs>");
        }

        /// <summary>
        /// Export to HTML format
        /// </summary>
        private async Task ExportToHtmlAsync(IEnumerable<IisLogEntry> entries, string filePath)
        {
            using var writer = new StreamWriter(filePath);
            await writer.WriteLineAsync("<!DOCTYPE html>");
            await writer.WriteLineAsync("<html><head><title>IIS Logs</title></head><body>");
            await writer.WriteLineAsync("<table border='1'>");
            await writer.WriteLineAsync("<tr><th>DateTime</th><th>Client IP</th><th>Method</th><th>URI</th><th>Status</th><th>Time Taken</th></tr>");
            
            foreach (var entry in entries)
            {
                await writer.WriteLineAsync($"<tr><td>{entry.DateTime}</td><td>{entry.ClientIPAddress}</td><td>{entry.Method}</td><td>{entry.UriStem}</td><td>{entry.HttpStatus}</td><td>{entry.TimeTaken}</td></tr>");
            }
            
            await writer.WriteLineAsync("</table>");
            await writer.WriteLineAsync("</body></html>");
        }

        #endregion
    }
}

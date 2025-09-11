namespace Log_Parser_App.Services.Parsing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Log_Parser_App.Models;
    using Log_Parser_App.Interfaces;
    using Microsoft.Extensions.Logging;

    public class RabbitMqLogParsingOrchestrator : IRabbitMqLogParserService
    {
        private readonly RabbitMqLogParserService _singleFileParser;
        private readonly IPairedFileDetectionService _pairedFileDetection;
        private readonly IPairedRabbitMqLogParserService _pairedFileParser;
        private readonly ILogger<RabbitMqLogParsingOrchestrator> _logger;

        public RabbitMqLogParsingOrchestrator(
            RabbitMqLogParserService singleFileParser,
            IPairedFileDetectionService pairedFileDetection,
            IPairedRabbitMqLogParserService pairedFileParser,
            ILogger<RabbitMqLogParsingOrchestrator> logger)
        {
            _singleFileParser = singleFileParser ?? throw new ArgumentNullException(nameof(singleFileParser));
            _pairedFileDetection = pairedFileDetection ?? throw new ArgumentNullException(nameof(pairedFileDetection));
            _pairedFileParser = pairedFileParser ?? throw new ArgumentNullException(nameof(pairedFileParser));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async IAsyncEnumerable<RabbitMqLogEntry> ParseLogFileAsync(
            string filePath, 
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                _logger.LogWarning("File path is null or empty");
                yield break;
            }

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File does not exist: {FilePath}", filePath);
                yield break;
            }

            // Smart routing: check if this is a paired file scenario
            if (_pairedFileDetection.IsMainMessageFile(filePath))
            {
                _logger.LogDebug("Detected main message file, attempting paired parsing: {FilePath}", filePath);
                
                var pairedFile = await _pairedFileDetection.FindPairedFileAsync(filePath);
                var entry = await _pairedFileParser.ParseSinglePairedFileAsync(pairedFile, cancellationToken);
                
                if (entry != null)
                {
                    yield return entry;
                }
            }
            else
            {
                // Traditional single file parsing
                _logger.LogDebug("Using traditional single file parsing for: {FilePath}", filePath);
                await foreach (var entry in _singleFileParser.ParseLogFileAsync(filePath, cancellationToken))
                {
                    yield return entry;
                }
            }
        }

        public async IAsyncEnumerable<RabbitMqLogEntry> ParseLogFilesAsync(
            IEnumerable<string> filePaths, 
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (filePaths == null)
            {
                _logger.LogWarning("File paths collection is null");
                yield break;
            }

            var filePathsList = filePaths.ToList();
            
            // Check if any files match paired file patterns
            var mainFiles = filePathsList.Where(fp => _pairedFileDetection.IsMainMessageFile(fp)).ToList();
            
            if (mainFiles.Any())
            {
                _logger.LogInformation("Found {Count} main message files, attempting paired file parsing", mainFiles.Count);
                
                // Parse as paired files
                foreach (var mainFile in mainFiles)
                {
                    var pairedFile = await _pairedFileDetection.FindPairedFileAsync(mainFile);
                    var entry = await _pairedFileParser.ParseSinglePairedFileAsync(pairedFile, cancellationToken);
                    
                    if (entry != null)
                    {
                        yield return entry;
                    }
                }
            }
            else
            {
                // Traditional multi-file parsing
                _logger.LogDebug("Using traditional multi-file parsing for {Count} files", filePathsList.Count);
                await foreach (var entry in _singleFileParser.ParseLogFilesAsync(filePathsList, cancellationToken))
                {
                    yield return entry;
                }
            }
        }

        public async IAsyncEnumerable<RabbitMqLogEntry> ParseLogDirectoryAsync(
            string directoryPath, 
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                _logger.LogWarning("Directory path is null or empty");
                yield break;
            }

            if (!Directory.Exists(directoryPath))
            {
                _logger.LogWarning("Directory does not exist: {DirectoryPath}", directoryPath);
                yield break;
            }

            _logger.LogInformation("Starting directory parsing with paired file detection: {DirectoryPath}", directoryPath);
            Console.WriteLine($"[ORCHESTRATOR] Starting directory parsing: {directoryPath}");
            
            // Debug: Check what files are in the directory
            var allFiles = Directory.GetFiles(directoryPath);
            Console.WriteLine($"[ORCHESTRATOR] Found {allFiles.Length} total files in directory");
            foreach (var file in allFiles.Take(10)) {
                var fileName = Path.GetFileName(file);
                var isMain = _pairedFileDetection.IsMainMessageFile(file);
                var isHeaders = _pairedFileDetection.IsHeadersFile(file);
                Console.WriteLine($"[ORCHESTRATOR] File: {fileName} - IsMain: {isMain}, IsHeaders: {isHeaders}");
            }

            // Use paired file detection to scan directory
            var detectedCount = 0;
            await foreach (var pairedFile in _pairedFileDetection.DetectPairedFilesAsync(directoryPath, cancellationToken))
            {
                detectedCount++;
                Console.WriteLine($"[ORCHESTRATOR] Processing paired file {detectedCount}: {pairedFile.MessageId} - {pairedFile.Status}");
                
                cancellationToken.ThrowIfCancellationRequested();

                var entry = await _pairedFileParser.ParseSinglePairedFileAsync(pairedFile, cancellationToken);
                if (entry != null)
                {
                    Console.WriteLine($"[ORCHESTRATOR] Successfully parsed entry for {pairedFile.MessageId}");
                    yield return entry;
                }
                else
                {
                    Console.WriteLine($"[ORCHESTRATOR] Failed to parse entry for {pairedFile.MessageId}");
                }
            }

            Console.WriteLine($"[ORCHESTRATOR] Completed directory parsing. Detected {detectedCount} paired files.");
            _logger.LogInformation("Completed directory parsing: {DirectoryPath}", directoryPath);
        }

        public async Task<bool> IsValidRabbitMqLogFileAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            // Check if it is a paired file first
            if (_pairedFileDetection.IsMainMessageFile(filePath) || _pairedFileDetection.IsHeadersFile(filePath))
            {
                return File.Exists(filePath);
            }

            // Fall back to traditional validation
            return await _singleFileParser.IsValidRabbitMqLogFileAsync(filePath);
        }

        public async Task<int> GetEstimatedLogCountAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return 0;

            if (Directory.Exists(filePath))
            {
                // Directory case - count paired files
                var pairedFiles = await _pairedFileDetection.DetectPairedFilesListAsync(filePath);
                return pairedFiles.Count;
            }
            else if (_pairedFileDetection.IsMainMessageFile(filePath))
            {
                // Single paired file
                return File.Exists(filePath) ? 1 : 0;
            }
            else
            {
                // Traditional file counting
                return await _singleFileParser.GetEstimatedLogCountAsync(filePath);
            }
        }
    }
}

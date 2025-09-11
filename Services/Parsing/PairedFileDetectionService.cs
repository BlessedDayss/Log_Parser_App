namespace Log_Parser_App.Services.Parsing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Log_Parser_App.Models;
    using Microsoft.Extensions.Logging;

    public class PairedFileDetectionService : IPairedFileDetectionService
    {
        private static readonly Regex MainFilePattern = new(@"^msg-(\d+)$", RegexOptions.Compiled);
        private static readonly Regex HeadersFilePattern = new(@"^msg-(\d+)-headers\+properties\.json$", RegexOptions.Compiled);

        private readonly ILogger<PairedFileDetectionService> _logger;

        public PairedFileDetectionService(ILogger<PairedFileDetectionService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async IAsyncEnumerable<PairedFileData> DetectPairedFilesAsync(
            string directoryPath, 
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
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

            _logger.LogInformation("Starting paired file detection in directory: {DirectoryPath}", directoryPath);

            // Debug: Test regex patterns on first few files
            var testFiles = Directory.GetFiles(directoryPath).Take(5).Select(Path.GetFileName).ToList();
            _logger.LogInformation("Testing regex on first 5 files: {Files}", string.Join(", ", testFiles));
            
            foreach (var testFile in testFiles)
            {
                var isMain = IsMainMessageFile(Path.Combine(directoryPath, testFile!));
                var isHeaders = IsHeadersFile(Path.Combine(directoryPath, testFile!));
                var messageId = ExtractMessageId(testFile!);
                _logger.LogInformation("File: {File}, IsMain: {IsMain}, IsHeaders: {IsHeaders}, MessageId: {MessageId}", 
                    testFile, isMain, isHeaders, messageId);
            }

            ConcurrentDictionary<string, PairedFileData> pairedFiles;
            
            try
            {
                pairedFiles = await BuildFilePairingMapAsync(directoryPath, cancellationToken);
                Console.WriteLine($"[DETECTOR] Built pairing map with {pairedFiles.Count} entries");
                _logger.LogInformation("Completed paired file detection. Found {Count} paired files", pairedFiles.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Paired file detection was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during paired file detection in directory: {DirectoryPath}", directoryPath);
                throw;
            }

            // Yield results outside of try-catch block
            foreach (var pairedFile in pairedFiles.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return pairedFile;
            }

            // Also check for unified JSON files and upgrade existing pairs
            var directoryFiles = Directory.GetFiles(directoryPath);
            foreach (var filePath in directoryFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var fileName = Path.GetFileName(filePath);
                if (IsMainMessageFile(filePath))
                {
                    var messageId = ExtractMessageId(fileName);
                    if (!string.IsNullOrEmpty(messageId))
                    {
                        if (pairedFiles.ContainsKey(messageId))
                        {
                            // This main file is already paired with headers - check if it's unified JSON
                            if (await IsUnifiedJsonFileAsync(filePath))
                            {
                                var existingPair = pairedFiles[messageId];
                                Console.WriteLine($"[DETECTOR] Upgrading paired file to unified JSON: {fileName}");
                                // Mark this as unified so the parser knows to use enhanced logic
                                existingPair.Status = PairedFileStatus.UnifiedJson;
                            }
                        }
                        else
                        {
                            // This is a main file that wasn't included in pairing (no headers file)
                            // Check if it might be a unified JSON file
                            if (await IsUnifiedJsonFileAsync(filePath))
                            {
                                Console.WriteLine($"[DETECTOR] Found standalone unified JSON file: {fileName}");
                                yield return PairedFileData.CreatePartial(filePath, messageId);
                            }
                        }
                    }
                }
            }
        }

        public async Task<List<PairedFileData>> DetectPairedFilesListAsync(
            string directoryPath, 
            CancellationToken cancellationToken = default)
        {
            var result = new List<PairedFileData>();
            await foreach (var pairedFile in DetectPairedFilesAsync(directoryPath, cancellationToken))
            {
                result.Add(pairedFile);
            }
            return result;
        }

        public async Task<PairedFileData> FindPairedFileAsync(string mainFilePath)
        {
            if (string.IsNullOrWhiteSpace(mainFilePath))
            {
                return PairedFileData.CreateFailed("", "Main file path is null or empty");
            }

            if (!File.Exists(mainFilePath))
            {
                return PairedFileData.CreateFailed("", $"Main file does not exist: {mainFilePath}");
            }

            var fileName = Path.GetFileName(mainFilePath);
            var messageId = ExtractMessageId(fileName);

            if (string.IsNullOrEmpty(messageId))
            {
                return PairedFileData.CreateFailed("", $"Invalid main file name pattern: {fileName}");
            }

            var directory = Path.GetDirectoryName(mainFilePath) ?? string.Empty;
            var headersFileName = $"msg-{messageId}-headers+properties.json";
            var headersFilePath = Path.Combine(directory, headersFileName);

            await Task.Yield();

            if (File.Exists(headersFilePath))
            {
                _logger.LogDebug("Found complete paired files for message {MessageId}", messageId);
                return PairedFileData.CreateComplete(mainFilePath, headersFilePath, messageId);
            }
            else
            {
                _logger.LogDebug("Found partial paired files for message {MessageId} (headers file missing)", messageId);
                return PairedFileData.CreatePartial(mainFilePath, messageId);
            }
        }

        public bool IsMainMessageFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            var fileName = Path.GetFileName(filePath);
            return MainFilePattern.IsMatch(fileName);
        }

        public bool IsHeadersFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            var fileName = Path.GetFileName(filePath);
            return HeadersFilePattern.IsMatch(fileName);
        }

        public string? ExtractMessageId(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            var mainMatch = MainFilePattern.Match(fileName);
            if (mainMatch.Success)
            {
                return mainMatch.Groups[1].Value;
            }

            var headersMatch = HeadersFilePattern.Match(fileName);
            if (headersMatch.Success)
            {
                return headersMatch.Groups[1].Value;
            }

            return null;
        }

        private async Task<ConcurrentDictionary<string, PairedFileData>> BuildFilePairingMapAsync(
            string directoryPath, 
            CancellationToken cancellationToken)
        {
            var pairedFiles = new ConcurrentDictionary<string, PairedFileData>();

            await Task.Run(() =>
            {
                var allFiles = Directory.GetFiles(directoryPath)
                    .Select(Path.GetFileName)
                    .Where(f => !string.IsNullOrEmpty(f))
                    .ToList();

                Parallel.ForEach(allFiles, new ParallelOptions { CancellationToken = cancellationToken }, fileName =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var messageId = ExtractMessageId(fileName!);
                    if (string.IsNullOrEmpty(messageId))
                        return;

                    var fullPath = Path.Combine(directoryPath, fileName!);

                    if (IsMainMessageFile(fullPath))
                    {
                        pairedFiles.AddOrUpdate(messageId,
                            PairedFileData.CreatePartial(fullPath, messageId),
                            (key, existing) =>
                            {
                                if (string.IsNullOrEmpty(existing.MainFilePath))
                                {
                                    existing.MainFilePath = fullPath;
                                }

                                if (!string.IsNullOrEmpty(existing.HeadersFilePath))
                                {
                                    existing.Status = PairedFileStatus.Complete;
                                }

                                return existing;
                            });
                    }
                    else if (IsHeadersFile(fullPath))
                    {
                        pairedFiles.AddOrUpdate(messageId,
                            new PairedFileData
                            {
                                HeadersFilePath = fullPath,
                                MessageId = messageId,
                                Status = PairedFileStatus.Partial
                            },
                            (key, existing) =>
                            {
                                if (string.IsNullOrEmpty(existing.HeadersFilePath))
                                {
                                    existing.HeadersFilePath = fullPath;
                                }

                                if (!string.IsNullOrEmpty(existing.MainFilePath))
                                {
                                    existing.Status = PairedFileStatus.Complete;
                                }

                                return existing;
                            });
                    }
                });

            }, cancellationToken);

            _logger.LogDebug("Built file pairing map with {Count} entries", pairedFiles.Count);
            return pairedFiles;
        }

        private async Task<bool> IsUnifiedJsonFileAsync(string filePath)
        {
            try
            {
                var content = await File.ReadAllTextAsync(filePath);
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                // Check if this looks like a unified RabbitMQ message (has message, sentTime, headers)
                return root.TryGetProperty("message", out _) && 
                       root.TryGetProperty("sentTime", out _) &&
                       root.TryGetProperty("headers", out _);
            }
            catch
            {
                return false;
            }
        }
    }
}

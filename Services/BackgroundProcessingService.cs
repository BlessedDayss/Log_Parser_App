using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Interfaces;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services
{
    /// <summary>
    /// Service for executing heavy operations in background threads
    /// Keeps UI responsive during long-running tasks
    /// </summary>
    public class BackgroundProcessingService : IBackgroundProcessingService, IDisposable
    {
        private readonly ILogger<BackgroundProcessingService> _logger;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeOperations;
        private readonly SemaphoreSlim _concurrencyLimiter;
        private readonly CancellationTokenSource _serviceCancellation;
        private bool _disposed;

        public BackgroundProcessingService(ILogger<BackgroundProcessingService> logger)
        {
            _logger = logger;
            _activeOperations = new ConcurrentDictionary<string, CancellationTokenSource>();
            _concurrencyLimiter = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
            _serviceCancellation = new CancellationTokenSource();
        }

        public int ActiveOperationsCount => _activeOperations.Count;

        public event EventHandler<ProcessingProgress>? OperationStarted;
        public event EventHandler<ProcessingProgress>? OperationCompleted;
        public event EventHandler<ProcessingProgress>? ProgressUpdated;

        /// <summary>
        /// Execute operation in background with progress reporting
        /// </summary>
        public async Task<T> ProcessAsync<T>(
            Func<CancellationToken, IProgress<ProcessingProgress>, Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            if (_disposed)
                throw new ObjectDisposedException(nameof(BackgroundProcessingService));

            var operationId = Guid.NewGuid().ToString();
            var operationCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _serviceCancellation.Token);

            _activeOperations[operationId] = operationCts;

            try
            {
                await _concurrencyLimiter.WaitAsync(operationCts.Token);

                var progress = new Progress<ProcessingProgress>(p =>
                {
                    p.OperationId = operationId;
                    ProgressUpdated?.Invoke(this, p);
                    _logger.LogTrace("Operation {OperationId} progress: {Percentage:F1}% - {Operation}", 
                        operationId, p.PercentageComplete, p.CurrentOperation);
                });

                var startProgress = new ProcessingProgress
                {
                    OperationId = operationId,
                    CurrentOperation = "Starting background operation",
                    ProcessedItems = 0,
                    TotalItems = 0
                };

                OperationStarted?.Invoke(this, startProgress);
                _logger.LogDebug("Started background operation {OperationId}", operationId);

                var startTime = DateTime.UtcNow;
                var result = await Task.Run(async () =>
                {
                    return await operation(operationCts.Token, progress);
                }, operationCts.Token);

                var completedProgress = new ProcessingProgress
                {
                    OperationId = operationId,
                    CurrentOperation = "Operation completed successfully",
                    IsCompleted = true,
                    ElapsedTime = DateTime.UtcNow - startTime
                };

                OperationCompleted?.Invoke(this, completedProgress);
                _logger.LogDebug("Completed background operation {OperationId} in {ElapsedTime}", 
                    operationId, completedProgress.ElapsedTime);

                return result;
            }
            catch (OperationCanceledException)
            {
                var cancelledProgress = new ProcessingProgress
                {
                    OperationId = operationId,
                    CurrentOperation = "Operation cancelled",
                    IsCancelled = true
                };

                OperationCompleted?.Invoke(this, cancelledProgress);
                _logger.LogDebug("Cancelled background operation {OperationId}", operationId);
                throw;
            }
            catch (Exception ex)
            {
                var errorProgress = new ProcessingProgress
                {
                    OperationId = operationId,
                    CurrentOperation = "Operation failed",
                    Error = ex
                };

                OperationCompleted?.Invoke(this, errorProgress);
                _logger.LogError(ex, "Failed background operation {OperationId}", operationId);
                throw;
            }
            finally
            {
                _concurrencyLimiter.Release();
                _activeOperations.TryRemove(operationId, out _);
                operationCts.Dispose();
            }
        }

        /// <summary>
        /// Execute operation in background without return value
        /// </summary>
        public async Task ProcessAsync(
            Func<CancellationToken, IProgress<ProcessingProgress>, Task> operation,
            CancellationToken cancellationToken = default)
        {
            await ProcessAsync<object?>(async (ct, progress) =>
            {
                await operation(ct, progress);
                return null;
            }, cancellationToken);
        }

        /// <summary>
        /// Cancel all currently running operations
        /// </summary>
        public async Task CancelAllOperationsAsync()
        {
            _logger.LogInformation("Cancelling all active operations ({Count})", _activeOperations.Count);

            var cancellationTasks = new List<Task>();

            foreach (var kvp in _activeOperations)
            {
                var operationId = kvp.Key;
                var cts = kvp.Value;

                cancellationTasks.Add(Task.Run(() =>
                {
                    try
                    {
                        cts.Cancel();
                        _logger.LogDebug("Cancelled operation {OperationId}", operationId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error cancelling operation {OperationId}", operationId);
                    }
                }));
            }

            if (cancellationTasks.Count > 0)
            {
                await Task.WhenAll(cancellationTasks);
                
                // Wait a bit for operations to actually cancel
                var timeout = TimeSpan.FromSeconds(5);
                var waitStart = DateTime.UtcNow;
                
                while (_activeOperations.Count > 0 && DateTime.UtcNow - waitStart < timeout)
                {
                    await Task.Delay(100);
                }

                if (_activeOperations.Count > 0)
                {
                    _logger.LogWarning("Some operations did not cancel within timeout. Remaining: {Count}", 
                        _activeOperations.Count);
                }
            }

            _logger.LogInformation("Cancellation completed. Remaining operations: {Count}", _activeOperations.Count);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _logger.LogDebug("Disposing BackgroundProcessingService");

            try
            {
                _serviceCancellation.Cancel();
                
                // Cancel all operations synchronously
                foreach (var kvp in _activeOperations)
                {
                    try
                    {
                        kvp.Value.Cancel();
                        kvp.Value.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error disposing operation {OperationId}", kvp.Key);
                    }
                }

                _activeOperations.Clear();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during BackgroundProcessingService disposal");
            }
            finally
            {
                _serviceCancellation.Dispose();
                _concurrencyLimiter.Dispose();
                _disposed = true;
            }
        }
    }
} 